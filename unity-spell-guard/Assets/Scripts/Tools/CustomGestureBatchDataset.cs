using System;
using System.Collections.Generic;
using System.IO;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tools
{
    [Serializable]
    public sealed class CustomGestureBatchDataset
    {
        public string DatasetName = "custom_gesture_batch";
        public float DefaultFps = 30f;
        public List<CustomGestureBatchClip> Clips = new List<CustomGestureBatchClip>();

        public static CustomGestureBatchDataset LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Dataset path is empty.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Dataset json was not found.", path);
            }

            var dataset = JsonUtility.FromJson<CustomGestureBatchDataset>(File.ReadAllText(path));
            if (dataset == null)
            {
                throw new InvalidDataException($"Dataset json could not be parsed: {path}");
            }

            dataset.Clips ??= new List<CustomGestureBatchClip>();
            return dataset;
        }
    }

    [Serializable]
    public sealed class CustomGestureBatchClip
    {
        public string ClipId;
        public string Label;
        public string Split = "test";
        public string Handedness = "Right";
        public float Fps = 30f;
        public List<CustomGestureBatchFrame> Frames = new List<CustomGestureBatchFrame>();

        public bool IsTrain => string.Equals(Split, "train", StringComparison.OrdinalIgnoreCase);
        public bool IsValidation => string.Equals(Split, "val", StringComparison.OrdinalIgnoreCase) || string.Equals(Split, "validation", StringComparison.OrdinalIgnoreCase);
        public bool IsTest => string.Equals(Split, "test", StringComparison.OrdinalIgnoreCase);

        public CustomGestureSample ToSample(float defaultFps)
        {
            var frames = BuildFrameSamples(defaultFps);
            if (frames.Count == 0)
            {
                return null;
            }

            return new CustomGestureSample
            {
                SampleId = string.IsNullOrWhiteSpace(ClipId) ? Guid.NewGuid().ToString("N") : ClipId,
                Handedness = ParseHandedness(Handedness),
                DurationSeconds = frames[frames.Count - 1].Time,
                Frames = frames
            };
        }

        public List<GestureFrame> ToRuntimeFrames(float defaultFps)
        {
            var result = new List<GestureFrame>();
            var frameSamples = BuildFrameSamples(defaultFps);
            var handedness = ParseHandedness(Handedness);
            for (var index = 0; index < frameSamples.Count; index++)
            {
                var sample = frameSamples[index];
                result.Add(new GestureFrame
                {
                    FrameId = index + 1,
                    Timestamp = sample.Time,
                    Source = GestureSourceKind.ExternalBridge,
                    LatestMotion = MotionGestureEvent.None,
                    Hands = new[]
                    {
                        new TrackedHandState
                        {
                            TrackId = 1,
                            Handedness = handedness,
                            IsTracked = true,
                            StaticGesture = sample.StaticGesture,
                            Confidence = sample.Confidence,
                            ViewportPosition = sample.PalmCenter,
                            PalmCenter = sample.PalmCenter,
                            Landmarks = sample.Landmarks
                        }
                    }
                });
            }

            return result;
        }

        private List<CustomGestureFrameSample> BuildFrameSamples(float defaultFps)
        {
            var result = new List<CustomGestureFrameSample>();
            if (Frames == null)
            {
                return result;
            }

            var fps = Fps > 0f ? Fps : defaultFps;
            if (fps <= 0f)
            {
                fps = 30f;
            }

            for (var index = 0; index < Frames.Count; index++)
            {
                var frame = Frames[index];
                if (frame == null || frame.Landmarks == null || frame.Landmarks.Count < CustomGestureFeatureExtractor.RequiredLandmarkCount)
                {
                    continue;
                }

                var landmarks = new Vector2[CustomGestureFeatureExtractor.RequiredLandmarkCount];
                for (var pointIndex = 0; pointIndex < landmarks.Length; pointIndex++)
                {
                    landmarks[pointIndex] = frame.Landmarks[pointIndex].ToVector2();
                }

                var palm = frame.HasPalmCenter
                    ? frame.PalmCenter.ToVector2()
                    : (landmarks[0] + landmarks[5] + landmarks[17]) / 3f;
                result.Add(new CustomGestureFrameSample
                {
                    Time = frame.Time >= 0f ? frame.Time : result.Count / fps,
                    Confidence = Mathf.Clamp01(frame.Confidence <= 0f ? 1f : frame.Confidence),
                    StaticGesture = ParseGestureType(frame.StaticGesture),
                    PalmCenter = palm,
                    Landmarks = landmarks
                });
            }

            return result;
        }

        private static GestureType ParseGestureType(string value)
        {
            return Enum.TryParse(value, true, out GestureType gesture) ? gesture : GestureType.OpenPalm;
        }

        private static GestureHandedness ParseHandedness(string value)
        {
            if (Enum.TryParse(value, true, out GestureHandedness handedness) && handedness != GestureHandedness.Unknown)
            {
                return handedness;
            }

            return GestureHandedness.Right;
        }
    }

    [Serializable]
    public sealed class CustomGestureBatchFrame
    {
        public float Time = -1f;
        public float Confidence = 1f;
        public string StaticGesture = "OpenPalm";
        public bool HasPalmCenter;
        public CustomGestureBatchPoint PalmCenter;
        public List<CustomGestureBatchPoint> Landmarks = new List<CustomGestureBatchPoint>();
    }

    [Serializable]
    public struct CustomGestureBatchPoint
    {
        public float X;
        public float Y;

        public Vector2 ToVector2()
        {
            return new Vector2(X, Y);
        }
    }
}

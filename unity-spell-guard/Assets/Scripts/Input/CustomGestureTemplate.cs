using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public enum CustomGestureKind
    {
        StaticPose,
        DynamicMotion
    }

    [Serializable]
    public sealed class CustomGestureTemplate
    {
        public string GestureId;
        public string DisplayName;
        public CustomGestureKind Kind = CustomGestureKind.DynamicMotion;
        public GestureHandedness RequiredHandedness = GestureHandedness.Unknown;
        public GestureIntent TargetIntent = GestureIntent.CustomGesture;
        public float MatchThreshold = CustomGestureRecognizer.DefaultDynamicThreshold;
        public List<CustomGestureSample> Samples = new List<CustomGestureSample>();

        public bool HasSamples => Samples != null && Samples.Count > 0;
    }

    [Serializable]
    public sealed class CustomGestureSample
    {
        public string SampleId;
        public GestureHandedness Handedness = GestureHandedness.Unknown;
        public float DurationSeconds;
        public List<CustomGestureFrameSample> Frames = new List<CustomGestureFrameSample>();

        public bool HasFrames => Frames != null && Frames.Count > 0;
    }

    [Serializable]
    public sealed class CustomGestureFrameSample
    {
        public float Time;
        public float Confidence;
        public Vector2[] Landmarks = Array.Empty<Vector2>();
    }
}

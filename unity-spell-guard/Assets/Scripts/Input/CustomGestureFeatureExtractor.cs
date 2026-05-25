using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public static class CustomGestureFeatureExtractor
    {
        public const int RequiredLandmarkCount = 21;
        public const int FeatureLength = RequiredLandmarkCount * 2;
        private const int WristIndex = 0;
        private const int ThumbTipIndex = 4;
        private const int IndexMcpIndex = 5;
        private const int IndexPipIndex = 6;
        private const int IndexTipIndex = 8;
        private const int MiddleMcpIndex = 9;
        private const int MiddlePipIndex = 10;
        private const int MiddleTipIndex = 12;
        private const int RingMcpIndex = 13;
        private const int RingPipIndex = 14;
        private const int RingTipIndex = 16;
        private const int PinkyMcpIndex = 17;
        private const int PinkyPipIndex = 18;
        private const int PinkyTipIndex = 20;
        private const float MinimumScale = 0.0001f;

        public static bool TryExtract(IReadOnlyList<Vector2> landmarks, float confidence, float minimumConfidence, out float[] features)
        {
            features = null;
            if (confidence < minimumConfidence || landmarks == null || landmarks.Count < RequiredLandmarkCount)
            {
                return false;
            }

            var wrist = landmarks[WristIndex];
            var middleMcpDistance = Vector2.Distance(wrist, landmarks[MiddleMcpIndex]);
            var palmWidth = Vector2.Distance(landmarks[IndexMcpIndex], landmarks[PinkyMcpIndex]);
            var scale = Mathf.Max(middleMcpDistance, palmWidth, MinimumScale);
            if (scale <= MinimumScale)
            {
                return false;
            }

            features = new float[FeatureLength];
            for (var index = 0; index < RequiredLandmarkCount; index++)
            {
                var normalized = (landmarks[index] - wrist) / scale;
                var featureIndex = index * 2;
                features[featureIndex] = normalized.x;
                features[featureIndex + 1] = normalized.y;
            }

            return true;
        }

        public static bool TryExtract(CustomGestureFrameSample frame, float minimumConfidence, out float[] features)
        {
            if (frame == null)
            {
                features = null;
                return false;
            }

            return TryExtract(frame.Landmarks, frame.Confidence, minimumConfidence, out features);
        }

        public static bool TryExtractFrameFeatures(CustomGestureFrameSample frame, float minimumConfidence, out CustomGestureFrameFeatures features)
        {
            features = default;
            if (frame == null || !TryExtract(frame, minimumConfidence, out var normalized))
            {
                return false;
            }

            features = new CustomGestureFrameFeatures
            {
                NormalizedLandmarks = normalized,
                ThumbIndexDistance = NormalizedDistance(normalized, ThumbTipIndex, IndexTipIndex),
                ThumbMiddleDistance = NormalizedDistance(normalized, ThumbTipIndex, MiddleTipIndex),
                IndexMiddleDistance = NormalizedDistance(normalized, IndexTipIndex, MiddleTipIndex),
                IndexRingDistance = NormalizedDistance(normalized, IndexTipIndex, RingTipIndex),
                MiddleRingDistance = NormalizedDistance(normalized, MiddleTipIndex, RingTipIndex),
                ThumbCurl = NormalizedDistance(normalized, ThumbTipIndex, IndexMcpIndex),
                IndexCurl = FingerCurl(normalized, IndexTipIndex, IndexPipIndex, IndexMcpIndex),
                MiddleCurl = FingerCurl(normalized, MiddleTipIndex, MiddlePipIndex, MiddleMcpIndex),
                RingCurl = FingerCurl(normalized, RingTipIndex, RingPipIndex, RingMcpIndex),
                PinkyCurl = FingerCurl(normalized, PinkyTipIndex, PinkyPipIndex, PinkyMcpIndex)
            };
            return true;
        }

        public static bool TryExtractSequenceFeatures(IReadOnlyList<CustomGestureFrameSample> frames, float minimumConfidence, int fingerAIndex, int fingerBIndex, out CustomGestureSequenceFeatures features)
        {
            features = default;
            if (frames == null || frames.Count < 2)
            {
                return false;
            }

            var firstFrame = default(CustomGestureFrameSample);
            var lastFrame = default(CustomGestureFrameSample);
            var firstFeatures = default(CustomGestureFrameFeatures);
            var lastFeatures = default(CustomGestureFrameFeatures);
            var previousFrame = default(CustomGestureFrameSample);
            var previousFeatures = default(CustomGestureFrameFeatures);
            var previousFingerDistance = 0f;
            var previousFingerDelta = 0f;
            var hasPrevious = false;
            var hasFirst = false;
            var palmPath = 0f;
            var featurePath = 0f;
            var fingerPath = 0f;
            var peakVelocity = 0f;
            var oscillations = 0;
            var poseCounts = new Dictionary<GestureType, int>();

            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                if (frame == null || frame.Confidence < minimumConfidence || !TryExtractFrameFeatures(frame, minimumConfidence, out var currentFeatures))
                {
                    continue;
                }

                if (!TryResolvePalm(frame, out var palm))
                {
                    continue;
                }

                if (!TryResolveFingerDistance(frame, fingerAIndex, fingerBIndex, out var fingerDistance))
                {
                    continue;
                }

                if (!hasFirst)
                {
                    firstFrame = frame;
                    firstFeatures = currentFeatures;
                    previousFingerDistance = fingerDistance;
                    hasFirst = true;
                }
                else if (hasPrevious)
                {
                    if (TryResolvePalm(previousFrame, out var previousPalm))
                    {
                        palmPath += UnityEngine.Vector2.Distance(previousPalm, palm);
                    }

                    var featureStep = Distance(previousFeatures.NormalizedLandmarks, currentFeatures.NormalizedLandmarks);
                    if (!float.IsPositiveInfinity(featureStep))
                    {
                        featurePath += featureStep;
                    }

                    var fingerDelta = fingerDistance - previousFingerDistance;
                    fingerPath += UnityEngine.Mathf.Abs(fingerDelta);
                    var dt = UnityEngine.Mathf.Max(0.0001f, frame.Time - previousFrame.Time);
                    peakVelocity = UnityEngine.Mathf.Max(peakVelocity, UnityEngine.Mathf.Abs(fingerDelta) / dt);
                    if (UnityEngine.Mathf.Abs(previousFingerDelta) > 0.0001f
                        && UnityEngine.Mathf.Abs(fingerDelta) > 0.0001f
                        && UnityEngine.Mathf.Sign(previousFingerDelta) != UnityEngine.Mathf.Sign(fingerDelta))
                    {
                        oscillations++;
                    }

                    if (UnityEngine.Mathf.Abs(fingerDelta) > 0.0001f)
                    {
                        previousFingerDelta = fingerDelta;
                    }

                    previousFingerDistance = fingerDistance;
                }

                CountPose(frame.StaticGesture, poseCounts);
                previousFrame = frame;
                previousFeatures = currentFeatures;
                lastFrame = frame;
                lastFeatures = currentFeatures;
                hasPrevious = true;
            }

            if (!hasFirst || !hasPrevious || firstFrame == lastFrame)
            {
                return false;
            }

            TryResolvePalm(firstFrame, out var firstPalm);
            TryResolvePalm(lastFrame, out var lastPalm);
            TryResolveFingerDistance(firstFrame, fingerAIndex, fingerBIndex, out var firstFingerDistance);
            TryResolveFingerDistance(lastFrame, fingerAIndex, fingerBIndex, out var lastFingerDistance);
            features = new CustomGestureSequenceFeatures
            {
                Duration = UnityEngine.Mathf.Max(0.0001f, lastFrame.Time - firstFrame.Time),
                PalmNetDistance = UnityEngine.Vector2.Distance(firstPalm, lastPalm),
                PalmPathLength = palmPath,
                FeatureNetDistance = Distance(firstFeatures.NormalizedLandmarks, lastFeatures.NormalizedLandmarks),
                FeaturePathLength = featurePath,
                SelectedFingerDistanceDelta = lastFingerDistance - firstFingerDistance,
                SelectedFingerDistancePath = fingerPath,
                SelectedFingerPeakVelocity = peakVelocity,
                OscillationCount = oscillations,
                DominantStaticPose = ResolveDominantPose(poseCounts),
                StartPose = firstFrame.StaticGesture,
                EndPose = lastFrame.StaticGesture
            };
            return true;
        }

        public static float Distance(float[] first, float[] second)
        {
            if (first == null || second == null || first.Length != second.Length || first.Length == 0)
            {
                return float.PositiveInfinity;
            }

            var sum = 0f;
            for (var index = 0; index < first.Length; index++)
            {
                var delta = first[index] - second[index];
                sum += delta * delta;
            }

            return Mathf.Sqrt(sum / first.Length);
        }

        private static float NormalizedDistance(float[] features, int firstIndex, int secondIndex)
        {
            var first = GetPoint(features, firstIndex);
            var second = GetPoint(features, secondIndex);
            return Vector2.Distance(first, second);
        }

        private static float FingerCurl(float[] features, int tipIndex, int pipIndex, int mcpIndex)
        {
            var tipToMcp = NormalizedDistance(features, tipIndex, mcpIndex);
            var pipToMcp = NormalizedDistance(features, pipIndex, mcpIndex);
            return 1f - Mathf.Clamp01(tipToMcp / Mathf.Max(0.0001f, pipToMcp * 1.8f));
        }

        private static Vector2 GetPoint(float[] features, int landmarkIndex)
        {
            var index = landmarkIndex * 2;
            return features == null || features.Length <= index + 1
                ? Vector2.zero
                : new Vector2(features[index], features[index + 1]);
        }

        private static bool TryResolvePalm(CustomGestureFrameSample frame, out Vector2 palm)
        {
            palm = frame.PalmCenter;
            if (palm != Vector2.zero)
            {
                return true;
            }

            if (frame.Landmarks == null || frame.Landmarks.Length <= PinkyMcpIndex)
            {
                return false;
            }

            palm = (frame.Landmarks[WristIndex] + frame.Landmarks[IndexMcpIndex] + frame.Landmarks[PinkyMcpIndex]) / 3f;
            return true;
        }

        private static bool TryResolveFingerDistance(CustomGestureFrameSample frame, int fingerAIndex, int fingerBIndex, out float distance)
        {
            distance = 0f;
            if (frame?.Landmarks == null || frame.Landmarks.Length <= Mathf.Max(fingerAIndex, fingerBIndex))
            {
                return false;
            }

            distance = Vector2.Distance(frame.Landmarks[fingerAIndex], frame.Landmarks[fingerBIndex]);
            return true;
        }

        private static void CountPose(GestureType pose, Dictionary<GestureType, int> counts)
        {
            if (pose == GestureType.None || pose == GestureType.Unknown)
            {
                return;
            }

            counts.TryGetValue(pose, out var count);
            counts[pose] = count + 1;
        }

        private static GestureType ResolveDominantPose(Dictionary<GestureType, int> counts)
        {
            var best = GestureType.Unknown;
            var bestCount = 0;
            foreach (var pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }

            return best;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public static class CustomGestureFeatureExtractor
    {
        public const int RequiredLandmarkCount = 21;
        public const int FeatureLength = RequiredLandmarkCount * 2;
        private const int WristIndex = 0;
        private const int IndexMcpIndex = 5;
        private const int MiddleMcpIndex = 9;
        private const int PinkyMcpIndex = 17;
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
    }
}

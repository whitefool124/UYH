using UnityEngine;

namespace SpellGuard.InputSystem
{
    public static class LegacyGestureRuntimeAdapter
    {
        public static GestureFrame BuildSingleHandFrame(
            GestureSnapshot snapshot,
            Vector2[] landmarks,
            int frameId,
            float timestamp,
            GestureSourceKind source,
            MotionGestureEvent motion,
            GestureHandedness handedness = GestureHandedness.Unknown,
            int trackId = 0)
        {
            if (!snapshot.HandPresent)
            {
                return new GestureFrame
                {
                    FrameId = frameId,
                    Timestamp = timestamp,
                    Source = source,
                    Hands = System.Array.Empty<TrackedHandState>(),
                    LatestMotion = motion
                };
            }

            var resolvedLandmarks = landmarks ?? System.Array.Empty<Vector2>();
            var palmCenter = snapshot.ViewportPosition;
            if (resolvedLandmarks.Length > 17)
            {
                palmCenter = (resolvedLandmarks[0] + resolvedLandmarks[5] + resolvedLandmarks[17]) / 3f;
            }

            return new GestureFrame
            {
                FrameId = frameId,
                Timestamp = timestamp,
                Source = source,
                LatestMotion = motion,
                Hands = new[]
                {
                    new TrackedHandState
                    {
                        TrackId = Mathf.Max(0, trackId),
                        Handedness = handedness,
                        IsTracked = true,
                        StaticGesture = snapshot.Gesture,
                        Confidence = snapshot.Confidence,
                        ViewportPosition = snapshot.ViewportPosition,
                        PalmCenter = palmCenter,
                        Landmarks = resolvedLandmarks
                    }
                }
            };
        }

        public static GestureCommand BuildCommand(GestureSnapshot snapshot, MotionGestureEvent motion)
        {
            if (motion.IsValid)
            {
                return GestureCommand.FromMotion(motion);
            }

            return GestureCommand.FromStatic(snapshot);
        }
    }
}

using System.Collections.Generic;
using SpellGuard.InputSystem;

namespace SpellGuard.Tests.PlayMode
{
    public static class GestureTestSamples
    {
        public static GestureCommand Static(GestureType gesture, float time, float confidence = 1f, GestureHandedness handedness = GestureHandedness.Unknown, int trackId = 0)
        {
            return new GestureCommand
            {
                Kind = GestureCommandKind.StaticPose,
                StaticGesture = gesture,
                MotionGesture = MotionGestureType.None,
                Confidence = confidence,
                TriggeredTime = time,
                Handedness = handedness,
                TrackId = trackId
            };
        }

        public static GestureCommand Motion(MotionGestureType gesture, float time, float confidence = 1f, GestureHandedness handedness = GestureHandedness.Unknown, int trackId = 0)
        {
            return new GestureCommand
            {
                Kind = GestureCommandKind.Motion,
                StaticGesture = GestureType.None,
                MotionGesture = gesture,
                Confidence = confidence,
                TriggeredTime = time,
                Handedness = handedness,
                TrackId = trackId
            };
        }

        public static List<GestureCommand> PointFistSnapSequence(float start = 10f)
        {
            return new List<GestureCommand>
            {
                Static(GestureType.Point, start + 0.00f),
                Static(GestureType.Fist, start + 0.18f),
                Motion(MotionGestureType.Snap, start + 0.42f)
            };
        }

        public static List<GestureCommand> OpenPalmSwipeSequence(float start = 20f)
        {
            return new List<GestureCommand>
            {
                Static(GestureType.OpenPalm, start + 0.00f),
                Motion(MotionGestureType.SwipeLeftToRight, start + 0.16f)
            };
        }

        public static List<GestureCommand> IndexBeckonSequence(float start = 30f, GestureHandedness handedness = GestureHandedness.Right, int trackId = 1)
        {
            return new List<GestureCommand>
            {
                Static(GestureType.Point, start + 0.00f, 1f, handedness, trackId),
                Motion(MotionGestureType.PointToFist, start + 0.18f, 0.92f, handedness, trackId),
                Motion(MotionGestureType.Snap, start + 0.38f, 0.94f, handedness, trackId)
            };
        }

        public static List<GestureCommand> PinkyCircleSequence(float start = 40f, GestureHandedness handedness = GestureHandedness.Right, int trackId = 1)
        {
            return new List<GestureCommand>
            {
                Static(GestureType.OpenPalm, start + 0.00f, 1f, handedness, trackId),
                Motion(MotionGestureType.SwipeLeftToRight, start + 0.14f, 0.82f, handedness, trackId),
                Motion(MotionGestureType.SwipeRightToLeft, start + 0.28f, 0.82f, handedness, trackId)
            };
        }

        public static List<GestureCommand> DualHandComboSequence(float start = 50f)
        {
            return new List<GestureCommand>
            {
                Static(GestureType.Point, start + 0.00f, 1f, GestureHandedness.Left, 0),
                Static(GestureType.OpenPalm, start + 0.05f, 1f, GestureHandedness.Right, 1),
                Motion(MotionGestureType.SwipeLeftToRight, start + 0.26f, 0.91f, GestureHandedness.Right, 1)
            };
        }
    }
}

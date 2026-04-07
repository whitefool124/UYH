using UnityEngine;

namespace SpellGuard.InputSystem
{
    public struct MotionGestureEvent
    {
        public MotionGestureType Gesture;
        public Vector2 ViewportPosition;
        public float Confidence;
        public float TriggeredTime;

        public bool IsValid => Gesture != MotionGestureType.None;

        public static MotionGestureEvent None => new MotionGestureEvent
        {
            Gesture = MotionGestureType.None,
            ViewportPosition = new Vector2(0.5f, 0.5f),
            Confidence = 0f,
            TriggeredTime = -999f
        };
    }
}

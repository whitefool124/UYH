using UnityEngine;

namespace SpellGuard.InputSystem
{
    public struct GestureSnapshot
    {
        public bool HandPresent;
        public GestureType Gesture;
        public Vector2 ViewportPosition;
        public float Confidence;

        public static GestureSnapshot Missing => new GestureSnapshot
        {
            HandPresent = false,
            Gesture = GestureType.None,
            ViewportPosition = new Vector2(0.5f, 0.5f),
            Confidence = 0f
        };
    }
}

using UnityEngine;

namespace SpellGuard.InputSystem
{
    public enum GestureSourceKind
    {
        Unknown,
        Mock,
        NativeMediapipe,
        ExternalBridge,
        Routed
    }

    public enum GestureHandedness
    {
        Unknown,
        Left,
        Right
    }

    public struct TrackedHandState
    {
        public int TrackId;
        public GestureHandedness Handedness;
        public bool IsTracked;
        public GestureType StaticGesture;
        public float Confidence;
        public Vector2 ViewportPosition;
        public Vector2 PalmCenter;
        public Vector2[] Landmarks;

        public static TrackedHandState Missing => new TrackedHandState
        {
            TrackId = -1,
            Handedness = GestureHandedness.Unknown,
            IsTracked = false,
            StaticGesture = GestureType.None,
            Confidence = 0f,
            ViewportPosition = new Vector2(0.5f, 0.5f),
            PalmCenter = new Vector2(0.5f, 0.5f),
            Landmarks = System.Array.Empty<Vector2>()
        };
    }

    public struct GestureFrame
    {
        public long FrameId;
        public float Timestamp;
        public GestureSourceKind Source;
        public TrackedHandState[] Hands;
        public MotionGestureEvent LatestMotion;

        public int HandCount => Hands?.Length ?? 0;
        public bool HasPrimaryHand => HandCount > 0 && Hands[0].IsTracked;
        public TrackedHandState PrimaryHand => HasPrimaryHand ? Hands[0] : TrackedHandState.Missing;

        public static GestureFrame Empty(GestureSourceKind source)
        {
            return new GestureFrame
            {
                FrameId = 0,
                Timestamp = 0f,
                Source = source,
                Hands = System.Array.Empty<TrackedHandState>(),
                LatestMotion = MotionGestureEvent.None
            };
        }
    }
}

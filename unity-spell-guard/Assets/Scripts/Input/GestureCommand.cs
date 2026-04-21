namespace SpellGuard.InputSystem
{
    public enum GestureCommandKind
    {
        None,
        StaticPose,
        Motion
    }

    public struct GestureCommand
    {
        public GestureCommandKind Kind;
        public GestureType StaticGesture;
        public MotionGestureType MotionGesture;
        public float Confidence;
        public float TriggeredTime;
        public GestureHandedness Handedness;
        public int TrackId;

        public bool IsValid => Kind != GestureCommandKind.None;

        public static GestureCommand None => new GestureCommand
        {
            Kind = GestureCommandKind.None,
            StaticGesture = GestureType.None,
            MotionGesture = MotionGestureType.None,
            Confidence = 0f,
            TriggeredTime = -999f,
            Handedness = GestureHandedness.Unknown,
            TrackId = -1
        };

        public static GestureCommand FromStatic(GestureSnapshot snapshot)
        {
            if (!snapshot.HandPresent || snapshot.Gesture == GestureType.None || snapshot.Gesture == GestureType.Unknown)
            {
                return None;
            }

            return new GestureCommand
            {
                Kind = GestureCommandKind.StaticPose,
                StaticGesture = snapshot.Gesture,
                MotionGesture = MotionGestureType.None,
                Confidence = snapshot.Confidence,
                TriggeredTime = UnityEngine.Time.time,
                Handedness = GestureHandedness.Unknown,
                TrackId = -1
            };
        }

        public static GestureCommand FromMotion(MotionGestureEvent motion)
        {
            if (!motion.IsValid)
            {
                return None;
            }

            return new GestureCommand
            {
                Kind = GestureCommandKind.Motion,
                StaticGesture = GestureType.None,
                MotionGesture = motion.Gesture,
                Confidence = motion.Confidence,
                TriggeredTime = motion.TriggeredTime,
                Handedness = GestureHandedness.Unknown,
                TrackId = -1
            };
        }
    }
}

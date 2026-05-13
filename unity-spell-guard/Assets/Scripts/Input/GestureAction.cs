namespace SpellGuard.InputSystem
{
    public struct GestureAction
    {
        public GestureIntent Intent;
        public float Confidence;
        public float TriggeredTime;
        public GestureCommandKind SourceKind;
        public GestureHandedness Handedness;
        public int TrackId;

        public bool IsValid => Intent != GestureIntent.None;
        public bool IsTransient => SourceKind == GestureCommandKind.Motion;

        public static GestureAction None => new GestureAction
        {
            Intent = GestureIntent.None,
            Confidence = 0f,
            TriggeredTime = -999f,
            SourceKind = GestureCommandKind.None,
            Handedness = GestureHandedness.Unknown,
            TrackId = -1
        };

        public static GestureAction FromCommand(GestureIntent intent, GestureCommand command)
        {
            if (intent == GestureIntent.None || !command.IsValid)
            {
                return None;
            }

            return new GestureAction
            {
                Intent = intent,
                Confidence = command.Confidence,
                TriggeredTime = command.TriggeredTime,
                SourceKind = command.Kind,
                Handedness = command.Handedness,
                TrackId = command.TrackId
            };
        }

        public static GestureAction FromMotion(GestureIntent intent, MotionGestureEvent motion)
        {
            if (intent == GestureIntent.None || !motion.IsValid)
            {
                return None;
            }

            return new GestureAction
            {
                Intent = intent,
                Confidence = motion.Confidence,
                TriggeredTime = motion.TriggeredTime,
                SourceKind = GestureCommandKind.Motion,
                Handedness = GestureHandedness.Unknown,
                TrackId = -1
            };
        }
    }
}

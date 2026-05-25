namespace SpellGuard.InputSystem
{
    public static class CustomGestureDynamicPatternUtility
    {
        public static CustomGestureDynamicPattern Normalize(CustomGestureDynamicPattern pattern)
        {
            return pattern switch
            {
                CustomGestureDynamicPattern.Directional => CustomGestureDynamicPattern.PalmTrajectory,
                CustomGestureDynamicPattern.FingerSpread => CustomGestureDynamicPattern.FingerDistanceChange,
                _ => pattern
            };
        }

        public static bool IsPalmTrajectory(CustomGestureDynamicPattern pattern)
        {
            var normalized = Normalize(pattern);
            return normalized == CustomGestureDynamicPattern.PalmTrajectory;
        }

        public static bool UsesPalmTrajectoryTemplate(CustomGestureDynamicPattern pattern)
        {
            var normalized = Normalize(pattern);
            return normalized == CustomGestureDynamicPattern.PalmTrajectory
                   || normalized == CustomGestureDynamicPattern.Repeat
                   || normalized == CustomGestureDynamicPattern.Loop;
        }

        public static bool IsDynamicRulePattern(CustomGestureDynamicPattern pattern)
        {
            var normalized = Normalize(pattern);
            return normalized == CustomGestureDynamicPattern.PalmTrajectory
                   || normalized == CustomGestureDynamicPattern.Repeat
                   || normalized == CustomGestureDynamicPattern.Loop
                   || normalized == CustomGestureDynamicPattern.FingerDistanceChange
                   || normalized == CustomGestureDynamicPattern.FingerOscillation
                   || normalized == CustomGestureDynamicPattern.PoseTransition
                   || normalized == CustomGestureDynamicPattern.FeatureSequence;
        }

        public static bool IsFingerDistanceChange(CustomGestureDynamicPattern pattern)
        {
            var normalized = Normalize(pattern);
            return normalized == CustomGestureDynamicPattern.FingerDistanceChange;
        }

        public static bool IsFeatureSequence(CustomGestureDynamicPattern pattern)
        {
            var normalized = Normalize(pattern);
            return normalized == CustomGestureDynamicPattern.FeatureSequence;
        }
    }
}

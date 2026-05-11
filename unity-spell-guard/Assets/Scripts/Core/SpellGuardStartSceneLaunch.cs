namespace SpellGuard.Core
{
    public enum SpellGuardStartSceneLaunchMode
    {
        None,
        Combat,
        Training
    }

    public static class SpellGuardStartSceneLaunch
    {
        private static SpellGuardStartSceneLaunchMode pendingMode = SpellGuardStartSceneLaunchMode.None;
        private static bool returnToStartScene;

        public static SpellGuardStartSceneLaunchMode PendingMode => pendingMode;
        public static bool ShouldReturnToStartScene => returnToStartScene;

        public static void Request(SpellGuardStartSceneLaunchMode mode)
        {
            pendingMode = mode;
            returnToStartScene = true;
        }

        public static SpellGuardStartSceneLaunchMode ConsumePendingMode()
        {
            var mode = pendingMode;
            pendingMode = SpellGuardStartSceneLaunchMode.None;
            return mode;
        }

        public static void ClearReturnTarget()
        {
            returnToStartScene = false;
            pendingMode = SpellGuardStartSceneLaunchMode.None;
        }
    }
}

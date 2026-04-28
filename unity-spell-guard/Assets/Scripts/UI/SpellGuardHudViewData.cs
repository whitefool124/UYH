namespace SpellGuard.UI
{
    public readonly struct SpellGuardHudViewData
    {
        public SpellGuardHudViewData(
            string screenLabel,
            string inputModeLabel,
            string motionCaptureSignal,
            string healthText,
            string shieldText,
            string enemyText,
            string motionGestureLabel,
            int poseLandmarkCount)
        {
            ScreenLabel = screenLabel;
            InputModeLabel = inputModeLabel;
            MotionCaptureSignal = motionCaptureSignal;
            HealthText = healthText;
            ShieldText = shieldText;
            EnemyText = enemyText;
            MotionGestureLabel = motionGestureLabel;
            PoseLandmarkCount = poseLandmarkCount;
        }

        public string ScreenLabel { get; }
        public string InputModeLabel { get; }
        public string MotionCaptureSignal { get; }
        public string HealthText { get; }
        public string ShieldText { get; }
        public string EnemyText { get; }
        public string MotionGestureLabel { get; }
        public int PoseLandmarkCount { get; }
    }
}

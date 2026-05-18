using SpellGuard.Combat;
using SpellGuard.Core;

namespace SpellGuard.UI
{
    public readonly struct SpellGuardFlowViewData
    {
        public SpellGuardFlowViewData(
            SpellGuardScreen screen,
            string hintText,
            float trainingMenuHoldSeconds,
            string confirmLabel,
            string difficultyLabel,
            int combatScore,
            int combatHits,
            int combatCasts,
            int combatFireCasts,
            int combatIceCasts,
            int combatShieldCasts,
            int trainingCasts,
            int trainingPointerChecks,
            int trainingFireCasts,
            int trainingIceCasts,
            int trainingShieldCasts,
            int trainingSwipeCommands,
            int trainingSpecialCommands,
            SpellType lastTrainingSpell,
            TrainingGestureStep trainingStep,
            string trainingStepLabel,
            string trainingStepFeedback,
            int hitRate,
            SpellGuardRunResult runResult,
            int targetScoreToWin,
            int bestScore,
            bool tutorialSeen,
            bool trainingComplete,
            bool developerToolsEnabled,
            string customGestureDisplayName,
            string customGestureTemplateName,
            string customGestureKindLabel,
            string customGestureTargetLabel,
            string customGestureStatusText,
            int customGestureSampleCount,
            int customGestureRequiredSamples,
            bool customGestureRecording,
            string customGestureLastMatchedName,
            float customGestureLastScore,
            int customGestureTemplateCount,
            bool customGestureValidationActive,
            string customGestureValidationTargetLabel,
            string customGestureValidationStatusText)
        {
            Screen = screen;
            HintText = hintText;
            TrainingMenuHoldSeconds = trainingMenuHoldSeconds;
            ConfirmLabel = confirmLabel;
            DifficultyLabel = difficultyLabel;
            CombatScore = combatScore;
            CombatHits = combatHits;
            CombatCasts = combatCasts;
            CombatFireCasts = combatFireCasts;
            CombatIceCasts = combatIceCasts;
            CombatShieldCasts = combatShieldCasts;
            TrainingCasts = trainingCasts;
            TrainingPointerChecks = trainingPointerChecks;
            TrainingFireCasts = trainingFireCasts;
            TrainingIceCasts = trainingIceCasts;
            TrainingShieldCasts = trainingShieldCasts;
            TrainingSwipeCommands = trainingSwipeCommands;
            TrainingSpecialCommands = trainingSpecialCommands;
            LastTrainingSpell = lastTrainingSpell;
            TrainingStep = trainingStep;
            TrainingStepLabel = trainingStepLabel;
            TrainingStepFeedback = trainingStepFeedback;
            HitRate = hitRate;
            RunResult = runResult;
            TargetScoreToWin = targetScoreToWin;
            BestScore = bestScore;
            TutorialSeen = tutorialSeen;
            TrainingComplete = trainingComplete;
            DeveloperToolsEnabled = developerToolsEnabled;
            CustomGestureDisplayName = customGestureDisplayName;
            CustomGestureTemplateName = customGestureTemplateName;
            CustomGestureKindLabel = customGestureKindLabel;
            CustomGestureTargetLabel = customGestureTargetLabel;
            CustomGestureStatusText = customGestureStatusText;
            CustomGestureSampleCount = customGestureSampleCount;
            CustomGestureRequiredSamples = customGestureRequiredSamples;
            CustomGestureRecording = customGestureRecording;
            CustomGestureLastMatchedName = customGestureLastMatchedName;
            CustomGestureLastScore = customGestureLastScore;
            CustomGestureTemplateCount = customGestureTemplateCount;
            CustomGestureValidationActive = customGestureValidationActive;
            CustomGestureValidationTargetLabel = customGestureValidationTargetLabel;
            CustomGestureValidationStatusText = customGestureValidationStatusText;
        }

        public SpellGuardScreen Screen { get; }
        public string HintText { get; }
        public float TrainingMenuHoldSeconds { get; }
        public string ConfirmLabel { get; }
        public string DifficultyLabel { get; }
        public int CombatScore { get; }
        public int CombatHits { get; }
        public int CombatCasts { get; }
        public int CombatFireCasts { get; }
        public int CombatIceCasts { get; }
        public int CombatShieldCasts { get; }
        public int TrainingCasts { get; }
        public int TrainingPointerChecks { get; }
        public int TrainingFireCasts { get; }
        public int TrainingIceCasts { get; }
        public int TrainingShieldCasts { get; }
        public int TrainingSwipeCommands { get; }
        public int TrainingSpecialCommands { get; }
        public SpellType LastTrainingSpell { get; }
        public TrainingGestureStep TrainingStep { get; }
        public string TrainingStepLabel { get; }
        public string TrainingStepFeedback { get; }
        public int HitRate { get; }
        public SpellGuardRunResult RunResult { get; }
        public int TargetScoreToWin { get; }
        public int BestScore { get; }
        public bool TutorialSeen { get; }
        public bool TrainingComplete { get; }
        public bool DeveloperToolsEnabled { get; }
        public string CustomGestureDisplayName { get; }
        public string CustomGestureTemplateName { get; }
        public string CustomGestureKindLabel { get; }
        public string CustomGestureTargetLabel { get; }
        public string CustomGestureStatusText { get; }
        public int CustomGestureSampleCount { get; }
        public int CustomGestureRequiredSamples { get; }
        public bool CustomGestureRecording { get; }
        public string CustomGestureLastMatchedName { get; }
        public float CustomGestureLastScore { get; }
        public int CustomGestureTemplateCount { get; }
        public bool CustomGestureValidationActive { get; }
        public string CustomGestureValidationTargetLabel { get; }
        public string CustomGestureValidationStatusText { get; }
    }
}

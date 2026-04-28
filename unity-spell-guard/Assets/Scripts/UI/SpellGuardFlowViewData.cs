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
            int trainingCasts,
            int trainingPointerChecks,
            int trainingFireCasts,
            int trainingIceCasts,
            int trainingShieldCasts,
            SpellType lastTrainingSpell,
            int hitRate)
        {
            Screen = screen;
            HintText = hintText;
            TrainingMenuHoldSeconds = trainingMenuHoldSeconds;
            ConfirmLabel = confirmLabel;
            DifficultyLabel = difficultyLabel;
            CombatScore = combatScore;
            CombatHits = combatHits;
            CombatCasts = combatCasts;
            TrainingCasts = trainingCasts;
            TrainingPointerChecks = trainingPointerChecks;
            TrainingFireCasts = trainingFireCasts;
            TrainingIceCasts = trainingIceCasts;
            TrainingShieldCasts = trainingShieldCasts;
            LastTrainingSpell = lastTrainingSpell;
            HitRate = hitRate;
        }

        public SpellGuardScreen Screen { get; }
        public string HintText { get; }
        public float TrainingMenuHoldSeconds { get; }
        public string ConfirmLabel { get; }
        public string DifficultyLabel { get; }
        public int CombatScore { get; }
        public int CombatHits { get; }
        public int CombatCasts { get; }
        public int TrainingCasts { get; }
        public int TrainingPointerChecks { get; }
        public int TrainingFireCasts { get; }
        public int TrainingIceCasts { get; }
        public int TrainingShieldCasts { get; }
        public SpellType LastTrainingSpell { get; }
        public int HitRate { get; }
    }
}

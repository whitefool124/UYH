using SpellGuard.Combat;
using SpellGuard.Player;
using SpellGuard.UI;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardFlowController : MonoBehaviour
    {
        [SerializeField] private SpellGuardGameSettings settings;
        [SerializeField] private FpsGestureMotor motor;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private GameFlowManager gameFlow;
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private float trainingMenuHoldSeconds = 1.6f;

        private SpellGuardScreen screen = SpellGuardScreen.Menu;
        private int combatScore;
        private int combatHits;
        private int combatCasts;
        private int trainingCasts;
        private int trainingPointerChecks;
        private int trainingFireCasts;
        private int trainingIceCasts;
        private int trainingShieldCasts;
        private SpellType lastTrainingSpell = SpellType.None;
        private bool subscribed;

        public SpellGuardScreen Screen => screen;
        public string HintText { get; private set; } = "菜单指令：食指指向移动焦点，停留确认。";
        public float TrainingMenuHoldSeconds => trainingMenuHoldSeconds;
        public int CombatScore => combatScore;
        public int CombatHits => combatHits;
        public int CombatCasts => combatCasts;
        public int TrainingCasts => trainingCasts;
        public int TrainingPointerChecks => trainingPointerChecks;
        public int TrainingFireCasts => trainingFireCasts;
        public int TrainingIceCasts => trainingIceCasts;
        public int TrainingShieldCasts => trainingShieldCasts;
        public SpellType LastTrainingSpell => lastTrainingSpell;
        public string ConfirmLabel => settings != null ? settings.ConfirmLabel : "未绑定";
        public string DifficultyLabel => settings != null ? settings.DifficultyLabel : "未绑定";

        public SpellGuardFlowViewData GetViewData()
        {
            return new SpellGuardFlowViewData(
                screen,
                HintText,
                trainingMenuHoldSeconds,
                ConfirmLabel,
                DifficultyLabel,
                combatScore,
                combatHits,
                combatCasts,
                trainingCasts,
                trainingPointerChecks,
                trainingFireCasts,
                trainingIceCasts,
                trainingShieldCasts,
                lastTrainingSpell,
                GetHitRate());
        }

        private void OnEnable()
        {
            RefreshSpellCasterSubscription();
        }

        private void OnDisable()
        {
            if (spellCaster != null && subscribed)
            {
                spellCaster.SpellResolved -= HandleSpellResolved;
                subscribed = false;
            }
        }

        private void Start()
        {
            ReturnToMenu();
        }

        private void Update()
        {
            ApplyModeState();

            if (screen == SpellGuardScreen.Playing && gameFlow != null && gameFlow.GameOver)
            {
                screen = SpellGuardScreen.Results;
                HintText = "结果页指令：食指指向按钮并停留确认。";
            }
        }

        public void OpenSettings()
        {
            screen = SpellGuardScreen.Settings;
            HintText = "设置指令：指向条目并停留切换。";
        }

        public void OpenTutorial()
        {
            screen = SpellGuardScreen.Tutorial;
            HintText = "教程指令：阅读后可开始守卫或进入训练场。";
        }

        public void CycleConfirmSetting()
        {
            settings?.CycleConfirm();
            HintText = $"设置已切换：施法确认 {settings?.ConfirmLabel}";
            LogFlowEvent("cycle confirm setting");
        }

        public void CycleDifficultySetting()
        {
            settings?.CycleDifficulty();
            HintText = $"设置已切换：敌人节奏 {settings?.DifficultyLabel}";
            LogFlowEvent("cycle difficulty setting");
        }

        public void RecordTrainingPointerCheck()
        {
            trainingPointerChecks += 1;
        }

        public void ResetTrainingStats()
        {
            trainingCasts = 0;
            trainingPointerChecks = 0;
            trainingFireCasts = 0;
            trainingIceCasts = 0;
            trainingShieldCasts = 0;
            lastTrainingSpell = SpellType.None;
        }

        public void StartRun()
        {
            ResetCombatStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            if (settings != null)
            {
                enemySpawner?.ApplySettings(settings.Difficulty);
            }

            ResetPlayerPose();
            screen = SpellGuardScreen.Playing;
            HintText = "战斗指令：Point转向并抬高手位前进；握拳/V/张掌或打响指/左右扇手施法。";
        }

        public void StartTraining()
        {
            ResetTrainingStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            ResetPlayerPose();
            screen = SpellGuardScreen.Training;
            HintText = $"训练指令：可用握拳/V/张掌，也可用打响指/左右扇手直接施法；返回主菜单需停留 {trainingMenuHoldSeconds:F1} 秒。";
        }

        public void ReturnToMenu()
        {
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            screen = SpellGuardScreen.Menu;
            HintText = "菜单指令：食指指向移动焦点，停留确认。";
        }

        private void ApplyModeState()
        {
            var interactive3D = screen == SpellGuardScreen.Playing || screen == SpellGuardScreen.Training;
            motor?.SetInputEnabled(interactive3D);

            if (spellCaster != null)
            {
                spellCaster.SetCastingEnabled(interactive3D);
                if (settings != null)
                {
                    spellCaster.SetConfirmSeconds(settings.ConfirmSeconds);
                }
            }

            enemySpawner?.SetSpawningEnabled(screen == SpellGuardScreen.Playing);
        }

        private void HandleSpellResolved(SpellType spell, int hitCount)
        {
            if (screen == SpellGuardScreen.Playing)
            {
                combatCasts += 1;
                combatHits += hitCount;
                combatScore += hitCount;
                return;
            }

            if (screen == SpellGuardScreen.Training)
            {
                trainingCasts += 1;
                lastTrainingSpell = spell;
                if (spell == SpellType.Fire) trainingFireCasts += 1;
                else if (spell == SpellType.Ice) trainingIceCasts += 1;
                else if (spell == SpellType.Shield) trainingShieldCasts += 1;
            }
        }

        private void RefreshSpellCasterSubscription()
        {
            if (spellCaster == null || subscribed)
            {
                return;
            }

            spellCaster.SpellResolved += HandleSpellResolved;
            subscribed = true;
        }

        private void ResetCombatStats()
        {
            combatScore = 0;
            combatHits = 0;
            combatCasts = 0;
        }

        private void ResetPlayerPose()
        {
            if (playerRoot == null)
            {
                return;
            }

            playerRoot.position = new Vector3(0f, 1.1f, 0f);
            playerRoot.rotation = Quaternion.identity;
        }

        private void LogFlowEvent(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[Gesture][FlowReaction] {message}", this);
        }

        public int GetHitRate()
        {
            if (combatCasts <= 0)
            {
                return 0;
            }

            return Mathf.RoundToInt(combatHits / (float)combatCasts * 100f);
        }

    }
}

using SpellGuard.Combat;
using SpellGuard.Audio;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using SpellGuard.UI;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellGuard.Core
{
    public class SpellGuardFlowController : MonoBehaviour
    {
        [SerializeField] private SpellGuardGameSettings settings;
        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private FpsGestureMotor motor;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private GameFlowManager gameFlow;
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private float trainingMenuHoldSeconds = 1.6f;
        [SerializeField] private KeyCode pauseToggleKey = KeyCode.Escape;
        [SerializeField] private string startSceneName = "SpellGuardStart";

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
        private int bestScore;
        private bool tutorialSeen;

        public SpellGuardRunResult CurrentRunResult => gameFlow != null ? gameFlow.RunResult : SpellGuardRunResult.None;
        public int TargetScoreToWin => gameFlow != null ? gameFlow.TargetScoreToWin : 0;
        public int BestScore => bestScore;
        public bool TutorialSeen => tutorialSeen;

        public SpellGuardScreen Screen => screen;
        public string HintText { get; private set; } = "菜单：鼠标可直接点击按钮；手势仍可用食指停留确认。";
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
        public bool TrainingComplete => trainingPointerChecks > 0 && trainingFireCasts > 0 && trainingIceCasts > 0 && trainingShieldCasts > 0;
        public string ConfirmLabel => settings != null ? settings.ConfirmLabel : "未绑定";
        public string DifficultyLabel => settings != null ? settings.DifficultyLabel : "未绑定";
        public string MusicVolumeLabel => settings != null ? settings.MusicVolumeLabel : "未绑定";
        public string SfxVolumeLabel => settings != null ? settings.SfxVolumeLabel : "未绑定";
        public string InputModeLabel => inputRouter != null ? FormatInputMode(inputRouter.Mode) : settings != null ? settings.InputModeLabel : "未绑定";
        public event Action<SpellType, int, SpellGuardScreen> SpellResolvedForDiagnostics;

        public SpellGuardRuntimeStatus GetScreenStatus()
        {
            return screen switch
            {
                SpellGuardScreen.Menu => new SpellGuardRuntimeStatus("主菜单", "先看教程，或进入训练场热身，再开始守卫"),
                SpellGuardScreen.Settings => new SpellGuardRuntimeStatus("设置", $"输入模式：{InputModeLabel} | 结印确认：{ConfirmLabel} | 敌人节奏：{DifficultyLabel}"),
                SpellGuardScreen.Tutorial => new SpellGuardRuntimeStatus("上手教程", "先理解流程，再进入训练场或直接开始战斗"),
                SpellGuardScreen.Training => new SpellGuardRuntimeStatus("训练场", "练习位移、施法与返回菜单"),
                SpellGuardScreen.Playing => new SpellGuardRuntimeStatus("战斗中", $"推进、施法、换位并完成 {TargetScoreToWin} 分防守目标"),
                SpellGuardScreen.Paused => new SpellGuardRuntimeStatus("战斗暂停", "暂停中：可继续、重开本局，或返回主菜单"),
                SpellGuardScreen.Results => new SpellGuardRuntimeStatus(GetResultTitle(), $"得分：{CombatScore} | 命中率：{GetHitRate()}%"),
                _ => new SpellGuardRuntimeStatus("未绑定", "无可用流程状态")
            };
        }

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
                GetHitRate(),
                CurrentRunResult,
                TargetScoreToWin,
                bestScore,
                tutorialSeen,
                TrainingComplete);
        }

        private void OnEnable()
        {
            RefreshSpellCasterSubscription();
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;

            if (spellCaster != null && subscribed)
            {
                spellCaster.SpellResolved -= HandleSpellResolved;
                subscribed = false;
            }
        }

        private void Start()
        {
            LoadLocalProgress();
            var launchMode = SpellGuardStartSceneLaunch.ConsumePendingMode();
            if (launchMode == SpellGuardStartSceneLaunchMode.Training)
            {
                StartTraining();
            }
            else if (launchMode == SpellGuardStartSceneLaunchMode.Combat)
            {
                StartRun();
            }
            else
            {
                ReturnToMenu();
            }
        }

        private void Update()
        {
            HandlePauseToggle();
            ApplyModeState();

            if (screen == SpellGuardScreen.Playing && gameFlow != null && gameFlow.GameOver)
            {
                Time.timeScale = 1f;
                UpdateBestScore(combatScore);
                screen = SpellGuardScreen.Results;
                if (gameFlow.RunResult == SpellGuardRunResult.Victory)
                {
                    SpellGuardAudioController.Instance?.PlayVictorySfx();
                }
                else
                {
                    SpellGuardAudioController.Instance?.PlayDefeatSfx();
                }

                SpellGuardAudioController.Instance?.PlayMenuMusic();
                HintText = BuildResultsHint(gameFlow.RunResult, combatScore >= bestScore);
            }
        }

        public void OpenSettings()
        {
            screen = SpellGuardScreen.Settings;
            HintText = "设置：鼠标点击或手势停留都可以切换。";
        }

        public void OpenTutorial()
        {
            MarkTutorialSeen();
            screen = SpellGuardScreen.Tutorial;
            HintText = "教程：先看流程，再进入训练场或开始守卫。";
        }

        public void CycleConfirmSetting()
        {
            settings?.CycleConfirm();
            HintText = $"设置已切换：施法确认 {settings?.ConfirmLabel}";
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            LogFlowEvent("cycle confirm setting");
        }

        public void CycleDifficultySetting()
        {
            settings?.CycleDifficulty();
            HintText = $"设置已切换：敌人节奏 {settings?.DifficultyLabel}";
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            LogFlowEvent("cycle difficulty setting");
        }

        public void CycleInputModeSetting()
        {
            var nextMode = settings != null ? settings.CycleInputMode() : GetNextInputMode(inputRouter != null ? inputRouter.Mode : GestureInputRouter.InputMode.Mock);
            inputRouter?.SetMode(nextMode);
            HintText = $"设置已切换：输入模式 {FormatInputMode(nextMode)}";
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            LogFlowEvent("cycle input mode setting");
        }

        public void CycleMusicVolumeSetting()
        {
            settings?.CycleMusicVolume();
            SpellGuardAudioController.Instance?.ApplySettings(settings);
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            HintText = $"设置已切换：音乐音量 {settings?.MusicVolumeLabel}";
            LogFlowEvent("cycle music volume setting");
        }

        public void CycleSfxVolumeSetting()
        {
            settings?.CycleSfxVolume();
            SpellGuardAudioController.Instance?.ApplySettings(settings);
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            HintText = $"设置已切换：音效音量 {settings?.SfxVolumeLabel}";
            LogFlowEvent("cycle sfx volume setting");
        }

        public void RecordTrainingPointerCheck()
        {
            trainingPointerChecks += 1;
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
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
            Time.timeScale = 1f;
            ResetCombatStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            if (settings != null)
            {
                enemySpawner?.ApplySettings(settings.Difficulty);
                SpellGuardAudioController.Instance?.ApplySettings(settings);
            }

            ResetPlayerPose();
            screen = SpellGuardScreen.Playing;
            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.PlayCombatMusic();
            HintText = $"战斗：左右/上下摆手位移，握拳/V/张掌/打响指施法，先拿到 {TargetScoreToWin} 分即胜利。";
        }

        public void StartTraining()
        {
            Time.timeScale = 1f;
            ResetTrainingStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            ResetPlayerPose();
            screen = SpellGuardScreen.Training;
            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.PlayMenuMusic();
            HintText = "训练：完成指向确认和火/冰/盾三法术后，可直接进入正式守卫。";
        }

        public void StartRunFromTraining()
        {
            if (!TrainingComplete)
            {
                HintText = "训练目标未完成：至少做一次指向确认，并各释放一次火焰、冰霜和护盾。";
                SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
                return;
            }

            StartRun();
            HintText = $"训练完成，进入正式守卫：目标 {TargetScoreToWin} 分。";
            LogFlowEvent("start run from completed training");
        }

        public void ReturnToMenu()
        {
            if (SpellGuardStartSceneLaunch.ShouldReturnToStartScene && !string.IsNullOrWhiteSpace(startSceneName))
            {
                Time.timeScale = 1f;
                enemySpawner?.ClearAll();
                gameFlow?.ResetGameOver();
                inputProvider?.ClearTransientInputs();
                SpellGuardStartSceneLaunch.ClearReturnTarget();
                SceneManager.LoadScene(startSceneName);
                return;
            }

            Time.timeScale = 1f;
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            screen = SpellGuardScreen.Menu;
            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.PlayMenuMusic();
            HintText = tutorialSeen
                ? "菜单：可直接开始守卫，也可以进入训练场或调整设置。"
                : "菜单：建议先看教程，再进入训练场热身后开始守卫。";
        }

        public void PauseRun()
        {
            if (screen != SpellGuardScreen.Playing || (gameFlow != null && gameFlow.GameOver))
            {
                return;
            }

            Time.timeScale = 0f;
            screen = SpellGuardScreen.Paused;
            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.PauseMusic();
            HintText = "暂停：继续当前战斗、重开本局，或返回主菜单。";
            LogFlowEvent("pause run");
        }

        public void ResumeRun()
        {
            if (screen != SpellGuardScreen.Paused)
            {
                return;
            }

            Time.timeScale = 1f;
            screen = SpellGuardScreen.Playing;
            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.ResumeMusic();
            HintText = $"战斗继续：左右/上下摆手位移，握拳/V/张掌/打响指施法，目标 {TargetScoreToWin} 分。";
            LogFlowEvent("resume run");
        }

        public void RestartRun()
        {
            StartRun();
            LogFlowEvent("restart run");
        }

        private void ApplyModeState()
        {
            var interactive3D = screen == SpellGuardScreen.Playing || screen == SpellGuardScreen.Training;
            motor?.SetInputEnabled(interactive3D);
            if (!interactive3D)
            {
                inputProvider?.ClearTransientInputs();
            }

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

        private void HandlePauseToggle()
        {
            if (!Input.GetKeyDown(pauseToggleKey))
            {
                return;
            }

            if (screen == SpellGuardScreen.Playing)
            {
                PauseRun();
            }
            else if (screen == SpellGuardScreen.Paused)
            {
                ResumeRun();
            }
        }

        private void HandleSpellResolved(SpellType spell, int hitCount)
        {
            SpellResolvedForDiagnostics?.Invoke(spell, hitCount, screen);

            if (screen == SpellGuardScreen.Playing)
            {
                combatCasts += 1;
                combatHits += hitCount;
                combatScore += hitCount;
                gameFlow?.ReportCombatScore(combatScore);
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

        private void LoadLocalProgress()
        {
            bestScore = SpellGuardLocalProgress.LoadBestScore();
            tutorialSeen = SpellGuardLocalProgress.LoadTutorialSeen();
        }

        private void MarkTutorialSeen()
        {
            if (tutorialSeen)
            {
                return;
            }

            tutorialSeen = true;
            SpellGuardLocalProgress.SaveTutorialSeen(true);
        }

        private void UpdateBestScore(int score)
        {
            if (score <= bestScore)
            {
                return;
            }

            bestScore = score;
            SpellGuardLocalProgress.SaveBestScore(bestScore);
        }

        private string BuildResultsHint(SpellGuardRunResult runResult, bool isNewBestScore)
        {
            var newRecordSuffix = isNewBestScore ? " 已刷新最高分。" : string.Empty;
            return runResult == SpellGuardRunResult.Victory
                ? $"胜利：你已完成本局守卫目标，可再来一局或返回主菜单。{newRecordSuffix}"
                : $"失败：点击再来一局，或返回主菜单调整后再战。{newRecordSuffix}";
        }

        public int GetHitRate()
        {
            if (combatCasts <= 0)
            {
                return 0;
            }

            return Mathf.RoundToInt(combatHits / (float)combatCasts * 100f);
        }

        private string GetResultTitle()
        {
            return CurrentRunResult switch
            {
                SpellGuardRunResult.Victory => "战斗胜利",
                SpellGuardRunResult.Defeat => "战斗失败",
                _ => "战斗结果"
            };
        }

        private static GestureInputRouter.InputMode GetNextInputMode(GestureInputRouter.InputMode mode)
        {
            return mode switch
            {
                GestureInputRouter.InputMode.Mock => GestureInputRouter.InputMode.NativeMediapipe,
                GestureInputRouter.InputMode.NativeMediapipe => GestureInputRouter.InputMode.ExternalBridge,
                _ => GestureInputRouter.InputMode.Mock
            };
        }

        private static string FormatInputMode(GestureInputRouter.InputMode mode)
        {
            return mode switch
            {
                GestureInputRouter.InputMode.Mock => "Mock",
                GestureInputRouter.InputMode.NativeMediapipe => "Native MediaPipe",
                GestureInputRouter.InputMode.ExternalBridge => "ExternalBridge",
                _ => "Unknown"
            };
        }

    }
}

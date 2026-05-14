using SpellGuard.Combat;
using SpellGuard.Audio;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using SpellGuard.UI;
using System;
using System.Collections.Generic;
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
        [SerializeField] private LevelConfigLibrary levelConfigLibrary;
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private float trainingMenuHoldSeconds = 1.6f;
        [SerializeField] private KeyCode pauseToggleKey = KeyCode.Escape;
        [SerializeField] private string startSceneName = "SpellGuardStart";
        [SerializeField] private bool developerToolsMode;
        [Header("Custom Gesture Training")]
        [SerializeField] private float customGestureCountdownSeconds = 3f;
        [SerializeField] private float customGestureRecordSeconds = 1.2f;
        [SerializeField] private float customGestureSampleIntervalSeconds = 0.06f;
        [SerializeField] private float customGestureMinimumConfidence = 0.55f;
        [SerializeField] private int customGestureRequiredSamples = 5;

        private readonly CustomGestureRecorder customGestureRecorder = new CustomGestureRecorder();
        private readonly List<CustomGestureSample> pendingCustomGestureSamples = new List<CustomGestureSample>();
        private SpellGuardScreen screen = SpellGuardScreen.Menu;
        private int combatScore;
        private int combatHits;
        private int combatCasts;
        private int combatFireCasts;
        private int combatIceCasts;
        private int combatShieldCasts;
        private int trainingCasts;
        private int trainingPointerChecks;
        private int trainingFireCasts;
        private int trainingIceCasts;
        private int trainingShieldCasts;
        private int trainingSwipeCommands;
        private int trainingSpecialCommands;
        private SpellType lastTrainingSpell = SpellType.None;
        private TrainingGestureStep trainingStep = TrainingGestureStep.Point;
        private bool subscribed;
        private int bestScore;
        private bool tutorialSeen;
        private int customGestureSlotIndex = 1;
        private GestureHandedness customGestureTargetHandedness = GestureHandedness.Right;
        private string customGestureStatusText = "项目手势库：选择左/右手后开始录制。";

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
        public int CombatFireCasts => combatFireCasts;
        public int CombatIceCasts => combatIceCasts;
        public int CombatShieldCasts => combatShieldCasts;
        public int TrainingCasts => trainingCasts;
        public int TrainingPointerChecks => trainingPointerChecks;
        public int TrainingFireCasts => trainingFireCasts;
        public int TrainingIceCasts => trainingIceCasts;
        public int TrainingShieldCasts => trainingShieldCasts;
        public int TrainingSwipeCommands => trainingSwipeCommands;
        public int TrainingSpecialCommands => trainingSpecialCommands;
        public SpellType LastTrainingSpell => lastTrainingSpell;
        public TrainingGestureStep TrainingStep => trainingStep;
        public string TrainingStepLabel => GetTrainingStepLabel(trainingStep);
        public string TrainingStepFeedback { get; private set; } = "第 1 步：使用 Point 完成一次指向确认。";
        public bool TrainingComplete => trainingStep == TrainingGestureStep.Complete;
        public bool DeveloperToolsEnabled => developerToolsMode;
        public bool IsCustomGestureRecording => customGestureRecorder.IsBusy;
        public string ConfirmLabel => settings != null ? settings.ConfirmLabel : "未绑定";
        public string DifficultyLabel => settings != null ? settings.DifficultyLabel : "未绑定";
        public string MusicVolumeLabel => settings != null ? settings.MusicVolumeLabel : "未绑定";
        public string SfxVolumeLabel => settings != null ? settings.SfxVolumeLabel : "未绑定";
        public string InputModeLabel => inputRouter != null ? FormatInputMode(inputRouter.Mode) : settings != null ? settings.InputModeLabel : "未绑定";
        public string CustomGestureDisplayName => $"Custom {customGestureSlotIndex}";
        public string CustomGestureTargetLabel => FormatCustomGestureHandedness(customGestureTargetHandedness);
        public string CustomGestureStatusText => customGestureStatusText;
        public int CustomGestureSampleCount => pendingCustomGestureSamples.Count;
        public int CustomGestureRequiredSamples => Mathf.Max(1, customGestureRequiredSamples);
        public bool CustomGestureRecording => customGestureRecorder.IsBusy;
        public string CustomGestureLastMatchedName => inputRouter != null ? inputRouter.LastCustomGestureName : "无";
        public float CustomGestureLastScore => inputRouter != null ? inputRouter.LastCustomGestureScore : float.PositiveInfinity;
        public event Action<SpellType, int, SpellGuardScreen> SpellResolvedForDiagnostics;

        public LevelConfig CurrentLevelConfig { get; private set; }

        public SpellGuardRuntimeStatus GetScreenStatus()
        {
            return screen switch
            {
                SpellGuardScreen.Menu => new SpellGuardRuntimeStatus("主菜单", "先看教程，或进入训练场热身，再开始守卫"),
                SpellGuardScreen.Settings => new SpellGuardRuntimeStatus("设置", $"输入模式：{InputModeLabel} | 结印确认：{ConfirmLabel} | 敌人节奏：{DifficultyLabel}"),
                SpellGuardScreen.Tutorial => new SpellGuardRuntimeStatus("上手教程", "先理解流程，再进入训练场或直接开始战斗"),
                SpellGuardScreen.Training => developerToolsMode
                    ? new SpellGuardRuntimeStatus("开发者靶场", "无敌人、无限时间：录入自定义手势并观察识别历史")
                    : new SpellGuardRuntimeStatus("训练场", "练习位移、施法与返回菜单"),
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
                combatFireCasts,
                combatIceCasts,
                combatShieldCasts,
                trainingCasts,
                trainingPointerChecks,
                trainingFireCasts,
                trainingIceCasts,
                trainingShieldCasts,
                trainingSwipeCommands,
                trainingSpecialCommands,
                lastTrainingSpell,
                trainingStep,
                TrainingStepLabel,
                TrainingStepFeedback,
                GetHitRate(),
                CurrentRunResult,
                TargetScoreToWin,
                bestScore,
                tutorialSeen,
                TrainingComplete,
                developerToolsMode,
                CustomGestureDisplayName,
                CustomGestureTargetLabel,
                customGestureStatusText,
                CustomGestureSampleCount,
                CustomGestureRequiredSamples,
                CustomGestureRecording,
                CustomGestureLastMatchedName,
                CustomGestureLastScore);
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
            ConfigureCustomGestureRecorder();
            inputRouter?.ReloadCustomGestures();
            var launchMode = SpellGuardStartSceneLaunch.ConsumePendingMode();
            if (launchMode == SpellGuardStartSceneLaunchMode.Training || launchMode == SpellGuardStartSceneLaunchMode.DeveloperTools || developerToolsMode)
            {
                developerToolsMode = developerToolsMode || launchMode == SpellGuardStartSceneLaunchMode.DeveloperTools;
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
            UpdateCustomGestureRecording();

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

        public void CycleCustomGestureSlot()
        {
            customGestureSlotIndex = customGestureSlotIndex >= 3 ? 1 : customGestureSlotIndex + 1;
            pendingCustomGestureSamples.Clear();
            customGestureRecorder.Cancel();
            customGestureStatusText = $"已切换到 {CustomGestureDisplayName}，请重新录制 5 个样本。";
            HintText = customGestureStatusText;
        }

        public void CycleCustomGestureTarget()
        {
            CycleCustomGestureHandedness();
        }

        public void CycleCustomGestureHandedness()
        {
            customGestureTargetHandedness = customGestureTargetHandedness == GestureHandedness.Left ? GestureHandedness.Right : GestureHandedness.Left;
            pendingCustomGestureSamples.Clear();
            customGestureRecorder.Cancel();
            customGestureStatusText = $"录入手别已切换：{CustomGestureTargetLabel}，请重新录制样本。";
            HintText = customGestureStatusText;
        }

        public void StartCustomGestureRecording()
        {
            if (screen != SpellGuardScreen.Training)
            {
                return;
            }

            ConfigureCustomGestureRecorder();
            customGestureRecorder.SetTargetHandedness(customGestureTargetHandedness);
            inputRouter?.SetCustomGesturesEnabled(false);
            customGestureRecorder.Begin(Time.time);
            customGestureStatusText = customGestureRecorder.StatusText;
            HintText = $"保持{CustomGestureTargetLabel}完整入镜，系统会录制 1.2 秒 landmark 序列。";
            inputProvider?.ClearTransientInputs();
        }

        public void SaveCustomGestureTemplate()
        {
            if (pendingCustomGestureSamples.Count < CustomGestureRequiredSamples)
            {
                customGestureStatusText = $"样本不足：{pendingCustomGestureSamples.Count}/{CustomGestureRequiredSamples}，继续录制。";
                HintText = customGestureStatusText;
                return;
            }

            var template = new CustomGestureTemplate
            {
                GestureId = BuildCustomGestureId(),
                DisplayName = $"{CustomGestureDisplayName} {CustomGestureTargetLabel}",
                Kind = CustomGestureKind.DynamicMotion,
                RequiredHandedness = customGestureTargetHandedness,
                TargetIntent = GestureIntent.CustomGesture,
                MatchThreshold = CustomGestureRecognizer.DefaultDynamicThreshold,
                Samples = new List<CustomGestureSample>(pendingCustomGestureSamples)
            };

            inputRouter?.SaveCustomGesture(template);
            customGestureRecorder.MarkSaved();
            inputRouter?.SetCustomGesturesEnabled(true);
            customGestureStatusText = $"已保存 {template.DisplayName} 到项目手势库；该录入不绑定法术。";
            HintText = customGestureStatusText;
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
        }

        public void ReloadCustomGestureTemplates()
        {
            inputRouter?.ReloadCustomGestures();
            inputRouter?.SetCustomGesturesEnabled(true);
            customGestureStatusText = $"已重新加载模板库，最近识别：{CustomGestureLastMatchedName}";
            HintText = customGestureStatusText;
            inputProvider?.ClearTransientInputs();
        }

        public void RecordTrainingPointerCheck()
        {
            trainingPointerChecks += 1;
            CompleteTrainingStep(TrainingGestureStep.Point, "Point 指向确认完成，下一步练习 Fist 火焰术。");
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
        }

        public void ResetTrainingStats()
        {
            trainingCasts = 0;
            trainingPointerChecks = 0;
            trainingFireCasts = 0;
            trainingIceCasts = 0;
            trainingShieldCasts = 0;
            trainingSwipeCommands = 0;
            trainingSpecialCommands = 0;
            lastTrainingSpell = SpellType.None;
            trainingStep = TrainingGestureStep.Point;
            TrainingStepFeedback = "第 1 步：使用 Point 完成一次指向确认。";
        }

        public void StartRun()
        {
            Time.timeScale = 1f;
            ResetCombatStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            ApplyLevelConfig(levelConfigLibrary != null ? levelConfigLibrary.CombatLevel : null, true);
            if (settings != null)
            {
                if (CurrentLevelConfig == null)
                {
                    enemySpawner?.ApplySettings(settings.Difficulty);
                }

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
            ApplyLevelConfig(levelConfigLibrary != null ? levelConfigLibrary.TutorialLevel : null, false);
            ResetPlayerPose();
            screen = SpellGuardScreen.Training;
            inputProvider?.ClearTransientInputs();
            inputRouter?.ReloadCustomGestures();
            ConfigureCustomGestureRecorder();
            customGestureStatusText = "项目手势库：可按左/右手分别录制 5 个样本，不绑定法术。";
            SpellGuardAudioController.Instance?.PlayMenuMusic();
            HintText = developerToolsMode
                ? "开发者靶场：无敌人、无倒计时，专注录入自定义手势、测试识别历史与采集论文数据。"
                : "训练：完成基础目标；自定义手势录入仅维护项目手势库，不绑定法术。";
            if (CurrentLevelConfig != null && !string.IsNullOrWhiteSpace(CurrentLevelConfig.TutorialHint))
            {
                HintText = developerToolsMode ? HintText : CurrentLevelConfig.TutorialHint;
            }
        }

        public void StartRunFromTraining()
        {
            if (developerToolsMode)
            {
                HintText = "开发者靶场保持无限测试，不会进入正式战斗；请直接观察 HUD 识别历史或导出实验数据。";
                SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
                return;
            }

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
            var allowGameplayInput = interactive3D && !IsCustomGestureRecording;
            motor?.SetInputEnabled(allowGameplayInput);
            if (!interactive3D)
            {
                inputProvider?.ClearTransientInputs();
            }

            if (spellCaster != null)
            {
                spellCaster.SetCastingEnabled(allowGameplayInput);
                if (settings != null)
                {
                    spellCaster.SetConfirmSeconds(settings.ConfirmSeconds);
                }
            }

            inputRouter?.SetCustomGesturesEnabled(!IsCustomGestureRecording);

            var levelAllowsSpawning = CurrentLevelConfig == null || CurrentLevelConfig.SpawnEnemies;
            enemySpawner?.SetSpawningEnabled(screen == SpellGuardScreen.Playing && levelAllowsSpawning);
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
                if (spell == SpellType.Fire)
                {
                    combatFireCasts += 1;
                }
                else if (spell == SpellType.Ice)
                {
                    combatIceCasts += 1;
                }
                else if (spell == SpellType.Shield)
                {
                    combatShieldCasts += 1;
                }

                combatHits += hitCount;
                combatScore += hitCount;
                gameFlow?.ReportCombatScore(combatScore);
                return;
            }

            if (screen == SpellGuardScreen.Training)
            {
                trainingCasts += 1;
                lastTrainingSpell = spell;
                if (spell == SpellType.Fire)
                {
                    trainingFireCasts += 1;
                    CompleteTrainingStep(TrainingGestureStep.Fist, "Fist 火焰术完成，下一步练习 V Sign 冰霜术。");
                }
                else if (spell == SpellType.Ice)
                {
                    trainingIceCasts += 1;
                    CompleteTrainingStep(TrainingGestureStep.VSign, "V Sign 冰霜术完成，下一步练习 OpenPalm 护盾术。");
                }
                else if (spell == SpellType.Shield)
                {
                    trainingShieldCasts += 1;
                    CompleteTrainingStep(TrainingGestureStep.OpenPalm, "OpenPalm 护盾术完成，下一步练习 Swipe 左右移动。");
                }
            }
        }

        public void RecordTrainingAction(GestureAction action)
        {
            if (screen != SpellGuardScreen.Training)
            {
                return;
            }

            var trainingAction = GestureIntentMapper.ToTrainingAction(action);
            if (trainingAction.Intent == GestureIntent.TrainingSwipe)
            {
                trainingSwipeCommands += 1;
                CompleteTrainingStep(TrainingGestureStep.Swipe, "Swipe 移动练习完成，下一步练习 Snap 或 PointToFist 确认动作。");
            }
            else if (trainingAction.Intent == GestureIntent.TrainingSpecialConfirm)
            {
                trainingSpecialCommands += 1;
                CompleteTrainingStep(TrainingGestureStep.SnapOrPointToFist, "Snap / PointToFist 完成，训练流程已完成，可进入正式守卫。");
            }
        }

        private void ConfigureCustomGestureRecorder()
        {
            customGestureRecorder.Configure(customGestureCountdownSeconds, customGestureRecordSeconds, customGestureSampleIntervalSeconds, customGestureMinimumConfidence);
        }

        private void UpdateCustomGestureRecording()
        {
            if (screen != SpellGuardScreen.Training || !customGestureRecorder.IsBusy)
            {
                return;
            }

            var frame = inputProvider != null ? inputProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            var completed = customGestureRecorder.Update(frame, Time.time);
            customGestureStatusText = customGestureRecorder.StatusText;
            if (!completed || customGestureRecorder.LastSample == null)
            {
                return;
            }

            pendingCustomGestureSamples.Add(customGestureRecorder.LastSample);
            customGestureStatusText = $"已录入样本 {pendingCustomGestureSamples.Count}/{CustomGestureRequiredSamples}。";
            HintText = pendingCustomGestureSamples.Count >= CustomGestureRequiredSamples
                ? "样本已足够，点击保存模板写入项目手势库。"
                : $"样本有效，继续录制同一个{CustomGestureTargetLabel}动作。";
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
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
            combatFireCasts = 0;
            combatIceCasts = 0;
            combatShieldCasts = 0;
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

        private void CompleteTrainingStep(TrainingGestureStep expectedStep, string feedback)
        {
            if (trainingStep != expectedStep)
            {
                return;
            }

            trainingStep = expectedStep == TrainingGestureStep.SnapOrPointToFist ? TrainingGestureStep.Complete : expectedStep + 1;
            TrainingStepFeedback = feedback;
        }

        private static string GetTrainingStepLabel(TrainingGestureStep step)
        {
            return step switch
            {
                TrainingGestureStep.Point => "Point：指向确认",
                TrainingGestureStep.Fist => "Fist：火焰术",
                TrainingGestureStep.VSign => "V Sign：冰霜术",
                TrainingGestureStep.OpenPalm => "OpenPalm：护盾术",
                TrainingGestureStep.Swipe => "Swipe：左右移动",
                TrainingGestureStep.SnapOrPointToFist => "Snap / PointToFist：确认动作",
                TrainingGestureStep.Complete => "训练完成",
                _ => "训练步骤"
            };
        }

        private static string FormatCustomGestureHandedness(GestureHandedness handedness)
        {
            return handedness == GestureHandedness.Left ? "左手" : "右手";
        }

        private string BuildCustomGestureId()
        {
            var handSuffix = customGestureTargetHandedness == GestureHandedness.Left ? "left" : "right";
            return $"custom_{customGestureSlotIndex}_{handSuffix}";
        }

        private void ApplyLevelConfig(LevelConfig config, bool allowEnemySpawning)
        {
            CurrentLevelConfig = config;
            if (config == null)
            {
                return;
            }

            playerHealth?.SetMaxHealth(config.PlayerHealth);
            gameFlow?.ApplyLevelConfig(config);
            enemySpawner?.ApplyWaveConfig(config.Wave);
            enemySpawner?.SetSpawningEnabled(allowEnemySpawning && config.SpawnEnemies);
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

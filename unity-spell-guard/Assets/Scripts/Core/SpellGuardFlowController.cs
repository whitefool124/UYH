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
        [SerializeField] private bool disableGameplayGestureCommandsInDeveloperTools = true;
        [Header("Custom Gesture Training")]
        [SerializeField] private float customGestureCountdownSeconds = 3f;
        [SerializeField] private float customGestureRecordSeconds = 1.2f;
        [SerializeField] private float customGestureSampleIntervalSeconds = 0.06f;
        [SerializeField] private float customGestureMinimumConfidence = 0.55f;
        [SerializeField] private float customGestureValidationMinimumConfidence = 0.35f;
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
        private CustomGestureKind customGestureKind = CustomGestureKind.DynamicMotion;
        private SpellType customGestureTargetSpell = SpellType.Fire;
        private bool customGestureHasReviewSample;
        private int customGestureValidationTemplateIndex;
        private bool customGestureValidationActive;
        private float customGestureValidationSuccessAt = -999f;
        private string customGestureTemplateName = string.Empty;
        private string customGestureStatusText = "项目手势库：选择左/右手后开始录制。";
        private string customGestureValidationStatusText = "验证页：请先加载模板库并选择一个目标手势。";

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
        public bool DeveloperToolsGestureCommandsDisabled => developerToolsMode && disableGameplayGestureCommandsInDeveloperTools;
        public bool IsCustomGestureRecording => customGestureRecorder.IsBusy;
        public string ConfirmLabel => settings != null ? settings.ConfirmLabel : "未绑定";
        public string DifficultyLabel => settings != null ? settings.DifficultyLabel : "未绑定";
        public string MusicVolumeLabel => settings != null ? settings.MusicVolumeLabel : "未绑定";
        public string SfxVolumeLabel => settings != null ? settings.SfxVolumeLabel : "未绑定";
        public string FullscreenLabel => settings != null ? settings.FullscreenLabel : "未绑定";
        public string InputModeLabel => inputRouter != null ? FormatInputMode(inputRouter.Mode) : settings != null ? settings.InputModeLabel : "未绑定";
        public string CustomGestureDisplayName => $"Custom {customGestureSlotIndex}";
        public string CustomGestureTemplateName => customGestureTemplateName;
        public string CustomGestureKindLabel => FormatCustomGestureKind(customGestureKind);
        public string CustomGestureTargetLabel => FormatCustomGestureHandedness(customGestureTargetHandedness);
        public string CustomGestureTargetSpellLabel => FormatCustomGestureSpell(customGestureTargetSpell);
        public string CustomGestureStatusText => customGestureStatusText;
        public int CustomGestureSampleCount => pendingCustomGestureSamples.Count;
        public int CustomGestureRequiredSamples => Mathf.Max(1, customGestureRequiredSamples);
        public bool CustomGestureRecording => customGestureRecorder.IsBusy;
        public string CustomGestureLastMatchedName => inputRouter != null ? inputRouter.LastCustomGestureName : "无";
        public float CustomGestureLastScore => inputRouter != null ? inputRouter.LastCustomGestureScore : float.PositiveInfinity;
        public int CustomGestureTemplateCount => inputRouter != null ? inputRouter.CustomGestureTemplateCount : 0;
        public bool CustomGestureValidationActive => customGestureValidationActive;
        public string CustomGestureValidationTargetLabel => GetCustomGestureValidationTargetLabel();
        public string CustomGestureValidationListText => inputRouter != null ? inputRouter.GetCustomGestureTemplateListText(customGestureValidationTemplateIndex) : string.Empty;
        public string CustomGestureValidationReferenceText => BuildCustomGestureValidationReferenceText();
        public string CustomGestureSamplePreviewText => BuildCustomGestureSamplePreviewText();
        public string CustomGestureValidationStatusText => customGestureValidationStatusText;
        public float CustomGestureValidationScore => inputRouter != null ? inputRouter.LastCustomGestureValidationScore : float.PositiveInfinity;
        public float CustomGestureValidationMinimumConfidence => customGestureValidationMinimumConfidence;
        public event Action<SpellType, int, SpellGuardScreen> SpellResolvedForDiagnostics;

        public LevelConfig CurrentLevelConfig { get; private set; }

        public SpellGuardRuntimeStatus GetScreenStatus()
        {
            return screen switch
            {
                SpellGuardScreen.Menu => developerToolsMode
                    ? new SpellGuardRuntimeStatus("开发者实验室", "干净环境：三维内容隐藏，游戏手势指令禁用，只保留识别采集与自定义手势")
                    : new SpellGuardRuntimeStatus("主菜单", "先看教程，或进入训练场热身，再开始守卫"),
                SpellGuardScreen.Settings => new SpellGuardRuntimeStatus("设置", $"输入模式：{InputModeLabel} | 结印确认：{ConfirmLabel} | 敌人节奏：{DifficultyLabel}"),
                SpellGuardScreen.Tutorial => new SpellGuardRuntimeStatus("上手教程", "先理解流程，再进入训练场或直接开始战斗"),
                SpellGuardScreen.Training => developerToolsMode
                    ? new SpellGuardRuntimeStatus("开发者实验室", "游戏手势指令已禁用：只采集识别数据、录入自定义手势并导出实验结果")
                    : new SpellGuardRuntimeStatus("训练场", "练习位移、施法与返回菜单"),
                SpellGuardScreen.Playing => new SpellGuardRuntimeStatus("教学关", $"网格步进移动，使用七色火焰清除 {TargetScoreToWin} 个敌人后激活出口"),
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
                customGestureTemplateName,
                CustomGestureKindLabel,
                CustomGestureTargetLabel,
                CustomGestureTargetSpellLabel,
                customGestureStatusText,
                CustomGestureSampleCount,
                CustomGestureRequiredSamples,
                CustomGestureRecording,
                CustomGestureLastMatchedName,
                CustomGestureLastScore,
                CustomGestureTemplateCount,
                CustomGestureValidationActive,
                CustomGestureValidationTargetLabel,
                CustomGestureValidationListText,
                CustomGestureValidationStatusText,
                CustomGestureSamplePreviewText);
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
            UpdateCustomGestureValidation();

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

        public void SetInputMode(GestureInputRouter.InputMode mode)
        {
            settings?.SetInputMode(mode);
            inputRouter?.SetMode(mode);
            HintText = $"输入模式已切换：{FormatInputMode(mode)}";
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            inputProvider?.ClearTransientInputs();
            LogFlowEvent($"set input mode {mode}");
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

        public void ToggleFullscreenSetting()
        {
            if (settings == null)
            {
                return;
            }

            settings.ToggleFullscreen();
            HintText = $"设置已切换：显示模式 {FullscreenLabel}";
            LogFlowEvent("toggle fullscreen setting");
        }

        public void SetCustomGestureTemplateName(string value)
        {
            customGestureTemplateName = value ?? string.Empty;
        }

        public void CycleCustomGestureKind()
        {
            customGestureKind = customGestureKind == CustomGestureKind.StaticPose ? CustomGestureKind.DynamicMotion : CustomGestureKind.StaticPose;
            pendingCustomGestureSamples.Clear();
            customGestureHasReviewSample = false;
            customGestureRecorder.Cancel();
            customGestureStatusText = $"录制类型已切换：{CustomGestureKindLabel}。这是采集设置，不是和标准手势匹配；请重新采样。";
            HintText = customGestureStatusText;
        }

        public void CycleCustomGestureSlot()
        {
            customGestureSlotIndex = customGestureSlotIndex >= 3 ? 1 : customGestureSlotIndex + 1;
            pendingCustomGestureSamples.Clear();
            customGestureHasReviewSample = false;
            customGestureRecorder.Cancel();
            customGestureStatusText = $"已切换到 {CustomGestureDisplayName}，样本会重新开始采集。";
            HintText = customGestureStatusText;
        }

        public void CycleCustomGestureTarget()
        {
            CycleCustomGestureHandedness();
        }

        public void CycleCustomGestureTargetSpell()
        {
            customGestureTargetSpell = customGestureTargetSpell switch
            {
                SpellType.Fire => SpellType.Ice,
                SpellType.Ice => SpellType.Shield,
                _ => SpellType.Fire
            };
            customGestureStatusText = $"目标法术已切换：{CustomGestureTargetSpellLabel}。保存后会直接映射到玩法施法。";
            HintText = customGestureStatusText;
        }

        public void CycleCustomGestureHandedness()
        {
            customGestureTargetHandedness = customGestureTargetHandedness == GestureHandedness.Left ? GestureHandedness.Right : GestureHandedness.Left;
            pendingCustomGestureSamples.Clear();
            customGestureHasReviewSample = false;
            customGestureRecorder.Cancel();
            customGestureStatusText = $"采集手已切换：{CustomGestureTargetLabel}。录制时会自动读取摄像头识别出的左右手；如果识别不到会提示等待左右手识别。";
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
            var frame = inputProvider != null ? inputProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            if (!customGestureRecorder.CanBegin(frame, out var reason))
            {
                customGestureHasReviewSample = false;
                customGestureStatusText = $"无法开始采集：{reason}。这是基础采集质量检查，不是和某个手势模板打分。";
                HintText = customGestureStatusText;
                SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
                return;
            }

            customGestureHasReviewSample = false;
            inputRouter?.SetCustomGesturesEnabled(false);
            customGestureRecorder.Begin(Time.time);
            customGestureStatusText = customGestureRecorder.StatusText;
            HintText = $"录制单个未知{CustomGestureKindLabel}样本：照你的想法做动作；系统只检查是否采到手、自动识别左右手、置信度和 21 个关键点。";
            inputProvider?.ClearTransientInputs();
        }

        public void AcceptCustomGestureSample()
        {
            if (!customGestureHasReviewSample || customGestureRecorder.LastSample == null)
            {
                customGestureStatusText = "没有可采用的样本，请先录制。";
                HintText = customGestureStatusText;
                return;
            }

            pendingCustomGestureSamples.Add(customGestureRecorder.LastSample);
            customGestureHasReviewSample = false;
            inputRouter?.SetCustomGesturesEnabled(true);
            customGestureRecorder.Cancel();
            customGestureStatusText = $"已采用未知{CustomGestureKindLabel}样本 {pendingCustomGestureSamples.Count}/{CustomGestureRequiredSamples}。";
            HintText = pendingCustomGestureSamples.Count >= CustomGestureRequiredSamples ? "样本已足够：在名称输入框命名新手势，然后点击保存模板。" : "样本已采用：继续录制下一个样本；每个样本都是同一个新手势的重复示范。";
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
        }

        public void DiscardCustomGestureSample()
        {
            customGestureHasReviewSample = false;
            inputRouter?.SetCustomGesturesEnabled(true);
            customGestureRecorder.Cancel();
            customGestureStatusText = "已丢弃当前样本。下一步：重新录制同一个新手势的一次示范。";
            HintText = customGestureStatusText;
        }

        public void SaveCustomGestureTemplate()
        {
            if (pendingCustomGestureSamples.Count < CustomGestureRequiredSamples)
            {
                customGestureStatusText = $"样本不足：{pendingCustomGestureSamples.Count}/{CustomGestureRequiredSamples}，继续录制。";
                HintText = customGestureStatusText;
                return;
            }

            var displayName = customGestureTemplateName.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                customGestureStatusText = "请先在名称输入框命名这个新手势，再保存。";
                HintText = customGestureStatusText;
                return;
            }

            var template = new CustomGestureTemplate
            {
                GestureId = BuildCustomGestureId(displayName),
                DisplayName = displayName,
                Kind = customGestureKind,
                RequiredHandedness = pendingCustomGestureSamples[0].Handedness,
                TargetIntent = MapCustomGestureTargetIntent(customGestureTargetSpell),
                MatchThreshold = customGestureKind == CustomGestureKind.StaticPose ? CustomGestureRecognizer.DefaultStaticThreshold : CustomGestureRecognizer.DefaultDynamicThreshold,
                Samples = new List<CustomGestureSample>(pendingCustomGestureSamples),
                TrajectoryTemplates = customGestureKind == CustomGestureKind.DynamicMotion
                    ? CustomGestureTrajectoryTemplateBuilder.Build(pendingCustomGestureSamples)
                    : new List<CustomGestureTrajectoryTemplate>()
            };

            if (customGestureKind == CustomGestureKind.DynamicMotion && (template.TrajectoryTemplates == null || template.TrajectoryTemplates.Count == 0))
            {
                customGestureStatusText = "DTW trajectory build failed: record a clearer motion sample with visible palm movement.";
                HintText = customGestureStatusText;
                inputRouter?.SetCustomGesturesEnabled(true);
                return;
            }

            inputRouter?.SaveCustomGesture(template);
            inputRouter?.ReloadCustomGestures();
            var savedIndex = inputRouter != null ? inputRouter.GetCustomGestureTemplateIndex(template.GestureId) : -1;
            if (savedIndex >= 0)
            {
                customGestureValidationTemplateIndex = savedIndex;
            }

            customGestureHasReviewSample = false;
            customGestureRecorder.MarkSaved();
            inputRouter?.SetCustomGesturesEnabled(true);
            customGestureStatusText = $"已保存“{template.DisplayName}”到项目手势库；它现在才成为可验证的模板，不绑定法术。";
            HintText = customGestureStatusText;
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
        }

        public void ReloadCustomGestureTemplates()
        {
            inputRouter?.ReloadCustomGestures();
            inputRouter?.SetCustomGesturesEnabled(true);
            ClampCustomGestureValidationTarget();
            customGestureStatusText = $"已加载模板库。现在可以进入验证页，选择库里的目标手势持续监测；当前目标：{CustomGestureValidationTargetLabel}";
            customGestureValidationStatusText = CustomGestureTemplateCount > 0
                ? $"已加载 {CustomGestureTemplateCount} 个模板。当前验证目标：{CustomGestureValidationTargetLabel}。"
                : "模板库为空：请先录制并保存一个自定义手势。";
            HintText = customGestureStatusText;
            inputProvider?.ClearTransientInputs();
        }

        public void CycleCustomGestureValidationTarget()
        {
            ReloadCustomGestureTemplates();
            var count = CustomGestureTemplateCount;
            if (count <= 0)
            {
                customGestureValidationTemplateIndex = 0;
                customGestureValidationActive = false;
                customGestureValidationStatusText = "模板库为空：请先录制并保存一个自定义手势。";
                HintText = customGestureValidationStatusText;
                return;
            }

            customGestureValidationTemplateIndex = (customGestureValidationTemplateIndex + 1) % count;
            customGestureValidationActive = true;
            customGestureValidationSuccessAt = -999f;
            customGestureValidationStatusText = $"验证目标已切换：{CustomGestureValidationTargetLabel}。请直接做这个手势，系统会持续监测。";
            HintText = customGestureValidationStatusText;
            inputProvider?.ClearTransientInputs();
        }

        public void ToggleCustomGestureValidation()
        {
            ReloadCustomGestureTemplates();
            if (CustomGestureTemplateCount <= 0)
            {
                customGestureValidationActive = false;
                customGestureValidationStatusText = "模板库为空：请先录制并保存一个自定义手势。";
                HintText = customGestureValidationStatusText;
                return;
            }

            customGestureValidationActive = !customGestureValidationActive;
            customGestureValidationSuccessAt = -999f;
            customGestureValidationStatusText = customGestureValidationActive
                ? $"开始验证：目标是 {CustomGestureValidationTargetLabel}。请持续做这个手势，命中后会明确提示成功。"
                : $"已暂停验证：当前目标仍是 {CustomGestureValidationTargetLabel}。";
            HintText = customGestureValidationStatusText;
            inputProvider?.ClearTransientInputs();
        }

        public void StartCustomGestureValidation()
        {
            ReloadCustomGestureTemplates();
            customGestureValidationActive = CustomGestureTemplateCount > 0;
            customGestureValidationSuccessAt = -999f;
            customGestureValidationStatusText = customGestureValidationActive
                ? $"验证页已就绪：目标是 {CustomGestureValidationTargetLabel}。请做这个手势，系统会持续监测。"
                : "模板库为空：请先录制并保存一个自定义手势。";
            HintText = customGestureValidationStatusText;
            inputProvider?.ClearTransientInputs();
        }

        public void StopCustomGestureValidation()
        {
            customGestureValidationActive = false;
            customGestureValidationStatusText = "已离开验证页。";
        }

        public bool TryGetCustomGestureValidationTemplate(out CustomGestureTemplate template)
        {
            template = null;
            if (inputRouter == null)
            {
                return false;
            }

            return inputRouter.TryGetCustomGestureTemplate(customGestureValidationTemplateIndex, out template);
        }

        public void DeleteSelectedCustomGestureTemplate()
        {
            if (inputRouter == null)
            {
                customGestureValidationStatusText = "Delete failed: input router is unavailable.";
                HintText = customGestureValidationStatusText;
                return;
            }

            var label = CustomGestureValidationTargetLabel;
            if (!inputRouter.DeleteCustomGestureTemplate(customGestureValidationTemplateIndex))
            {
                customGestureValidationStatusText = "Delete failed: no selected custom gesture template.";
                HintText = customGestureValidationStatusText;
                return;
            }

            ClampCustomGestureValidationTarget();
            customGestureValidationSuccessAt = -999f;
            customGestureValidationActive = CustomGestureTemplateCount > 0;
            customGestureValidationStatusText = CustomGestureTemplateCount > 0
                ? $"Deleted {label}. Current target: {CustomGestureValidationTargetLabel}."
                : $"Deleted {label}. Custom gesture library is empty.";
            customGestureStatusText = customGestureValidationStatusText;
            HintText = customGestureValidationStatusText;
            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
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
            HintText = $"教学关：WASD 网格步进，鼠标调整视角，左键/1 释放火焰，目标 {TargetScoreToWin} 个敌人。";
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
            customGestureStatusText = "项目手势库：先选类型和采集手，再录制 5 个同一新手势样本；保存前不做匹配评分。";
            SpellGuardAudioController.Instance?.PlayMenuMusic();
            HintText = developerToolsMode
                ? "开发者实验室：网格教学关仅保留键鼠验证、摄像头识别、自定义手势录入与论文数据采集。"
                : "教学关：WASD 每次移动一格，鼠标调整视角，左键/1 释放当前七色火焰；击败敌人后按 E 激活出口。";
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
            HintText = $"教学关：WASD 网格步进，鼠标调整视角，左键/1 释放火焰，目标 {TargetScoreToWin} 个敌人。";
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
            var gameplayCommandsBlocked = DeveloperToolsGestureCommandsDisabled && screen == SpellGuardScreen.Training;
            var allowGameplayInput = interactive3D && !IsCustomGestureRecording && !gameplayCommandsBlocked;
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
            customGestureRecorder.Configure(customGestureCountdownSeconds, customGestureRecordSeconds, customGestureSampleIntervalSeconds, customGestureMinimumConfidence, customGestureKind);
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

            customGestureHasReviewSample = true;
            inputRouter?.SetCustomGesturesEnabled(true);
            customGestureStatusText = $"样本待确认：有效 {customGestureRecorder.LastSample.Frames.Count} 帧。满意请点“采用样本”，否则点“重录样本”；这不是匹配评分。";
            HintText = customGestureStatusText;
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
        }

        private void UpdateCustomGestureValidation()
        {
            if (screen != SpellGuardScreen.Training || !developerToolsMode || !customGestureValidationActive || customGestureRecorder.IsBusy)
            {
                return;
            }

            if (inputRouter == null)
            {
                customGestureValidationStatusText = "验证不可用：没有输入路由器。";
                return;
            }

            ClampCustomGestureValidationTarget();
            if (!inputRouter.TryEvaluateCustomGestureTemplate(customGestureValidationTemplateIndex, inputRouter.CurrentGestureFrame, Time.time, out var targetLabel, out var requiredHandedness, out var matched))
            {
                customGestureValidationActive = false;
                customGestureValidationStatusText = "模板库为空：请先录制并保存一个自定义手势。";
                return;
            }

            var handLabel = FormatCustomGestureHandedness(requiredHandedness);
            if (matched)
            {
                if (Time.unscaledTime - customGestureValidationSuccessAt > 0.6f)
                {
                    SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
                }

                customGestureValidationSuccessAt = Time.unscaledTime;
                customGestureValidationStatusText = $"验证成功：已做出“{targetLabel}”（{handLabel}）。继续保持会持续提示。";
                HintText = customGestureValidationStatusText;
                return;
            }

            if (Time.unscaledTime - customGestureValidationSuccessAt <= 1.2f)
            {
                return;
            }

            customGestureValidationStatusText = $"正在验证“{targetLabel}”（{handLabel}）：请做出这个目标手势，命中后会显示验证成功。";
        }

        private void ClampCustomGestureValidationTarget()
        {
            var count = CustomGestureTemplateCount;
            if (count <= 0)
            {
                customGestureValidationTemplateIndex = 0;
                return;
            }

            customGestureValidationTemplateIndex = Mathf.Clamp(customGestureValidationTemplateIndex, 0, count - 1);
        }

        private string GetCustomGestureValidationTargetLabel()
        {
            return inputRouter != null ? inputRouter.GetCustomGestureTemplateLabel(customGestureValidationTemplateIndex) : "无";
        }

        private string BuildCustomGestureValidationReferenceText()
        {
            if (inputRouter == null || !inputRouter.TryGetCustomGestureTemplate(customGestureValidationTemplateIndex, out var template) || template == null)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            builder.Append("目标说明: ");
            builder.Append(string.IsNullOrWhiteSpace(template.DisplayName) ? template.GestureId : template.DisplayName);
            builder.Append('\n');
            builder.Append("类型: ");
            builder.Append(template.Kind == CustomGestureKind.StaticPose ? "静态" : "动态");
            builder.Append(" | 目标手: ");
            builder.Append(FormatCustomGestureHandedness(template.RequiredHandedness));
            builder.Append(" | 阈值: ");
            builder.Append(template.MatchThreshold.ToString("F2"));
            builder.Append('\n');
            builder.Append("样本数: ");
            builder.Append(template.Samples != null ? template.Samples.Count : 0);
            builder.Append(" | 轨迹模板: ");
            builder.Append(template.TrajectoryTemplates != null ? template.TrajectoryTemplates.Count : 0);
            builder.Append('\n');
            builder.Append("当前验证分数: ");
            builder.Append(float.IsPositiveInfinity(CustomGestureValidationScore) ? "--" : CustomGestureValidationScore.ToString("F3"));
            builder.Append('\n');
            builder.Append("阈值: ");
            builder.Append(CustomGestureValidationMinimumConfidence.ToString("F2"));
            return builder.ToString();
        }

        private string BuildCustomGestureSamplePreviewText()
        {
            var lines = new System.Text.StringBuilder();
            lines.Append("Samples: ");
            lines.Append(CustomGestureSampleCount);
            lines.Append('/');
            lines.Append(CustomGestureRequiredSamples);
            lines.Append(" | Type: ");
            lines.Append(CustomGestureKindLabel);
            lines.Append(" | Hand: ");
            lines.Append(CustomGestureTargetLabel);

            if (customGestureRecorder.IsBusy)
            {
                lines.Append('\n');
                lines.Append(customGestureRecorder.StatusText);
            }
            else if (customGestureHasReviewSample && customGestureRecorder.LastSample != null)
            {
                lines.Append('\n');
                lines.Append("Review sample frames: ");
                lines.Append(customGestureRecorder.LastSample.Frames != null ? customGestureRecorder.LastSample.Frames.Count : 0);
                lines.Append(" | Handedness: ");
                lines.Append(FormatCustomGestureHandedness(customGestureRecorder.LastSample.Handedness));
            }
            else if (pendingCustomGestureSamples.Count > 0)
            {
                var latest = pendingCustomGestureSamples[pendingCustomGestureSamples.Count - 1];
                lines.Append('\n');
                lines.Append("Last accepted frames: ");
                lines.Append(latest.Frames != null ? latest.Frames.Count : 0);
                lines.Append(" | Handedness: ");
                lines.Append(FormatCustomGestureHandedness(latest.Handedness));
            }
            else
            {
                lines.Append('\n');
                lines.Append(customGestureStatusText);
            }

            return lines.ToString();
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
            return handedness switch
            {
                GestureHandedness.Left => "左手",
                GestureHandedness.Right => "右手",
                _ => "未知手"
            };
        }

        private static string FormatCustomGestureKind(CustomGestureKind kind)
        {
            return kind == CustomGestureKind.StaticPose ? "静态" : "动态";
        }

        private static string FormatCustomGestureSpell(SpellType spell)
        {
            return spell switch
            {
                SpellType.Fire => "火焰",
                SpellType.Ice => "冰霜",
                SpellType.Shield => "护盾",
                _ => "未知"
            };
        }

        private static GestureIntent MapCustomGestureTargetIntent(SpellType spell)
        {
            return spell switch
            {
                SpellType.Fire => GestureIntent.CastFire,
                SpellType.Ice => GestureIntent.CastIce,
                SpellType.Shield => GestureIntent.CastShield,
                _ => GestureIntent.CustomGesture
            };
        }

        private string BuildCustomGestureId(string displayName)
        {
            var handSuffix = pendingCustomGestureSamples.Count > 0 && pendingCustomGestureSamples[0].Handedness == GestureHandedness.Left ? "left" : pendingCustomGestureSamples.Count > 0 && pendingCustomGestureSamples[0].Handedness == GestureHandedness.Right ? "right" : "unknown";
            var kindSuffix = customGestureKind == CustomGestureKind.StaticPose ? "static" : "dynamic";
            var nameSuffix = SanitizeCustomGestureIdPart(displayName);
            return $"custom_{nameSuffix}_{kindSuffix}_{handSuffix}";
        }

        private static string SanitizeCustomGestureIdPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            var chars = new char[value.Length];
            var count = 0;
            for (var i = 0; i < value.Length; i += 1)
            {
                var c = char.ToLowerInvariant(value[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    chars[count++] = c;
                }
                else if (c == ' ' || c == '-' || c == '_')
                {
                    chars[count++] = '_';
                }
            }

            return count > 0 ? new string(chars, 0, count) : "gesture";
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

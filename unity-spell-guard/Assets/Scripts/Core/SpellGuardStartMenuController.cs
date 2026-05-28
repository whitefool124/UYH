using System;
using SpellGuard.Audio;
using SpellGuard.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellGuard.Core
{
    public class SpellGuardStartMenuController : MonoBehaviour
    {
        private enum StartMenuScreen
        {
            Main,
            Guide,
            Settings,
            Calibration
        }

        [Serializable]
        private struct Region
        {
            public string key;
            public Rect rect;
        }

        private struct StartLayout
        {
            public Rect SafeArea;
            public Rect HeroPanel;
            public Rect NavPanel;
            public Rect Title;
            public Rect Body;
            public Rect Hint;
            public float Scale;
            public float Padding;
            public float Gap;
        }

        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private SpellGuardGameSettings settings;
        [SerializeField] private string gameplaySceneName = "SpellGuardPrototype";
        [SerializeField] private string developerToolsSceneName = "SpellGuardDeveloperTools";
        [SerializeField] private float confirmHoldSeconds = 0.45f;
        [SerializeField] private float backHoldSeconds = 0.45f;
        [SerializeField] private bool debugLogs;

        private readonly Region[] regions = new Region[10];
        private StartMenuScreen screen = StartMenuScreen.Main;
        private int regionCount;
        private int selectedIndex;
        private string holdKey;
        private GestureIntent holdIntent = GestureIntent.None;
        private float holdStartedAt;
        private float lastHandledMotionTime = -999f;
        private string lastActivatedKey;
        private float lastActivatedAt = -999f;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle hintStyle;
        private GUIStyle buttonStyle;
        private GUIStyle panelStyle;
        private float cachedScale = -1f;

        public string GameplaySceneName => gameplaySceneName;
        public string FocusedKey => GetSelectedKey();

        private void Start()
        {
            SpellGuardStartSceneLaunch.ClearReturnTarget();
            SpellGuardAudioController.Instance?.ApplySettings(settings);
            SpellGuardAudioController.Instance?.PlayMenuMusic();
        }

        private void Update()
        {
            RebuildRegions();
            ClampSelectedIndex();
            UpdateGestureNavigation();
            UpdateKeyboardFallback();
        }

        private void OnGUI()
        {
            RebuildRegions();
            ClampSelectedIndex();
            var layout = GetLayout();
            EnsureStyles(layout.Scale);
            DrawBackground(layout);

            switch (screen)
            {
                case StartMenuScreen.Guide:
                    DrawGuide(layout);
                    break;
                case StartMenuScreen.Calibration:
                    DrawCalibration(layout);
                    break;
                case StartMenuScreen.Settings:
                    DrawSettings(layout);
                    break;
                default:
                    DrawMain(layout);
                    break;
            }

            DrawGestureStatus(layout);
        }

        public void LaunchCombat()
        {
            Launch(SpellGuardStartSceneLaunchMode.Combat);
        }

        public void LaunchDeveloperTools()
        {
            if (string.IsNullOrWhiteSpace(developerToolsSceneName))
            {
                Debug.LogError("开始场景未配置开发者场景名。", this);
                return;
            }

            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            SpellGuardStartSceneLaunch.Request(SpellGuardStartSceneLaunchMode.DeveloperTools);
            SceneManager.LoadScene(developerToolsSceneName);
        }

        public void LaunchTraining()
        {
            LaunchDeveloperTools();
        }

        public void OpenMain()
        {
            screen = StartMenuScreen.Main;
            ResetSelection();
        }

        public void OpenTutorial()
        {
            screen = StartMenuScreen.Guide;
            SpellGuardLocalProgress.SaveTutorialSeen(true);
            ResetSelection();
        }

        public void OpenSettings()
        {
            screen = StartMenuScreen.Settings;
            ResetSelection();
        }

        public void OpenGestureGuide()
        {
            OpenTutorial();
        }

        public void OpenCalibration()
        {
            screen = StartMenuScreen.Calibration;
            EnsureCalibrationCameraPreview();
            ResetSelection();
        }

        private void Launch(SpellGuardStartSceneLaunchMode mode)
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogError("开始场景未配置战斗场景名。", this);
                return;
            }

            inputProvider?.ClearTransientInputs();
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            SpellGuardStartSceneLaunch.Request(mode);
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void UpdateGestureNavigation()
        {
            if (inputProvider == null)
            {
                return;
            }

            var action = inputProvider.GetMenuAction(screen != StartMenuScreen.Main);
            if (HandleTransientMenuAction(action))
            {
                return;
            }

            if (!action.IsValid || action.IsTransient)
            {
                ClearHoldState();
                return;
            }

            switch (action.Intent)
            {
                case GestureIntent.MenuConfirm:
                    UpdateHoldAction("confirm", GestureIntent.MenuConfirm, GetConfirmHoldSeconds(), ActivateSelectedRegion);
                    break;
                case GestureIntent.MenuBack:
                    UpdateHoldAction("back", GestureIntent.MenuBack, GetBackHoldSeconds(), OpenMainWithFeedback);
                    break;
                default:
                    ClearHoldState();
                    break;
            }
        }

        private bool HandleTransientMenuAction(GestureAction action)
        {
            if (!action.IsValid || !action.IsTransient || action.TriggeredTime <= lastHandledMotionTime)
            {
                return false;
            }

            lastHandledMotionTime = action.TriggeredTime;
            ClearHoldState();

            switch (action.Intent)
            {
                case GestureIntent.MenuPrevious:
                    MoveSelection(-1);
                    return true;

                case GestureIntent.MenuNext:
                    MoveSelection(1);
                    return true;

                case GestureIntent.MenuConfirm:
                    ActivateSelectedRegion();
                    return true;
            }

            return false;
        }

        private void UpdateKeyboardFallback()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveSelection(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveSelection(1);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ActivateSelectedRegion();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (screen != StartMenuScreen.Main)
                {
                    OpenMainWithFeedback();
                }
            }
        }

        private void UpdateHoldAction(string key, GestureIntent intent, float requiredSeconds, Action action)
        {
            if (holdKey != key || holdIntent != intent)
            {
                holdKey = key;
                holdIntent = intent;
                holdStartedAt = Time.unscaledTime;
            }

            if (Time.unscaledTime - holdStartedAt >= requiredSeconds)
            {
                action?.Invoke();
                ClearHoldState();
            }
        }

        private void ActivateSelectedRegion()
        {
            var key = GetSelectedKey();
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            ActivateRegion(key);
        }

        private void ActivateRegion(string key)
        {
            lastActivatedKey = key;
            lastActivatedAt = Time.unscaledTime;
            SpellGuardAudioController.Instance?.PlayUiClickSfx();

            switch (key)
            {
                case "start":
                    LaunchCombat();
                    break;
                case "developer-tools":
                    LaunchDeveloperTools();
                    break;
                case "guide":
                    OpenTutorial();
                    break;
                case "settings":
                    OpenSettings();
                    break;
                case "calibration":
                    OpenCalibration();
                    break;
                case "confirm":
                case "difficulty":
                case "input-mode":
                case "music-volume":
                case "sfx-volume":
                    CycleSetting(key);
                    break;
                case "camera-device":
                    CycleCameraDevice();
                    break;
                case "back":
                    OpenMain();
                    break;
            }
        }

        private void CycleSetting(string key)
        {
            if (settings == null)
            {
                return;
            }

            if (key == "confirm") settings.CycleConfirm();
            else if (key == "difficulty") settings.CycleDifficulty();
            else if (key == "input-mode")
            {
                var nextMode = settings.CycleInputMode();
                inputRouter?.SetMode(nextMode);
            }
            else if (key == "music-volume") settings.CycleMusicVolume();
            else if (key == "sfx-volume") settings.CycleSfxVolume();

            SpellGuardAudioController.Instance?.ApplySettings(settings);
            Log($"cycle setting {key}");
        }

        private void CycleCameraDevice()
        {
            if (webcamFeed == null)
            {
                return;
            }

            var switched = webcamFeed.TryStartNextPhysicalCamera();
            if (switched && inputRouter != null && inputRouter.Mode == GestureInputRouter.InputMode.NativeMediapipe)
            {
                inputRouter.SetMode(GestureInputRouter.InputMode.Mock);
                inputRouter.SetMode(GestureInputRouter.InputMode.NativeMediapipe);
            }

            nativeMediapipeProvider?.SetStatusText(switched
                ? $"已切换摄像头：{webcamFeed.ActiveDeviceName}"
                : $"摄像头切换失败：{webcamFeed.StatusText}");
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
            Log($"cycle camera device {webcamFeed.ActiveDeviceName}");
        }

        private void MoveSelection(int delta)
        {
            if (regionCount <= 0)
            {
                selectedIndex = 0;
                return;
            }

            selectedIndex = (selectedIndex + delta) % regionCount;
            if (selectedIndex < 0)
            {
                selectedIndex += regionCount;
            }

            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
        }

        private void OpenMainWithFeedback()
        {
            OpenMain();
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
        }

        private void RebuildRegions()
        {
            regionCount = 0;
            var layout = GetLayout();
            switch (screen)
            {
                case StartMenuScreen.Settings:
                    AddRegion("input-mode", MakeNavButtonRect(layout, 0, 6));
                    AddRegion("confirm", MakeNavButtonRect(layout, 1, 6));
                    AddRegion("difficulty", MakeNavButtonRect(layout, 2, 6));
                    AddRegion("music-volume", MakeNavButtonRect(layout, 3, 6));
                    AddRegion("sfx-volume", MakeNavButtonRect(layout, 4, 6));
                    AddRegion("back", MakeNavButtonRect(layout, 5, 6));
                    break;
                case StartMenuScreen.Guide:
                    AddRegion("start", MakeNavButtonRect(layout, 0, 2));
                    AddRegion("back", MakeNavButtonRect(layout, 1, 2));
                    break;
                case StartMenuScreen.Calibration:
                    AddRegion("input-mode", MakeNavButtonRect(layout, 0, 3));
                    AddRegion("camera-device", MakeNavButtonRect(layout, 1, 3));
                    AddRegion("back", MakeNavButtonRect(layout, 2, 3));
                    break;
                default:
                    AddRegion("start", MakeNavButtonRect(layout, 0, 5));
                    AddRegion("guide", MakeNavButtonRect(layout, 1, 5));
                    AddRegion("calibration", MakeNavButtonRect(layout, 2, 5));
                    AddRegion("settings", MakeNavButtonRect(layout, 3, 5));
                    AddRegion("developer-tools", MakeNavButtonRect(layout, 4, 5));
                    break;
            }
        }

        private void DrawMain(StartLayout layout)
        {
            DrawHero(layout, "SPELL GUARD", "体感施法守卫", BuildMainText(), "挥动切换，握拳确认，张掌返回。 ");
            DrawNavPanel(layout, "主菜单", "选择下一步", new[]
            {
                ("start", "开始守卫"),
                ("guide", "玩法说明"),
                ("calibration", "摄像头校准"),
                ("settings", "设置"),
                ("developer-tools", "开发者工具"),
            });
        }

        private void DrawGuide(StartLayout layout)
        {
            DrawHero(layout, "玩法说明", "守住仪式核心", "目标：阻止敌人突破通道，达到目标分数即胜利。\n\n战斗：握拳=火焰，V 手势=冰霜，张掌=护盾。\n\n移动：左右/上下挥动进行换位。\n\n菜单：挥动切换，握拳确认，张掌返回。", "准备好后直接开始守卫。 ");
            DrawNavPanel(layout, "下一步", "", new[]
            {
                ("start", "开始守卫"),
                ("back", "返回主菜单"),
            });
        }

        private void DrawSettings(StartLayout layout)
        {
            DrawHero(layout, "设置", "演示前确认", "输入模式、施法确认、敌人节奏和音量。\n\n正式演示建议使用 Mock；需要真实摄像头时先到校准页确认画面。", "张掌返回主菜单。 ");
            DrawNavPanel(layout, "设置项", "", new[]
            {
                ("input-mode", $"输入模式：{GetInputModeLabel()}"),
                ("confirm", $"结印确认：{GetConfirmLabel()}"),
                ("difficulty", $"敌人节奏：{GetDifficultyLabel()}"),
                ("music-volume", $"音乐音量：{GetMusicVolumeLabel()}"),
                ("sfx-volume", $"音效音量：{GetSfxVolumeLabel()}"),
                ("back", "返回主菜单"),
            });
        }

        private void DrawCalibration(StartLayout layout)
        {
            EnsureCalibrationCameraPreview();
            DrawCalibrationHero(layout);
            DrawCameraPreview(layout);
            DrawNavPanel(layout, "校准", "", new[]
            {
                ("input-mode", $"输入：{GetInputModeLabel()}"),
                ("camera-device", "切换摄像头"),
                ("back", "返回主菜单"),
            });
        }

        private void DrawCalibrationHero(StartLayout layout)
        {
            DrawPanel(layout.HeroPanel, new Color(0.045f, 0.06f, 0.1f, 0.96f), new Color(0.35f, 0.82f, 1f, 0.95f));
            GUI.Label(layout.Title, "摄像头校准", titleStyle);
            GUI.Label(new Rect(layout.Title.x, layout.Title.yMax + 4f * layout.Scale, layout.Title.width, 28f * layout.Scale), "确认摄像头是否可用", subtitleStyle);

            var preview = GetCalibrationPreviewRect(layout);
            var bodyRect = new Rect(
                layout.Body.x,
                layout.Body.y,
                layout.Body.width,
                Mathf.Max(76f * layout.Scale, preview.y - layout.Body.y - 12f * layout.Scale));
            GUI.Label(bodyRect, BuildCalibrationText(), bodyStyle);

            var hintRect = new Rect(layout.Hint.x, layout.Hint.y, layout.Hint.width, layout.Hint.height);
            GUI.Label(hintRect, "无画面时先切换到 Native MediaPipe，再尝试切换摄像头。", hintStyle);
        }

        private string BuildMainText()
        {
            var bestScore = SpellGuardLocalProgress.LoadBestScore();
            var tutorial = SpellGuardLocalProgress.LoadTutorialSeen() ? "已阅读" : "未阅读";
            return $"欢迎进入符印守卫。\n\n推荐流程：玩法说明 → 摄像头校准 → 开始守卫。\n\n教程状态：{tutorial}\n历史最高分：{bestScore}";
        }

        private string BuildCalibrationText()
        {
            var snapshot = nativeMediapipeProvider != null ? nativeMediapipeProvider.CurrentSnapshot : GestureSnapshot.Missing;
            var cameraReady = webcamFeed != null && webcamFeed.HasReadyFrame;
            var cameraState = cameraReady ? "可用" : "未就绪";
            var inputMode = GetInputModeLabel();
            var gestureState = snapshot.HandPresent ? snapshot.Gesture.ToChinese() : "未检测到手";
            var suggestion = cameraReady
                ? "画面正常，可以开始守卫。"
                : "正式演示可使用 Mock；真实摄像头请切到 Native MediaPipe 后重试。";

            return $"摄像头：{cameraState}\n输入模式：{inputMode}\n识别：{gestureState}\n建议：{suggestion}";
        }

        private void DrawCameraPreview(StartLayout layout)
        {
            var preview = GetCalibrationPreviewRect(layout);

            var previousColor = GUI.color;
            GUI.color = new Color(0.02f, 0.025f, 0.04f, 0.85f);
            GUI.Box(preview, GUIContent.none);
            GUI.color = previousColor;

            var content = Shrink(preview, 8f * layout.Scale, 8f * layout.Scale, 8f * layout.Scale, 8f * layout.Scale);
            if (webcamFeed == null || webcamFeed.Texture == null)
            {
                GUI.Label(content, "摄像头预览未启动\n切到 Native MediaPipe 或点击‘切换摄像头’", bodyStyle);
                return;
            }

            if (!webcamFeed.HasReadyFrame)
            {
                GUI.Label(content, $"摄像头启动中：{webcamFeed.ActiveDeviceName}\n请等待 1-2 秒，或点击‘切换摄像头’", bodyStyle);
                return;
            }

            if (webcamFeed.MirrorPreview)
            {
                var previousMatrix = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), new Vector2(content.x + content.width * 0.5f, content.y + content.height * 0.5f));
                GUI.DrawTexture(content, webcamFeed.Texture, ScaleMode.ScaleToFit, false);
                GUI.matrix = previousMatrix;
            }
            else
            {
                GUI.DrawTexture(content, webcamFeed.Texture, ScaleMode.ScaleToFit, false);
            }
        }

        private Rect GetCalibrationPreviewRect(StartLayout layout)
        {
            var previewHeight = Mathf.Clamp(layout.HeroPanel.height * 0.34f, 150f * layout.Scale, 230f * layout.Scale);
            var bottomReserved = layout.Padding + 44f * layout.Scale;
            return new Rect(
                layout.HeroPanel.x + layout.Padding,
                layout.HeroPanel.yMax - bottomReserved - previewHeight,
                layout.HeroPanel.width - layout.Padding * 2f,
                previewHeight);
        }

        private void EnsureCalibrationCameraPreview()
        {
            if (webcamFeed == null || webcamFeed.IsRunning || (inputRouter != null && inputRouter.Mode == GestureInputRouter.InputMode.ExternalBridge))
            {
                return;
            }

            webcamFeed.StartCamera();
            nativeMediapipeProvider?.SetStatusText(webcamFeed.Texture != null
                ? $"校准预览已启动：{webcamFeed.ActiveDeviceName}"
                : $"校准预览启动失败：{webcamFeed.StatusText}");
        }

        private void DrawHero(StartLayout layout, string title, string subtitle, string body, string hint)
        {
            DrawPanel(layout.HeroPanel, new Color(0.045f, 0.06f, 0.1f, 0.96f), new Color(0.96f, 0.64f, 0.22f, 0.95f));
            GUI.Label(layout.Title, title, titleStyle);
            GUI.Label(new Rect(layout.Title.x, layout.Title.yMax + 4f * layout.Scale, layout.Title.width, 28f * layout.Scale), subtitle, subtitleStyle);
            GUI.Label(layout.Body, body, bodyStyle);
            GUI.Label(layout.Hint, hint, hintStyle);
        }

        private void DrawNavPanel(StartLayout layout, string title, string subtitle, (string key, string label)[] buttons)
        {
            DrawPanel(layout.NavPanel, new Color(0.06f, 0.075f, 0.125f, 0.96f), new Color(0.38f, 0.58f, 1f, 0.9f));
            var header = new Rect(layout.NavPanel.x + layout.Padding, layout.NavPanel.y + layout.Padding, layout.NavPanel.width - layout.Padding * 2f, 36f * layout.Scale);
            GUI.Label(header, title, subtitleStyle);
            GUI.Label(new Rect(header.x, header.yMax + 2f * layout.Scale, header.width, 30f * layout.Scale), subtitle, hintStyle);

            for (var index = 0; index < buttons.Length; index++)
            {
                DrawRegion(buttons[index].key, buttons[index].label, MakeNavButtonRect(layout, index, buttons.Length));
            }
        }

        private void DrawRegion(string key, string label, Rect rect)
        {
            var selected = key == GetSelectedKey();
            var text = selected ? $"▶ {label}" : $"   {label}";
            if (IsRecentlyActivated(key))
            {
                text = $"▶ {label}   已确认";
            }
            else if (selected && !string.IsNullOrEmpty(holdKey))
            {
                var required = holdIntent == GestureIntent.MenuBack ? GetBackHoldSeconds() : GetConfirmHoldSeconds();
                var progress = Mathf.Clamp01((Time.unscaledTime - holdStartedAt) / Mathf.Max(0.01f, required));
                text = $"▶ {label}   {Mathf.RoundToInt(progress * 100f)}%";
            }

            var previousColor = GUI.color;
            GUI.color = selected ? new Color(0.95f, 0.62f, 0.24f, 0.96f) : new Color(0.16f, 0.2f, 0.29f, 0.94f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(new Rect(rect.x + 16f, rect.y + 8f, rect.width - 32f, rect.height - 16f), text, buttonStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                ActivateRegion(key);
            }
        }

        private void DrawGestureStatus(StartLayout layout)
        {
            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            var action = inputProvider != null ? inputProvider.GetMenuAction(screen != StartMenuScreen.Main) : GestureAction.None;
            var selected = GetSelectedKeyLabel();
            var status = snapshot.HandPresent
                ? $"当前识别：{snapshot.Gesture.ToChinese()} · 意图：{FormatAction(action)} · 选中：{selected}"
                : $"未检测到手 · 选中：{selected}";
            var rect = new Rect(layout.SafeArea.x + layout.Padding, layout.SafeArea.yMax - layout.Padding - 28f * layout.Scale, layout.SafeArea.width - layout.Padding * 2f, 26f * layout.Scale);
            GUI.Label(rect, status, hintStyle);
        }

        private static string FormatAction(GestureAction action)
        {
            if (!action.IsValid)
            {
                return "无";
            }

            return action.Intent.ToString();
        }

        private void DrawBackground(StartLayout layout)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0.018f, 0.024f, 0.045f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.13f, 0.22f, 0.55f);
            GUI.DrawTexture(new Rect(layout.SafeArea.x, layout.SafeArea.y, layout.SafeArea.width, layout.SafeArea.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawPanel(Rect rect, Color fillColor, Color accentColor)
        {
            var previousColor = GUI.color;
            GUI.color = fillColor;
            GUI.Box(rect, GUIContent.none, panelStyle ?? GUI.skin.box);
            GUI.color = accentColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, Mathf.Max(3f, 4f * GetLayout().Scale)), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private StartLayout GetLayout()
        {
            var safeArea = UnityEngine.Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                safeArea = new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);
            }

            var width = Mathf.Max(1f, safeArea.width);
            var height = Mathf.Max(1f, safeArea.height);
            var scale = Mathf.Clamp(Mathf.Min(width / 1280f, height / 720f), 0.78f, 1.32f);
            var padding = Mathf.Clamp(24f * scale, 16f, 34f);
            var gap = Mathf.Clamp(22f * scale, 14f, 34f);
            var landscape = width >= height * 1.08f;
            Rect heroPanel;
            Rect navPanel;

            if (landscape)
            {
                var heroWidth = Mathf.Clamp(width * 0.58f, 520f * scale, width - 360f * scale - gap - padding * 2f);
                var navWidth = width - heroWidth - gap - padding * 2f;
                var panelHeight = height - padding * 2f - 32f * scale;
                heroPanel = new Rect(safeArea.x + padding, safeArea.y + padding, heroWidth, panelHeight);
                navPanel = new Rect(heroPanel.xMax + gap, safeArea.y + padding, navWidth, panelHeight);
            }
            else
            {
                var panelWidth = width - padding * 2f;
                var availableHeight = height - padding * 2f - 32f * scale;
                var heroHeight = Mathf.Clamp(availableHeight * 0.55f, 300f * scale, availableHeight - 260f * scale - gap);
                var navHeight = availableHeight - heroHeight - gap;
                heroPanel = new Rect(safeArea.x + padding, safeArea.y + padding, panelWidth, heroHeight);
                navPanel = new Rect(safeArea.x + padding, heroPanel.yMax + gap, panelWidth, navHeight);
            }

            var heroContent = Shrink(heroPanel, padding, padding + 8f * scale, padding, padding);
            var title = new Rect(heroContent.x, heroContent.y, heroContent.width, 48f * scale);
            var body = new Rect(heroContent.x, title.yMax + 44f * scale, heroContent.width, Mathf.Max(80f, heroContent.height - 130f * scale));
            var hint = new Rect(heroContent.x, heroPanel.yMax - padding - 38f * scale, heroContent.width, 36f * scale);

            return new StartLayout
            {
                SafeArea = safeArea,
                HeroPanel = heroPanel,
                NavPanel = navPanel,
                Title = title,
                Body = body,
                Hint = hint,
                Scale = scale,
                Padding = padding,
                Gap = gap,
            };
        }

        private Rect MakeNavButtonRect(StartLayout layout, int index, int count)
        {
            var content = Shrink(layout.NavPanel, layout.Padding, layout.Padding + 74f * layout.Scale, layout.Padding, layout.Padding);
            var gap = Mathf.Clamp(12f * layout.Scale, 8f, 16f);
            var height = Mathf.Max(44f, Mathf.Min(58f * layout.Scale, (content.height - gap * Mathf.Max(0, count - 1)) / Mathf.Max(1, count)));
            var totalHeight = height * count + gap * Mathf.Max(0, count - 1);
            var y = content.y + Mathf.Max(0f, (content.height - totalHeight) * 0.5f) + (height + gap) * index;
            return new Rect(content.x, y, content.width, height);
        }

        private void AddRegion(string key, Rect rect)
        {
            if (regionCount >= regions.Length)
            {
                return;
            }

            regions[regionCount++] = new Region { key = key, rect = rect };
        }

        private void EnsureStyles(float scale)
        {
            if (titleStyle != null && Mathf.Abs(cachedScale - scale) < 0.01f)
            {
                return;
            }

            cachedScale = scale;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(46f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.95f, 0.82f, 1f) }
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.72f, 0.82f, 1f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(17f * scale),
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color(0.86f, 0.9f, 0.98f, 0.97f) }
            };
            hintStyle = new GUIStyle(bodyStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                normal = { textColor = new Color(1f, 0.82f, 0.42f, 0.98f) }
            };
            buttonStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            panelStyle = new GUIStyle(GUI.skin.box);
        }

        private string GetSelectedKey()
        {
            if (regionCount <= 0)
            {
                return null;
            }

            ClampSelectedIndex();
            return regions[selectedIndex].key;
        }

        private string GetSelectedKeyLabel()
        {
            var key = GetSelectedKey();
            if (string.IsNullOrEmpty(key))
            {
                return "无";
            }

            return key;
        }

        private void ResetSelection()
        {
            selectedIndex = 0;
            ClearHoldState();
        }

        private void ClampSelectedIndex()
        {
            if (regionCount <= 0)
            {
                selectedIndex = 0;
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, regionCount - 1);
        }

        private float GetConfirmHoldSeconds()
        {
            return Mathf.Max(0.05f, confirmHoldSeconds);
        }

        private float GetBackHoldSeconds()
        {
            return Mathf.Max(0.05f, settings != null ? settings.MenuBackHoldSeconds : backHoldSeconds);
        }

        private string GetConfirmLabel()
        {
            return settings != null ? settings.ConfirmLabel : "未绑定";
        }

        private string GetInputModeLabel()
        {
            if (inputRouter != null)
            {
                return inputRouter.Mode switch
                {
                    GestureInputRouter.InputMode.Mock => "Mock",
                    GestureInputRouter.InputMode.NativeMediapipe => "Native MediaPipe",
                    GestureInputRouter.InputMode.ExternalBridge => "ExternalBridge",
                    _ => "Unknown"
                };
            }

            return settings != null ? settings.InputModeLabel : "未绑定";
        }

        private string GetDifficultyLabel()
        {
            return settings != null ? settings.DifficultyLabel : "未绑定";
        }

        private string GetMusicVolumeLabel()
        {
            return settings != null ? settings.MusicVolumeLabel : "未绑定";
        }

        private string GetSfxVolumeLabel()
        {
            return settings != null ? settings.SfxVolumeLabel : "未绑定";
        }

        private void ClearHoldState()
        {
            holdKey = null;
            holdIntent = GestureIntent.None;
            holdStartedAt = 0f;
        }

        private bool IsRecentlyActivated(string key)
        {
            return lastActivatedKey == key && Time.unscaledTime - lastActivatedAt <= 0.45f;
        }

        private void Log(string message)
        {
            if (debugLogs)
            {
                Debug.Log($"[Gesture][StartMenu] {message}", this);
            }
        }

        private static Rect Shrink(Rect rect, float left, float top, float right, float bottom)
        {
            return new Rect(rect.x + left, rect.y + top, Mathf.Max(1f, rect.width - left - right), Mathf.Max(1f, rect.height - top - bottom));
        }
    }
}

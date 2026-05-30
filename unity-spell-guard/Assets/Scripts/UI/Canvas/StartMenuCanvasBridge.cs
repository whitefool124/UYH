using SpellGuard.Audio;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpellGuard.UI.Canvas
{
    /// <summary>
    /// Bridges the Canvas UI system with the existing StartMenuController logic.
    /// Attach to StartRuntime alongside SpellGuardStartMenuController.
    /// </summary>
    public class StartMenuCanvasBridge : MonoBehaviour
    {
        [SerializeField] private SpellGuardStartMenuController menuController;
        [SerializeField] private SpellGuardGameSettings settings;
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private WebcamFeedController webcamFeed;

        private UIManager uiManager;
        private StartMenuScreen currentScreen;

        private void Start()
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogError("StartMenuCanvasBridge: No UIManager found in scene");
                return;
            }

            // Wire all StartMenuScreen components
            var screens = FindObjectsOfType<StartMenuScreen>(true);
            foreach (var screen in screens)
            {
                screen.ButtonClicked += HandleButtonClick;
            }

            ShowMain();
        }

        private void Update()
        {
            HandleKeyboardNavigation();
            HandleGestureNavigation();
        }

        private void HandleKeyboardNavigation()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                currentScreen?.MoveSelection(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                currentScreen?.MoveSelection(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                currentScreen?.ActivateSelected();
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (currentScreen != null && currentScreen.ScreenId != "MainScreen")
                    ShowMain();
            }
        }

        private void HandleGestureNavigation()
        {
            if (currentScreen == null) return;

            // Use the existing input provider via MenuController's pattern
            // For now, keyboard is the primary fallback; gesture integration 
            // will be added via the existing GestureInputProvider on StartRuntime
        }

        private void HandleButtonClick(string key)
        {
            SpellGuardAudioController.Instance?.PlayUiClickSfx();
            UpdateSettingsLabels();

            switch (key)
            {
                case "start":
                    LaunchGameplay();
                    break;
                case "developer-tools":
                    LaunchDeveloperTools();
                    break;
                case "guide":
                    ShowGuide();
                    break;
                case "settings":
                    ShowSettings();
                    break;
                case "calibration":
                    ShowCalibration();
                    break;
                case "back":
                    ShowMain();
                    break;
                case "confirm":
                    settings?.CycleConfirm();
                    SpellGuardAudioController.Instance?.ApplySettings(settings);
                    UpdateSettingsLabels();
                    break;
                case "difficulty":
                    settings?.CycleDifficulty();
                    SpellGuardAudioController.Instance?.ApplySettings(settings);
                    UpdateSettingsLabels();
                    break;
                case "input-mode":
                    var nextMode = settings != null ? settings.CycleInputMode() : GestureInputRouter.InputMode.Mock;
                    inputRouter?.SetMode(nextMode);
                    SpellGuardAudioController.Instance?.ApplySettings(settings);
                    UpdateSettingsLabels();
                    break;
                case "music-volume":
                    settings?.CycleMusicVolume();
                    SpellGuardAudioController.Instance?.ApplySettings(settings);
                    UpdateSettingsLabels();
                    break;
                case "sfx-volume":
                    settings?.CycleSfxVolume();
                    SpellGuardAudioController.Instance?.ApplySettings(settings);
                    UpdateSettingsLabels();
                    break;
                case "camera-device":
                    CycleCameraDevice();
                    break;
            }
        }

        private void ShowMain()
        {
            var screen = uiManager.GetScreen<StartMenuScreen>();
            if (screen == null) return;
            uiManager.ShowScreen(screen);
            screen.Configure(StartMenuScreen.Mode.Main);
            currentScreen = screen;
        }

        private void ShowGuide()
        {
            var screens = FindObjectsOfType<StartMenuScreen>(true);
            StartMenuScreen guideScreen = null;
            foreach (var s in screens)
                if (s.ScreenId == "GuideScreen") { guideScreen = s; break; }
            if (guideScreen == null) return;

            uiManager.ShowScreen(guideScreen);
            guideScreen.Configure(StartMenuScreen.Mode.Guide);
            currentScreen = guideScreen;
        }

        private void ShowSettings()
        {
            var screens = FindObjectsOfType<StartMenuScreen>(true);
            StartMenuScreen settingsScreen = null;
            foreach (var s in screens)
                if (s.ScreenId == "SettingsScreen") { settingsScreen = s; break; }
            if (settingsScreen == null) return;

            uiManager.ShowScreen(settingsScreen);
            settingsScreen.Configure(StartMenuScreen.Mode.Settings);
            UpdateSettingsLabels();
            currentScreen = settingsScreen;
        }

        private void ShowCalibration()
        {
            var screens = FindObjectsOfType<StartMenuScreen>(true);
            StartMenuScreen calScreen = null;
            foreach (var s in screens)
                if (s.ScreenId == "CalibrationScreen") { calScreen = s; break; }
            if (calScreen == null) return;

            uiManager.ShowScreen(calScreen);
            calScreen.Configure(StartMenuScreen.Mode.Calibration);
            EnsureCalibrationCamera();
            UpdateCalibrationInfo(calScreen);
            currentScreen = calScreen;
        }

        private void UpdateSettingsLabels()
        {
            var screens = FindObjectsOfType<StartMenuScreen>(true);
            foreach (var s in screens)
            {
                if (s.ScreenId == "SettingsScreen")
                {
                    s.Configure(StartMenuScreen.Mode.Settings);
                    // Rebuild buttons with current labels
                    // The Configure method rebuilds the screen; we just need to call it
                    UpdateSettingsButtonLabels(s);
                }
            }
        }

        private void UpdateSettingsButtonLabels(StartMenuScreen screen)
        {
            // The buttons are already rebuilt by Configure.
            // The label text comes from the mode's hardcoded strings.
            // We need to update them with current values.
            // This requires accessing the buttons array via reflection since it's private.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var buttonsField = typeof(StartMenuScreen).GetField("buttons", flags);
            if (buttonsField == null) return;
            var buttons = buttonsField.GetValue(screen);
            if (buttons == null) return;
            var buttonsArray = (System.Array)buttons;
            for (int i = 0; i < buttonsArray.Length; i++)
            {
                var btn = buttonsArray.GetValue(i);
                var keyField = btn.GetType().GetField("Key");
                var labelField = btn.GetType().GetField("LabelText");
                if (keyField == null || labelField == null) continue;
                var key = (string)keyField.GetValue(btn);
                var labelText = (Text)labelField.GetValue(btn);
                if (labelText == null) continue;

                var selectedKey = screen.SelectedKey;
                var isSelected = key == selectedKey;
                var prefix = isSelected ? "▶ " : "   ";
                switch (key)
                {
                    case "input-mode": labelText.text = prefix + "输入模式：" + GetInputModeLabel(); break;
                    case "confirm": labelText.text = prefix + "结印确认：" + (settings != null ? settings.ConfirmLabel : "--"); break;
                    case "difficulty": labelText.text = prefix + "敌人节奏：" + (settings != null ? settings.DifficultyLabel : "--"); break;
                    case "music-volume": labelText.text = prefix + "音乐音量：" + (settings != null ? settings.MusicVolumeLabel : "--"); break;
                    case "sfx-volume": labelText.text = prefix + "音效音量：" + (settings != null ? settings.SfxVolumeLabel : "--"); break;
                }
            }
        }

        private void UpdateCalibrationInfo(StartMenuScreen screen)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var bodyField = typeof(StartMenuScreen).GetField("bodyText", flags);
            if (bodyField == null) return;
            var bodyText = (Text)bodyField.GetValue(screen);
            if (bodyText == null) return;

            var cameraReady = webcamFeed != null && webcamFeed.HasReadyFrame;
            var inputMode = GetInputModeLabel();
            var snapshot = nativeMediapipeProvider != null ? nativeMediapipeProvider.CurrentSnapshot : GestureSnapshot.Missing;
            var gestureState = snapshot.HandPresent ? snapshot.Gesture.ToChinese() : "未检测到手";

            bodyText.text = $"摄像头：{(cameraReady ? "可用" : "未就绪")}\n输入模式：{inputMode}\n识别：{gestureState}";
        }

        private void EnsureCalibrationCamera()
        {
            if (webcamFeed == null || webcamFeed.IsRunning) return;
            if (inputRouter != null && inputRouter.Mode == GestureInputRouter.InputMode.ExternalBridge) return;
            webcamFeed.StartCamera();
        }

        private void CycleCameraDevice()
        {
            if (webcamFeed == null) return;
            var switched = webcamFeed.TryStartNextPhysicalCamera();
            if (switched && inputRouter != null && inputRouter.Mode == GestureInputRouter.InputMode.NativeMediapipe)
            {
                inputRouter.SetMode(GestureInputRouter.InputMode.Mock);
                inputRouter.SetMode(GestureInputRouter.InputMode.NativeMediapipe);
            }
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();

            if (currentScreen != null && currentScreen.ScreenId == "CalibrationScreen")
                UpdateCalibrationInfo(currentScreen);
        }

        private string GetInputModeLabel()
        {
            if (inputRouter == null) return settings != null ? settings.InputModeLabel : "--";
            return inputRouter.Mode switch
            {
                GestureInputRouter.InputMode.Mock => "Mock",
                GestureInputRouter.InputMode.NativeMediapipe => "Native MediaPipe",
                GestureInputRouter.InputMode.ExternalBridge => "ExternalBridge",
                _ => "Unknown"
            };
        }

        private void LaunchGameplay()
        {
            var menuController = FindObjectOfType<SpellGuardStartMenuController>();
            if (menuController != null)
            {
                menuController.LaunchCombat();
                return;
            }

            // Fallback
            SpellGuardStartSceneLaunch.Request(SpellGuardStartSceneLaunchMode.Combat);
            SceneTransitionManager.Instance.LoadScene("SpellGuardPrototype");
        }

        private void LaunchDeveloperTools()
        {
            SpellGuardStartSceneLaunch.Request(SpellGuardStartSceneLaunchMode.DeveloperTools);
            SceneTransitionManager.Instance.LoadScene("SpellGuardDeveloperTools");
        }
    }
}

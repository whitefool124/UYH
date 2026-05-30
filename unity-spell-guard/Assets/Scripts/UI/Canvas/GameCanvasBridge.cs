using SpellGuard.Core;
using UnityEngine;

namespace SpellGuard.UI.Canvas
{
    /// <summary>
    /// Bridges Canvas UI with SpellGuardFlowController in the game scene.
    /// Reads flow state and pushes it to Canvas screens; handles button events.
    /// </summary>
    public class GameCanvasBridge : MonoBehaviour
    {
        [SerializeField] private SpellGuardFlowController flowController;

        private UIManager uiManager;
        private GameHUDScreen hudScreen;
        private GameOverlayScreen overlayScreen;
        private SpellGuardScreen lastScreen;

        private void Start()
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogError("GameCanvasBridge: No UIManager found");
                return;
            }

            hudScreen = uiManager.GetScreen<GameHUDScreen>();
            overlayScreen = uiManager.GetScreen<GameOverlayScreen>();

            if (overlayScreen != null)
                overlayScreen.ButtonClicked += HandleOverlayButton;

            lastScreen = SpellGuardScreen.Menu;
            RefreshUI();
        }

        private void Update()
        {
            if (flowController == null) return;

            var currentScreen = flowController.Screen;
            if (currentScreen != lastScreen)
            {
                RefreshUI();
                lastScreen = currentScreen;
            }

            // Update HUD data every frame
            UpdateHUD();

            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentScreen == SpellGuardScreen.Playing)
                    flowController.PauseRun();
                else if (currentScreen == SpellGuardScreen.Paused)
                    flowController.ResumeRun();
                else if (currentScreen != SpellGuardScreen.Menu)
                    flowController.ReturnToMenu();
            }
        }

        private void RefreshUI()
        {
            if (flowController == null) return;
            var screen = flowController.Screen;
            var viewData = flowController.GetViewData();
            var status = flowController.GetScreenStatus();

            switch (screen)
            {
                case SpellGuardScreen.Menu:
                    ShowOverlay("主菜单", status.Description, viewData.HintText ?? "",
                        ("start", "开始守卫"), ("tutorial", "上手教程"),
                        ("training", "训练场"), ("settings", "设置"),
                        ("back-to-start", "返回主菜单"));
                    break;
                case SpellGuardScreen.Settings:
                    ShowOverlay("设置", status.Description, viewData.HintText ?? "",
                        ("input-mode", "输入模式：" + flowController.InputModeLabel),
                        ("confirm", "结印确认：" + viewData.ConfirmLabel),
                        ("difficulty", "敌人节奏：" + viewData.DifficultyLabel),
                        ("back", "返回菜单"));
                    break;
                case SpellGuardScreen.Tutorial:
                    ShowOverlay("上手教程", status.Description, viewData.HintText ?? "",
                        ("training", "进入训练场"), ("start", "直接开始守卫"),
                        ("back", "返回菜单"));
                    break;
                case SpellGuardScreen.Training:
                    HideOverlay();
                    break;
                case SpellGuardScreen.Playing:
                    HideOverlay();
                    break;
                case SpellGuardScreen.Paused:
                    ShowOverlay("战斗暂停", status.Description, viewData.HintText ?? "",
                        ("resume", "继续"), ("restart", "重开本局"),
                        ("back", "返回菜单"));
                    break;
                case SpellGuardScreen.Results:
                    ShowOverlay(status.Title, status.Description,
                        $"得分：{viewData.CombatScore}\n命中率：{viewData.HitRate}%",
                        ("restart", "再来一局"), ("back", "返回菜单"));
                    break;
            }
        }

        private void ShowOverlay(string title, string subtitle, string body, params (string, string)[] buttons)
        {
            if (overlayScreen == null) return;
            overlayScreen.Configure(GameOverlayScreen.Mode.Menu, title, subtitle, body, "", buttons);
            uiManager.ShowScreen(overlayScreen);
        }

        private void HideOverlay()
        {
            if (overlayScreen != null && overlayScreen.IsOpen)
                uiManager.ShowScreen(hudScreen);
        }

        private void UpdateHUD()
        {
            if (hudScreen == null || flowController == null) return;

            var viewData = flowController.GetViewData();
            var status = flowController.GetScreenStatus();

            hudScreen.SetScreenLabel(status.Title);
            hudScreen.SetScore(viewData.CombatScore);
            hudScreen.SetHint(viewData.HintText ?? "");

            // Access player health via reflection (private serialized field)
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var phField = typeof(SpellGuard.Core.SpellGuardFlowController).GetField("playerHealth", flags);
            if (phField != null)
            {
                var ph = phField.GetValue(flowController) as SpellGuard.Combat.PlayerHealth;
                if (ph != null)
                    hudScreen.SetHealth(ph.CurrentHealth, ph.MaxHealth);
            }
        }

        private void HandleOverlayButton(string key)
        {
            if (flowController == null) return;
            SpellGuard.Audio.SpellGuardAudioController.Instance?.PlayUiClickSfx();

            switch (key)
            {
                case "start": flowController.StartRun(); break;
                case "tutorial": flowController.OpenTutorial(); break;
                case "training": flowController.StartTraining(); break;
                case "settings": flowController.OpenSettings(); break;
                case "resume": flowController.ResumeRun(); break;
                case "restart": flowController.RestartRun(); break;
                case "back": flowController.ReturnToMenu(); break;
                case "back-to-start":
                    Time.timeScale = 1f;
                    SpellGuardStartSceneLaunch.Request(SpellGuardStartSceneLaunchMode.Combat);
                    SceneTransitionManager.Instance.LoadScene("SpellGuardStart");
                    break;
                case "confirm": flowController.CycleConfirmSetting(); break;
                case "difficulty": flowController.CycleDifficultySetting(); break;
                case "input-mode": flowController.CycleInputModeSetting(); break;
            }
        }
    }
}

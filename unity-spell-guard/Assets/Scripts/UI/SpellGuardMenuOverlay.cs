using System;
using SpellGuard.Audio;
using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.UI
{
    public class SpellGuardMenuOverlay : MonoBehaviour
    {
        [Serializable]
        private struct Region
        {
            public string key;
            public string label;
            public Rect rect;
        }

        private struct OverlayLayout
        {
            public Rect Panel;
            public Rect Content;
            public Rect Title;
            public Rect Body;
            public Rect Hint;
            public Rect ButtonsRow;
            public float Scale;
            public float Padding;
            public float Gap;
        }

        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private SpellGuardGameSettings settings;
        [SerializeField] private SpellGuardFlowController flowController;
        [SerializeField] private bool debugLogs = true;

        private const float DefaultMenuDwellSeconds = 0.8f;
        private const float DefaultMenuBackHoldSeconds = 0.65f;

        private readonly Region[] regions = new Region[16];
        private int regionCount;
        private string focusedKey;
        private string dwellKey;
        private float dwellStartedAt;
        private GestureIntent dwellIntent = GestureIntent.None;
        private float backStartedAt;
        private int selectedIndex;
        private SpellGuardScreen lastScreen;
        private float lastHandledMotionTime = -999f;
        private string lastActivatedKey;
        private float lastActivatedAt = -999f;
        private GUIStyle overlayTitleStyle;
        private GUIStyle overlayBodyStyle;
        private GUIStyle overlayHintStyle;
        private GUIStyle overlayButtonStyle;
        private GUIStyle overlayPanelStyle;
        private float cachedOverlayScale = -1f;

        private void Update()
        {
            if (!IsMenuLikeScreen())
            {
                return;
            }

            UpdateMenuLikeInput();
        }

        private void OnGUI()
        {
            if (flowController == null || flowController.Screen == SpellGuardScreen.Playing)
            {
                return;
            }

            RebuildRegions();
            switch (flowController.Screen)
            {
                case SpellGuardScreen.Menu:
                    DrawMenu();
                    break;
                case SpellGuardScreen.Settings:
                    DrawSettings();
                    break;
                case SpellGuardScreen.Tutorial:
                    DrawTutorial();
                    break;
                case SpellGuardScreen.Training:
                    DrawTraining();
                    break;
                case SpellGuardScreen.Paused:
                    DrawPaused();
                    break;
                case SpellGuardScreen.Results:
                    DrawResults();
                    break;
            }

            DrawGestureStatus();
        }

        private bool IsMenuLikeScreen()
        {
            if (flowController == null)
            {
                return false;
            }

            return flowController.Screen == SpellGuardScreen.Menu ||
                   flowController.Screen == SpellGuardScreen.Settings ||
                   flowController.Screen == SpellGuardScreen.Tutorial ||
                   flowController.Screen == SpellGuardScreen.Training ||
                   flowController.Screen == SpellGuardScreen.Paused ||
                   flowController.Screen == SpellGuardScreen.Results;
        }

        private void UpdateMenuLikeInput()
        {
            if (flowController.Screen == SpellGuardScreen.Training && flowController.IsCustomGestureRecording)
            {
                ClearHoldState();
                return;
            }

            RebuildRegions();
            ClampSelectedIndex();
            focusedKey = GetSelectedKey();

            var allowBack = flowController.Screen != SpellGuardScreen.Menu && flowController.Screen != SpellGuardScreen.Playing;
            var action = inputProvider != null ? inputProvider.GetMenuAction(allowBack) : GestureAction.None;
            if (action.IsValid && !action.IsTransient && action.Intent == GestureIntent.MenuBack)
            {
                if (backStartedAt <= 0f)
                {
                    backStartedAt = Time.unscaledTime;
                }

                if (Time.unscaledTime - backStartedAt >= GetMenuBackHoldSeconds())
                {
                    flowController.ReturnToMenu();
                }

                return;
            }

            backStartedAt = 0f;

            if (HandleTransientMenuAction(action))
            {
                return;
            }

            if (!action.IsValid || action.IsTransient || action.Intent != GestureIntent.MenuConfirm)
            {
                ClearHoldState();
                return;
            }

            if (dwellKey != focusedKey || dwellIntent != action.Intent)
            {
                dwellKey = focusedKey;
                dwellIntent = action.Intent;
                dwellStartedAt = Time.unscaledTime;
            }

            if (!string.IsNullOrEmpty(focusedKey) && Time.unscaledTime - dwellStartedAt >= GetRequiredHoldSeconds(focusedKey))
            {
                ActivateRegion(focusedKey);
                ClearHoldState();
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
            if (flowController.Screen == SpellGuardScreen.Training)
            {
                flowController.RecordTrainingAction(action);
            }

            switch (action.Intent)
            {
                case GestureIntent.MenuNext:
                    MoveSelection(1);
                    return true;

                case GestureIntent.MenuPrevious:
                    MoveSelection(-1);
                    return true;

                case GestureIntent.MenuConfirm:
                    if (!string.IsNullOrEmpty(focusedKey))
                    {
                        ActivateRegion(focusedKey);
                    }
                    return true;
            }

            return false;
        }

        private void ActivateRegion(string key)
        {
            lastActivatedKey = key;
            lastActivatedAt = Time.unscaledTime;
            SpellGuardAudioController.Instance?.PlayUiClickSfx();

            switch (flowController.Screen)
            {
                case SpellGuardScreen.Menu:
                    if (key == "start") flowController.StartRun();
                    else if (key == "training" && flowController.DeveloperToolsEnabled) flowController.StartTraining();
                    else if (key == "settings") flowController.OpenSettings();
                    else if (key == "tutorial") flowController.OpenTutorial();
                    break;
                case SpellGuardScreen.Settings:
                    if (key == "input-mode") flowController.CycleInputModeSetting();
                    else if (key == "confirm") flowController.CycleConfirmSetting();
                    else if (key == "difficulty") flowController.CycleDifficultySetting();
                    else if (key == "music-volume") flowController.CycleMusicVolumeSetting();
                    else if (key == "sfx-volume") flowController.CycleSfxVolumeSetting();
                    else if (key == "back") flowController.ReturnToMenu();
                    break;
                case SpellGuardScreen.Tutorial:
                    if (key == "play") flowController.StartRun();
                    else if (key == "training" && flowController.DeveloperToolsEnabled) flowController.StartTraining();
                    else if (key == "back") flowController.ReturnToMenu();
                    break;
                case SpellGuardScreen.Training:
                    if (key == "pointer-check") flowController.RecordTrainingPointerCheck();
                    else if (key == "reset-training") flowController.ResetTrainingStats();
                    else if (key == "start-from-training") flowController.StartRunFromTraining();
                    else if (flowController.DeveloperToolsEnabled && key == "custom-slot") flowController.CycleCustomGestureSlot();
                    else if (flowController.DeveloperToolsEnabled && key == "custom-target") flowController.CycleCustomGestureHandedness();
                    else if (flowController.DeveloperToolsEnabled && key == "custom-record") flowController.StartCustomGestureRecording();
                    else if (flowController.DeveloperToolsEnabled && key == "custom-save") flowController.SaveCustomGestureTemplate();
                    else if (flowController.DeveloperToolsEnabled && key == "custom-reload") flowController.ReloadCustomGestureTemplates();
                    else if (key == "menu") flowController.ReturnToMenu();
                    break;
                case SpellGuardScreen.Paused:
                    if (key == "resume") flowController.ResumeRun();
                    else if (key == "restart") flowController.RestartRun();
                    else if (key == "menu") flowController.ReturnToMenu();
                    break;
                case SpellGuardScreen.Results:
                    if (key == "restart") flowController.StartRun();
                    else if (key == "menu") flowController.ReturnToMenu();
                    break;
            }

            dwellKey = null;
            focusedKey = null;
            dwellStartedAt = 0f;
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

        private void ClampSelectedIndex()
        {
            if (regionCount <= 0)
            {
                selectedIndex = 0;
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, regionCount - 1);
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

            focusedKey = GetSelectedKey();
            SpellGuardAudioController.Instance?.PlayTrainingPingSfx();
        }

        private void ClearHoldState()
        {
            dwellKey = null;
            dwellIntent = GestureIntent.None;
            dwellStartedAt = 0f;
        }

        private void RebuildRegions()
        {
            regionCount = 0;
            if (flowController.Screen != lastScreen)
            {
                selectedIndex = 0;
                ClearHoldState();
                lastScreen = flowController.Screen;
            }

            var layout = GetOverlayLayout();
            switch (flowController.Screen)
            {
                case SpellGuardScreen.Menu:
                    AddRegion("start", "开始守卫", MakeButtonRect(layout, 0, 0, 4));
                    AddRegion("tutorial", "上手教程", MakeButtonRect(layout, 1, 0, 4));
                    if (flowController.DeveloperToolsEnabled)
                    {
                        AddRegion("training", "开发者训练/录入", MakeButtonRect(layout, 2, 0, 4));
                        AddRegion("settings", "调整设置", MakeButtonRect(layout, 3, 0, 4));
                    }
                    else
                    {
                        AddRegion("settings", "调整设置", MakeButtonRect(layout, 2, 0, 3));
                    }
                    break;
                case SpellGuardScreen.Settings:
                    AddRegion("input-mode", $"输入模式：{flowController.InputModeLabel}", MakeButtonRect(layout, 0, 0, 6));
                    AddRegion("confirm", $"结印确认时长：{flowController.ConfirmLabel}", MakeButtonRect(layout, 1, 0, 6));
                    AddRegion("difficulty", $"敌人节奏：{flowController.DifficultyLabel}", MakeButtonRect(layout, 2, 0, 6));
                    AddRegion("music-volume", $"音乐音量：{flowController.MusicVolumeLabel}", MakeButtonRect(layout, 3, 0, 6));
                    AddRegion("sfx-volume", $"音效音量：{flowController.SfxVolumeLabel}", MakeButtonRect(layout, 4, 0, 6));
                    AddRegion("back", "返回主菜单", MakeButtonRect(layout, 5, 0, 6));
                    break;
                case SpellGuardScreen.Tutorial:
                    AddRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
                    if (flowController.DeveloperToolsEnabled)
                    {
                        AddRegion("training", "开发者训练/录入", MakeButtonRect(layout, 1, 0, 3));
                        AddRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
                    }
                    else
                    {
                        AddRegion("back", "返回主菜单", MakeButtonRect(layout, 1, 0, 2));
                    }
                    break;
                case SpellGuardScreen.Training:
                    AddTrainingRegions(layout);
                    break;
                case SpellGuardScreen.Paused:
                    AddRegion("resume", "继续战斗", MakeButtonRect(layout, 0, 0, 3));
                    AddRegion("restart", "重开本局", MakeButtonRect(layout, 1, 0, 3));
                    AddRegion("menu", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
                    break;
                case SpellGuardScreen.Results:
                    AddRegion("restart", "再来一局", MakeButtonRect(layout, 0, 0, 2));
                    AddRegion("menu", "返回主菜单", MakeButtonRect(layout, 1, 0, 2));
                    break;
            }

            ClampSelectedIndex();
        }

        private void DrawMenu()
        {
            var viewData = flowController.GetViewData();
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.06f, 0.08f, 0.13f, 0.94f), new Color(0.95f, 0.68f, 0.25f, 0.92f));
            GUI.Label(layout.Title, "SPELL GUARD", overlayTitleStyle);
            GUI.Label(layout.Body, BuildMenuOverlayText(viewData), overlayBodyStyle);
            GUI.Label(layout.Hint, flowController.HintText, overlayHintStyle);
            DrawRegion("start", "开始守卫", MakeButtonRect(layout, 0, 0, 4));
            DrawRegion("tutorial", "上手教程", MakeButtonRect(layout, 1, 0, 4));
            if (flowController.DeveloperToolsEnabled)
            {
                DrawRegion("training", "开发者训练/录入", MakeButtonRect(layout, 2, 0, 4));
                DrawRegion("settings", "调整设置", MakeButtonRect(layout, 3, 0, 4));
            }
            else
            {
                DrawRegion("settings", "调整设置", MakeButtonRect(layout, 2, 0, 3));
            }
        }

        private void DrawSettings()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.07f, 0.09f, 0.14f, 0.95f), new Color(0.34f, 0.56f, 1f, 0.9f));
            GUI.Label(layout.Title, "战斗设置", overlayTitleStyle);
            GUI.Label(layout.Body, "调整输入模式、施法确认、敌人节奏和音频音量，为正式战斗做准备。", overlayBodyStyle);
            DrawRegion("input-mode", $"输入模式：{flowController.InputModeLabel}", MakeButtonRect(layout, 0, 0, 6));
            DrawRegion("confirm", $"结印确认时长：{flowController.ConfirmLabel}", MakeButtonRect(layout, 1, 0, 6));
            DrawRegion("difficulty", $"敌人节奏：{flowController.DifficultyLabel}", MakeButtonRect(layout, 2, 0, 6));
            DrawRegion("music-volume", $"音乐音量：{flowController.MusicVolumeLabel}", MakeButtonRect(layout, 3, 0, 6));
            DrawRegion("sfx-volume", $"音效音量：{flowController.SfxVolumeLabel}", MakeButtonRect(layout, 4, 0, 6));
            DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 5, 0, 6));
        }

        private void DrawTutorial()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.06f, 0.08f, 0.13f, 0.95f), new Color(0.95f, 0.72f, 0.28f, 0.92f));
            GUI.Label(layout.Title, "上手教程", overlayTitleStyle);
            GUI.Label(layout.Body, "先理解流程，准备好后进入战斗。\n• Point：移动焦点并触发菜单停留\n• Fist / Snap：火焰术，正面打击目标\n• V / 扇手：冰霜术与节奏施法\n• OpenPalm：护盾术，提供一次防护\n\n自定义手势录入和采集测试仅在开发者场景开放。", overlayBodyStyle);
            GUI.Label(layout.Hint, flowController.HintText, overlayHintStyle);
            DrawRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
            if (flowController.DeveloperToolsEnabled)
            {
                DrawRegion("training", "开发者训练/录入", MakeButtonRect(layout, 1, 0, 3));
                DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
            }
            else
            {
                DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 1, 0, 2));
            }
        }

        private void DrawTraining()
        {
            var viewData = flowController.GetViewData();
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.05f, 0.08f, 0.13f, 0.94f), new Color(0.31f, 0.78f, 1f, 0.92f));
            GUI.Label(layout.Title, flowController.DeveloperToolsEnabled ? "开发者测试场" : "训练场", overlayTitleStyle);
            GUI.Label(layout.Body, BuildTrainingOverlayText(viewData), overlayBodyStyle);
            GUI.Label(layout.Hint, viewData.HintText, overlayHintStyle);
            DrawTrainingRegions(layout, viewData);
        }

        private void AddTrainingRegions(OverlayLayout layout)
        {
            AddRegion("pointer-check", "指向确认练习", MakeTrainingRect(layout, 0, 0));
            AddRegion("reset-training", "重置训练计数", MakeTrainingRect(layout, 1, 0));
            if (flowController.DeveloperToolsEnabled)
            {
                AddRegion("custom-slot", "切换 Custom", MakeTrainingRect(layout, 2, 0));
                AddRegion("custom-target", "切换左右手", MakeTrainingRect(layout, 0, 1));
                AddRegion("custom-record", "录制样本", MakeTrainingRect(layout, 1, 1));
                AddRegion("custom-save", "保存模板", MakeTrainingRect(layout, 2, 1));
                AddRegion("custom-reload", "重新加载/测试", MakeTrainingRect(layout, 0, 2));
                AddRegion("start-from-training", "保持无限靶场", MakeTrainingRect(layout, 1, 2));
                AddRegion("menu", "返回主菜单", MakeTrainingRect(layout, 2, 2));
                return;
            }

            AddRegion("start-from-training", "完成训练并开始守卫", MakeTrainingRect(layout, 0, 1));
            AddRegion("menu", "返回主菜单", MakeTrainingRect(layout, 1, 1));
        }

        private void DrawTrainingRegions(OverlayLayout layout, SpellGuardFlowViewData viewData)
        {
            DrawRegion("pointer-check", "指向确认练习", MakeTrainingRect(layout, 0, 0));
            DrawRegion("reset-training", "重置训练计数", MakeTrainingRect(layout, 1, 0));
            if (flowController.DeveloperToolsEnabled)
            {
                DrawRegion("custom-slot", $"模板：{viewData.CustomGestureDisplayName}", MakeTrainingRect(layout, 2, 0));
                DrawRegion("custom-target", $"手别：{viewData.CustomGestureTargetLabel}", MakeTrainingRect(layout, 0, 1));
                DrawRegion("custom-record", viewData.CustomGestureRecording ? "录制中..." : "录制样本", MakeTrainingRect(layout, 1, 1));
                DrawRegion("custom-save", "保存模板", MakeTrainingRect(layout, 2, 1));
                DrawRegion("custom-reload", "重新加载/测试", MakeTrainingRect(layout, 0, 2));
                DrawRegion("start-from-training", "保持无限靶场", MakeTrainingRect(layout, 1, 2));
                DrawRegion("menu", "返回主菜单", MakeTrainingRect(layout, 2, 2));
                return;
            }

            DrawRegion("start-from-training", viewData.TrainingComplete ? "开始正式守卫" : "完成训练后开始", MakeTrainingRect(layout, 0, 1));
            DrawRegion("menu", "返回主菜单", MakeTrainingRect(layout, 1, 1));
        }

        private void DrawPaused()
        {
            var viewData = flowController.GetViewData();
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.06f, 0.08f, 0.12f, 0.96f), new Color(0.4f, 0.82f, 1f, 0.94f));
            GUI.Label(layout.Title, "战斗暂停", overlayTitleStyle);
            GUI.Label(layout.Body, BuildPausedOverlayText(viewData), overlayBodyStyle);
            GUI.Label(layout.Hint, flowController.HintText, overlayHintStyle);
            DrawRegion("resume", "继续战斗", MakeButtonRect(layout, 0, 0, 3));
            DrawRegion("restart", "重开本局", MakeButtonRect(layout, 1, 0, 3));
            DrawRegion("menu", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
        }

        private void DrawResults()
        {
            var viewData = flowController.GetViewData();
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.08f, 0.08f, 0.12f, 0.95f), new Color(1f, 0.48f, 0.24f, 0.92f));
            GUI.Label(layout.Title, GetResultsTitle(viewData), overlayTitleStyle);
            GUI.Label(layout.Body, BuildResultsOverlayText(viewData), overlayBodyStyle);
            GUI.Label(layout.Hint, flowController.HintText, overlayHintStyle);
            DrawRegion("restart", "再来一局", MakeButtonRect(layout, 0, 0, 2));
            DrawRegion("menu", "返回主菜单", MakeButtonRect(layout, 1, 0, 2));
        }

        private static string BuildTrainingOverlayText(SpellGuardFlowViewData viewData)
        {
            var completion = viewData.TrainingComplete ? "已完成，可开始正式守卫" : "未完成，请补齐指向确认与三法术";
            var nextStep = viewData.TrainingComplete ? "可点‘开始正式守卫’进入战斗" : viewData.TrainingStepLabel;
            var score = float.IsInfinity(viewData.CustomGestureLastScore) ? "-" : viewData.CustomGestureLastScore.ToString("F3");
            var customGestureText = viewData.DeveloperToolsEnabled
                ? $"\n开发者靶场：无敌人 / 无限时间 / 专注识别历史。\n项目手势库：{viewData.CustomGestureDisplayName} · 手别 {viewData.CustomGestureTargetLabel}\n样本：{viewData.CustomGestureSampleCount}/{viewData.CustomGestureRequiredSamples} · {viewData.CustomGestureStatusText}\n测试：重复动作后只验证库内匹配；最近识别 {viewData.CustomGestureLastMatchedName}，分数 {score}"
                : "\n自定义手势录入已从玩家流程移至开发者专用场景。";
            return $"基础训练：{completion} · 当前：{nextStep}\n反馈：{viewData.TrainingStepFeedback}\n火/冰/盾：{viewData.TrainingFireCasts}/{viewData.TrainingIceCasts}/{viewData.TrainingShieldCasts} · Swipe/特殊：{viewData.TrainingSwipeCommands}/{viewData.TrainingSpecialCommands}{customGestureText}";
        }

        private static string BuildResultsOverlayText(SpellGuardFlowViewData viewData)
        {
            var outcomeSummary = viewData.RunResult switch
            {
                SpellGuardRunResult.Victory => $"守卫成功：已达到目标得分 {viewData.TargetScoreToWin}",
                SpellGuardRunResult.Defeat => "守卫失败：生命耗尽，防线被突破",
                _ => "本局已结束"
            };

            return $"{outcomeSummary}\n得分：{viewData.CombatScore}/{viewData.TargetScoreToWin} · 最高分：{viewData.BestScore}\n命中：{viewData.CombatHits} · 施法：{viewData.CombatCasts} · 命中率：{viewData.HitRate}%\n法术构成：火 {viewData.CombatFireCasts} / 冰 {viewData.CombatIceCasts} / 盾 {viewData.CombatShieldCasts}\n表现摘要：{BuildPerformanceSummary(viewData)}";
        }

        private static string BuildPerformanceSummary(SpellGuardFlowViewData viewData)
        {
            if (viewData.CombatCasts <= 0)
            {
                return "尚无施法记录，建议先完成训练场再进入战斗。";
            }

            if (viewData.HitRate >= 80)
            {
                return "高命中率，手势识别与瞄准稳定，可作为论文演示截图。";
            }

            if (viewData.CombatShieldCasts > viewData.CombatFireCasts + viewData.CombatIceCasts)
            {
                return "防御偏多，可在下一局增加火焰 / 冰霜输出。";
            }

            return "继续优化瞄准与施法节奏，提升命中率。";
        }

        private static string BuildPausedOverlayText(SpellGuardFlowViewData viewData)
        {
            return $"当前战斗已暂停\n得分：{viewData.CombatScore}/{viewData.TargetScoreToWin}\n命中：{viewData.CombatHits}\n施法：{viewData.CombatCasts}\n按 Esc 可继续战斗";
        }

        private static string BuildMenuOverlayText(SpellGuardFlowViewData viewData)
        {
            var tutorialStatus = viewData.TutorialSeen ? "已阅读" : "未阅读";
            return viewData.DeveloperToolsEnabled
                ? $"开发者场景：录入自定义手势、测试识别并采集论文数据。\n教程状态：{tutorialStatus} · 最高分：{viewData.BestScore}\nF2 查看调试 HUD，F8/F9 控制性能采集。"
                : $"先看教程，再开始守卫。\n教程状态：{tutorialStatus} · 最高分：{viewData.BestScore}\n鼠标可直接点击按钮，手势仍保留停留确认。";
        }

        private static string GetResultsTitle(SpellGuardFlowViewData viewData)
        {
            return viewData.RunResult switch
            {
                SpellGuardRunResult.Victory => "战斗胜利",
                SpellGuardRunResult.Defeat => "战斗失败",
                _ => "战斗结果"
            };
        }

        private void DrawRegion(string key, string label, Rect rect)
        {
            var isFocused = GetSelectedKey() == key;
            var text = isFocused ? $"▶ {label}" : $"   {label}";
            if (IsRecentlyActivated(key))
            {
                text = $"▶ {label}   已确认";
            }
            if (focusedKey == key && dwellKey == key)
            {
                var progress = Mathf.Clamp01((Time.unscaledTime - dwellStartedAt) / GetRequiredHoldSeconds(key));
                text = $"▶ {label}   {Mathf.RoundToInt(progress * 100f)}%";
            }

            var previousColor = GUI.color;
            var isHolding = focusedKey == key && dwellKey == key;
            GUI.color = isHolding ? new Color(1f, 0.68f, 0.28f, 0.95f) : (isFocused ? new Color(0.38f, 0.58f, 1f, 0.95f) : new Color(0.18f, 0.22f, 0.3f, 0.92f));
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 28f, rect.height - 18f), text, overlayButtonStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                ActivateRegion(key);
            }
        }

        private OverlayLayout GetOverlayLayout()
        {
            var width = Mathf.Max(1f, UnityEngine.Screen.width);
            var height = Mathf.Max(1f, UnityEngine.Screen.height);
            var scale = Mathf.Clamp(Mathf.Min(width / 1280f, height / 720f), 0.9f, 1.28f);
            var panelWidth = Mathf.Clamp(width * 0.34f, 340f, 520f);
            var panelHeight = Mathf.Clamp(height * 0.28f, 220f, 320f);
            var marginX = Mathf.Clamp(width * 0.03f, 18f, 40f);
            var marginY = Mathf.Clamp(height * 0.05f, 16f, 40f);
            var padding = Mathf.Clamp(18f * scale, 14f, 26f);
            var gap = Mathf.Clamp(10f * scale, 8f, 16f);

            var panelX = width - marginX - panelWidth;
            var panelY = height - marginY - panelHeight;
            var panel = new Rect(panelX, panelY, panelWidth, panelHeight);
            if (flowController.Screen == SpellGuardScreen.Training)
            {
                panelWidth = Mathf.Clamp(width * 0.42f, 430f, 620f);
                panelHeight = Mathf.Clamp(height * 0.34f, 300f, 390f);
                panel = new Rect(width - marginX - panelWidth, height - marginY - panelHeight, panelWidth, panelHeight);
            }

            var content = Shrink(panel, padding, padding + 18f * scale, padding, padding);
            var title = new Rect(content.x, content.y, content.width, 30f * scale);
            var body = new Rect(content.x, content.y + 34f * scale, content.width, Mathf.Max(40f, content.height * 0.48f));
            var hint = new Rect(content.x, panel.yMax - padding - 22f * scale - 4f, content.width, 22f * scale);
            var buttonRow = new Rect(content.x, panel.yMax - padding - 48f * scale, content.width, 48f * scale);

            return new OverlayLayout
            {
                Panel = panel,
                Content = content,
                Title = title,
                Body = body,
                Hint = hint,
                ButtonsRow = buttonRow,
                Scale = scale,
                Padding = padding,
                Gap = gap,
            };
        }

        private void EnsureOverlayStyles(float scale)
        {
            if (overlayTitleStyle != null && Mathf.Abs(cachedOverlayScale - scale) < 0.01f)
            {
                return;
            }

            cachedOverlayScale = scale;

            overlayTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(24f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.97f, 1f, 1f) }
            };

            overlayBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.88f, 0.96f, 0.96f) }
            };

            overlayHintStyle = new GUIStyle(overlayBodyStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                normal = { textColor = new Color(1f, 0.84f, 0.46f, 0.98f) }
            };

            overlayButtonStyle = new GUIStyle(overlayBodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };

            overlayPanelStyle = new GUIStyle(GUI.skin.box);
        }

        private void DrawPanel(Rect rect, Color fillColor, Color accentColor)
        {
            var previousColor = GUI.color;
            GUI.color = fillColor;
            GUI.Box(rect, GUIContent.none, overlayPanelStyle ?? GUI.skin.box);
            GUI.color = accentColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private Rect MakeButtonRect(OverlayLayout layout, int column, int row, int columns)
        {
            var rows = flowController.Screen == SpellGuardScreen.Training ? 2 : 1;
            var spacing = Mathf.Clamp(10f * layout.Scale, 8f, 14f);
            var height = Mathf.Clamp(46f * layout.Scale, 40f, 54f);
            var availableWidth = layout.Content.width;
            var width = columns > 1 ? (availableWidth - spacing * (columns - 1)) / columns : availableWidth;
            var y = flowController.Screen == SpellGuardScreen.Training
                ? layout.Panel.yMax - layout.Padding - (height * rows) - spacing
                : layout.Panel.yMax - layout.Padding - height;
            return new Rect(layout.Content.x + (width + spacing) * column, y + (height + spacing) * row, width, height);
        }

        private Rect MakeTrainingRect(OverlayLayout layout, int column, int row)
        {
            var spacing = Mathf.Clamp(10f * layout.Scale, 8f, 14f);
            var width = (layout.Content.width - spacing * 2f) / 3f;
            var height = Mathf.Clamp(40f * layout.Scale, 36f, 46f);
            var baseY = layout.Panel.yMax - layout.Padding - height * 3f - spacing * 2f;
            return new Rect(layout.Content.x + (width + spacing) * column, baseY + (height + spacing) * row, width, height);
        }

        private void AddRegion(string key, string label, Rect rect)
        {
            if (regionCount >= regions.Length)
            {
                return;
            }

            regions[regionCount++] = new Region { key = key, label = label, rect = rect };
        }

        private void DrawGestureStatus()
        {
            if (flowController.Screen == SpellGuardScreen.Training && flowController.IsCustomGestureRecording)
            {
                return;
            }

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            var allowBack = flowController.Screen != SpellGuardScreen.Menu && flowController.Screen != SpellGuardScreen.Playing;
            var action = inputProvider != null ? inputProvider.GetMenuAction(allowBack) : GestureAction.None;
            var layout = GetOverlayLayout();
            var selected = string.IsNullOrEmpty(GetSelectedKey()) ? "无" : GetSelectedKey();
            var status = snapshot.HandPresent
                ? $"识别：{snapshot.Gesture.ToChinese()} · 意图：{FormatAction(action)} · 选中：{selected}"
                : $"未检测到手 · 选中：{selected}";
            GUI.Label(new Rect(layout.Panel.x, layout.Panel.yMax + 6f, layout.Panel.width, 22f * layout.Scale), status, overlayHintStyle);
        }

        private static string FormatAction(GestureAction action)
        {
            if (!action.IsValid)
            {
                return "无";
            }

            return action.Intent.ToString();
        }

        private float GetRequiredHoldSeconds(string key)
        {
            if (flowController.Screen == SpellGuardScreen.Training && (key == "menu" || key == "start-from-training"))
            {
                return flowController.TrainingMenuHoldSeconds;
            }

            return settings != null ? settings.MenuDwellSeconds : DefaultMenuDwellSeconds;
        }

        private float GetMenuBackHoldSeconds()
        {
            return settings != null ? settings.MenuBackHoldSeconds : DefaultMenuBackHoldSeconds;
        }

        private bool IsRecentlyActivated(string key)
        {
            return lastActivatedKey == key && Time.unscaledTime - lastActivatedAt <= 0.45f;
        }

        private void LogFlowEvent(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[Gesture][MenuOverlay] {message}", this);
        }

        private static Rect Shrink(Rect rect, float left, float top, float right, float bottom)
        {
            return new Rect(rect.x + left, rect.y + top, Mathf.Max(1f, rect.width - left - right), Mathf.Max(1f, rect.height - top - bottom));
        }
    }
}

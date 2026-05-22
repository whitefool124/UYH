using System;
using System.Collections.Generic;
using System.IO;
using SpellGuard.Audio;
using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.Diagnostics;
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
        [SerializeField] private GesturePerformanceMonitor performanceMonitor;
        [SerializeField] private DemoRunRecorder demoRunRecorder;
        [SerializeField] private bool debugLogs = true;

        private const float DefaultMenuDwellSeconds = 0.8f;
        private const float DefaultMenuBackHoldSeconds = 0.65f;

        private readonly Region[] regions = new Region[24];
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
        private bool developerCustomGesturePage;
        private bool developerCustomGestureValidationPage;
        private readonly ReferenceClipSequencePlayer referenceClipSequencePlayer = new ReferenceClipSequencePlayer();

        private void Update()
        {
            if (flowController != null && flowController.DeveloperToolsEnabled && flowController.Screen == SpellGuardScreen.Training)
            {
                ClearHoldState();
                focusedKey = null;
                return;
            }

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
            if (flowController.DeveloperToolsEnabled && flowController.Screen == SpellGuardScreen.Training)
            {
                DrawDeveloperTools();
                return;
            }

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

            if (flowController.Screen == SpellGuardScreen.Training && flowController.DeveloperToolsEnabled)
            {
                ActivateDeveloperRegion(key);
                ClearHoldState();
                focusedKey = null;
                return;
            }

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
                    else if (key == "fullscreen") flowController.ToggleFullscreenSetting();
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
                    else if (flowController.DeveloperToolsEnabled && key == "custom-kind") flowController.CycleCustomGestureKind();
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
                    AddRegion("start", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
                    AddRegion("tutorial", "玩法说明", MakeButtonRect(layout, 1, 0, 3));
                    if (flowController.DeveloperToolsEnabled)
                    {
                        AddRegion("training", "识别实验", MakeButtonRect(layout, 2, 0, 4));
                        AddRegion("settings", "设置", MakeButtonRect(layout, 3, 0, 4));
                    }
                    else
                    {
                        AddRegion("settings", "设置", MakeButtonRect(layout, 2, 0, 3));
                    }
                    break;
                case SpellGuardScreen.Settings:
                    AddRegion("input-mode", $"输入模式：{flowController.InputModeLabel}", MakeButtonRect(layout, 0, 0, 4));
                    AddRegion("confirm", $"结印确认时长：{flowController.ConfirmLabel}", MakeButtonRect(layout, 1, 0, 4));
                    AddRegion("difficulty", $"敌人节奏：{flowController.DifficultyLabel}", MakeButtonRect(layout, 2, 0, 4));
                    AddRegion("fullscreen", $"显示模式：{flowController.FullscreenLabel}", MakeButtonRect(layout, 0, 1, 4));
                    AddRegion("music-volume", $"音乐音量：{flowController.MusicVolumeLabel}", MakeButtonRect(layout, 1, 1, 4));
                    AddRegion("sfx-volume", $"音效音量：{flowController.SfxVolumeLabel}", MakeButtonRect(layout, 2, 1, 4));
                    AddRegion("back", "返回主菜单", MakeButtonRect(layout, 3, 1, 4));
                    break;
                case SpellGuardScreen.Tutorial:
                    AddRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 2));
                    if (flowController.DeveloperToolsEnabled)
                    {
                        AddRegion("training", "识别实验", MakeButtonRect(layout, 1, 0, 3));
                        AddRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
                    }
                    else
                    {
                        AddRegion("back", "返回主菜单", MakeButtonRect(layout, 1, 0, 2));
                    }
                    break;
                case SpellGuardScreen.Training:
                    if (flowController.DeveloperToolsEnabled)
                    {
                        AddDeveloperRegions(layout);
                    }
                    else
                    {
                        AddTrainingRegions(layout);
                    }
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
            DrawRegion("start", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
            DrawRegion("tutorial", "玩法说明", MakeButtonRect(layout, 1, 0, 3));
            if (flowController.DeveloperToolsEnabled)
            {
                DrawRegion("training", "识别实验", MakeButtonRect(layout, 2, 0, 4));
                DrawRegion("settings", "设置", MakeButtonRect(layout, 3, 0, 4));
            }
            else
            {
                DrawRegion("settings", "设置", MakeButtonRect(layout, 2, 0, 3));
            }
        }

        private void DrawSettings()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.07f, 0.09f, 0.14f, 0.95f), new Color(0.34f, 0.56f, 1f, 0.9f));
            GUI.Label(layout.Title, "设置", overlayTitleStyle);
            GUI.Label(layout.Body, "键鼠教学关的输入、显示模式和音量。", overlayBodyStyle);
            DrawRegion("input-mode", $"输入模式：{flowController.InputModeLabel}", MakeButtonRect(layout, 0, 0, 4));
            DrawRegion("confirm", $"结印确认时长：{flowController.ConfirmLabel}", MakeButtonRect(layout, 1, 0, 4));
            DrawRegion("difficulty", $"敌人节奏：{flowController.DifficultyLabel}", MakeButtonRect(layout, 2, 0, 4));
            DrawRegion("fullscreen", $"显示模式：{flowController.FullscreenLabel}", MakeButtonRect(layout, 0, 1, 4));
            DrawRegion("music-volume", $"音乐音量：{flowController.MusicVolumeLabel}", MakeButtonRect(layout, 1, 1, 4));
            DrawRegion("sfx-volume", $"音效音量：{flowController.SfxVolumeLabel}", MakeButtonRect(layout, 2, 1, 4));
            DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 3, 1, 4));
        }

        private void DrawTutorial()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.06f, 0.08f, 0.13f, 0.95f), new Color(0.95f, 0.72f, 0.28f, 0.92f));
            GUI.Label(layout.Title, "玩法说明", overlayTitleStyle);
            GUI.Label(layout.Body, "目标：在网格教学关击败 3 个敌人，再到蓝色出口格完成通关。\n\n移动：WASD 每次移动一格。\n施法：左键或 1 释放当前火焰，Q/R 切换火焰。\n流程：Esc 暂停，设置可调全屏和音量。", overlayBodyStyle);
            GUI.Label(layout.Hint, flowController.HintText, overlayHintStyle);
            DrawRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
            if (flowController.DeveloperToolsEnabled)
            {
                DrawRegion("training", "识别实验", MakeButtonRect(layout, 1, 0, 3));
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
            GUI.Label(layout.Title, flowController.DeveloperToolsEnabled ? "开发者实验室" : "训练场", overlayTitleStyle);
            GUI.Label(layout.Body, BuildTrainingOverlayText(viewData), overlayBodyStyle);
            GUI.Label(layout.Hint, viewData.HintText, overlayHintStyle);
            DrawTrainingRegions(layout, viewData);
        }

        private void DrawDeveloperTools()
        {
            var viewData = flowController.GetViewData();
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.035f, 0.045f, 0.07f, 0.97f), new Color(0.25f, 0.88f, 1f, 0.96f));
            GUI.Label(layout.Title, developerCustomGestureValidationPage ? "自定义手势验证" : developerCustomGesturePage ? "自定义手势录入" : "开发者实验室", overlayTitleStyle);
            if (developerCustomGestureValidationPage)
            {
                GUI.Label(layout.Body, BuildCustomGestureValidationPageText(viewData), overlayBodyStyle);
            }
            else
            {
                GUI.Label(layout.Body, developerCustomGesturePage ? BuildCustomGesturePageText(viewData) : BuildDeveloperToolsText(viewData), overlayBodyStyle);
                GUI.Label(layout.Hint, flowController.HintText, overlayHintStyle);
            }
            if (developerCustomGestureValidationPage)
            {
                DrawCustomGestureValidationPreview(layout);
            }
            if (developerCustomGesturePage)
            {
                DrawDeveloperGestureNameField(layout);
            }
            DrawDeveloperRegions(layout);
        }

        private void DrawDeveloperGestureNameField(OverlayLayout layout)
        {
            var rect = MakeDevNameRect(layout);
            GUI.Label(new Rect(rect.x, rect.y - 22f * layout.Scale, rect.width, 20f * layout.Scale), "新手势名称：采完 5 个样本后，用这个名称保存模板", overlayHintStyle);
            var nextName = GUI.TextField(rect, flowController.CustomGestureTemplateName ?? string.Empty, 48);
            if (nextName != flowController.CustomGestureTemplateName)
            {
                flowController.SetCustomGestureTemplateName(nextName);
            }
        }

        private void DrawCustomGestureValidationPreview(OverlayLayout layout)
        {
            var scale = layout.Scale;
            var gap = Mathf.Clamp(12f * scale, 10f, 16f);
            var top = layout.Body.yMax + 10f * scale;
            var bottom = layout.ButtonsRow.y - 14f * scale;
            var height = Mathf.Max(150f, bottom - top);
            var halfWidth = (layout.Content.width - gap) * 0.5f;
            var left = new Rect(layout.Content.x, top, halfWidth, height);
            var right = new Rect(layout.Content.x + halfWidth + gap, top, halfWidth, height);

            DrawPanel(left, new Color(0.05f, 0.06f, 0.1f, 0.78f), new Color(0.55f, 0.74f, 1f, 0.9f));
            DrawPanel(right, new Color(0.05f, 0.06f, 0.1f, 0.78f), new Color(0.48f, 0.9f, 0.7f, 0.9f));

            GUI.Label(new Rect(left.x + 10f, left.y + 8f, left.width - 20f, 20f), "参考说明", overlayHintStyle);
            GUI.Label(new Rect(right.x + 10f, right.y + 8f, right.width - 20f, 20f), "实时骨架", overlayHintStyle);

            var referenceRect = Shrink(left, 12f, 34f, 12f, 16f);
            var liveRect = Shrink(right, 12f, 34f, 12f, 16f);
            DrawCustomGestureValidationReference(referenceRect);

            var frame = inputProvider != null ? inputProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            var hand = frame.HasPrimaryHand ? frame.PrimaryHand : TrackedHandState.Missing;
            var lineColor = frame.HasPrimaryHand ? new Color(0.46f, 0.84f, 1f, 0.95f) : new Color(1f, 0.6f, 0.36f, 0.45f);
            var pointColor = frame.HasPrimaryHand ? new Color(1f, 0.9f, 0.52f, 0.95f) : new Color(1f, 0.45f, 0.3f, 0.45f);
            GestureSkeletonDrawer.DrawHand(liveRect, hand.Landmarks, lineColor, pointColor);

            var label = frame.HasPrimaryHand
                ? $"手势：{hand.StaticGesture.ToChinese()}  手：{hand.Handedness}  置信度：{hand.Confidence:F2}\n分数：{(float.IsPositiveInfinity(flowController.CustomGestureValidationScore) ? "--" : flowController.CustomGestureValidationScore.ToString("F3"))}"
                : "未检测到手";
            GUI.Label(new Rect(right.x + 10f, right.yMax - 52f, right.width - 20f, 40f), label, overlayHintStyle);
        }

        private void DrawCustomGestureValidationReference(Rect rect)
        {
            if (flowController == null || !flowController.TryGetCustomGestureValidationTemplate(out var template) || template == null)
            {
                GUI.Label(rect, "还没有选中要验证的自定义手势模板", overlayBodyStyle);
                return;
            }

            var header = new Rect(rect.x, rect.y, rect.width, 56f);
            var preview = new Rect(rect.x, header.yMax + 6f, rect.width, rect.height - 62f);
            GUI.Label(header, BuildValidationReferenceHeader(template), overlayBodyStyle);

            referenceClipSequencePlayer.SetTemplate(template);
            referenceClipSequencePlayer.Update(Time.unscaledTime);

            var frameRect = Shrink(preview, 8f, 8f, 8f, 26f);
            DrawPanel(frameRect, new Color(0.03f, 0.04f, 0.06f, 0.92f), new Color(0.42f, 0.74f, 1f, 0.88f));
            var texture = referenceClipSequencePlayer.CurrentTexture;
            if (texture != null)
            {
                GUI.DrawTexture(frameRect, texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                GUI.Label(frameRect, referenceClipSequencePlayer.StatusText ?? "未找到参考视频帧", overlayHintStyle);
            }

            GUI.Label(
                new Rect(preview.x + 8f, preview.yMax - 24f, preview.width - 16f, 18f),
                referenceClipSequencePlayer.StatusText ?? "等待参考视频",
                overlayHintStyle);
        }

        private static string BuildValidationReferenceHeader(CustomGestureTemplate template)
        {
            var name = string.IsNullOrWhiteSpace(template.DisplayName) ? template.GestureId : template.DisplayName;
            var note = template.Kind == CustomGestureKind.DynamicMotion
                ? "动态手势：按轨迹走，别停太久"
                : "静态手势：保持姿势稳定";
            return $"{name}\n{note}\n手别：{template.RequiredHandedness}  阈值：{template.MatchThreshold:F2}";
        }

        private void AddDeveloperRegions(OverlayLayout layout)
        {
            if (developerCustomGestureValidationPage)
            {
                AddCustomGestureValidationRegions(layout);
            }
            else if (developerCustomGesturePage)
            {
                AddCustomGestureRegions(layout);
            }
            else
            {
                AddDeveloperHomeRegions(layout);
            }
        }

        private void AddDeveloperHomeRegions(OverlayLayout layout)
        {
            AddRegion("input-mock", "输入：Mock", MakeDevTrainingRect(layout, 0, 0));
            AddRegion("input-native", "输入：摄像头", MakeDevTrainingRect(layout, 1, 0));
            AddRegion("input-external", "输入：外部桥接", MakeDevTrainingRect(layout, 2, 0));
            AddRegion("dev-custom-page", "自定义手势录入", MakeDevTrainingRect(layout, 0, 1));
            AddRegion("dev-validate-page", "验证手势库", MakeDevTrainingRect(layout, 1, 1));
            AddRegion("performance-toggle", performanceMonitor != null && performanceMonitor.IsRecording ? "暂停性能采集" : "开始性能采集", MakeDevTrainingRect(layout, 2, 1));
            AddRegion("performance-export", "导出性能 CSV", MakeDevTrainingRect(layout, 0, 2));
            AddRegion("demo-export", "导出流程 CSV", MakeDevTrainingRect(layout, 1, 2));
            AddRegion("menu", "返回主菜单", MakeDevTrainingRect(layout, 2, 2));
        }

        private void AddCustomGestureRegions(OverlayLayout layout)
        {
            AddRegion("custom-kind", $"类型：{flowController.CustomGestureKindLabel}", MakeDevTrainingRect(layout, 0, 0));
            AddRegion("custom-slot", $"采集组：{flowController.CustomGestureDisplayName}", MakeDevTrainingRect(layout, 1, 0));
            AddRegion("custom-target", $"采集手：{flowController.CustomGestureTargetLabel}", MakeDevTrainingRect(layout, 2, 0));
            AddRegion("custom-target-spell", $"映射法术：{flowController.CustomGestureTargetSpellLabel}", MakeDevTrainingRect(layout, 0, 1));
            AddRegion("custom-record", flowController.CustomGestureRecording ? "录制中" : "录制样本", MakeDevTrainingRect(layout, 1, 1));
            AddRegion("custom-accept", "采用样本", MakeDevTrainingRect(layout, 2, 1));
            AddRegion("custom-discard", "重录样本", MakeDevTrainingRect(layout, 0, 2));
            AddRegion("custom-save", "保存模板", MakeDevTrainingRect(layout, 1, 2));
            AddRegion("custom-reload", "加载模板/验证识别", MakeDevTrainingRect(layout, 2, 2));
            AddRegion("dev-home", "返回开发者首页", MakeDevTrainingRect(layout, 1, 3));
        }

        private void AddCustomGestureValidationRegions(OverlayLayout layout)
        {
            AddRegion("custom-validation-target", $"目标：{flowController.CustomGestureValidationTargetLabel}", MakeDevTrainingRect(layout, 0, 0));
            AddRegion("custom-validation-toggle", flowController.CustomGestureValidationActive ? "暂停持续验证" : "开始持续验证", MakeDevTrainingRect(layout, 1, 0));
            AddRegion("custom-validation-reload", "重新加载库", MakeDevTrainingRect(layout, 2, 0));
            AddRegion("custom-validation-delete", "删除当前模板", MakeDevTrainingRect(layout, 1, 1));
            AddRegion("dev-custom-page", "返回录入页", MakeDevTrainingRect(layout, 0, 2));
            AddRegion("dev-home", "返回开发者首页", MakeDevTrainingRect(layout, 2, 2));
        }

        private void DrawDeveloperRegions(OverlayLayout layout)
        {
            if (developerCustomGestureValidationPage)
            {
                DrawCustomGestureValidationRegions(layout);
            }
            else if (developerCustomGesturePage)
            {
                DrawCustomGestureRegions(layout);
            }
            else
            {
                DrawDeveloperHomeRegions(layout);
            }
        }

        private void DrawDeveloperHomeRegions(OverlayLayout layout)
        {
            DrawRegion("input-mock", $"输入：{GetDeveloperInputModeButtonLabel(SpellGuard.InputSystem.GestureInputRouter.InputMode.Mock, "Mock")}", MakeDevTrainingRect(layout, 0, 0));
            DrawRegion("input-native", $"输入：{GetDeveloperInputModeButtonLabel(SpellGuard.InputSystem.GestureInputRouter.InputMode.NativeMediapipe, "摄像头")}", MakeDevTrainingRect(layout, 1, 0));
            DrawRegion("input-external", $"输入：{GetDeveloperInputModeButtonLabel(SpellGuard.InputSystem.GestureInputRouter.InputMode.ExternalBridge, "外部桥接")}", MakeDevTrainingRect(layout, 2, 0));
            DrawRegion("dev-custom-page", "自定义手势录入", MakeDevTrainingRect(layout, 0, 1));
            DrawRegion("dev-validate-page", "验证手势库", MakeDevTrainingRect(layout, 1, 1));
            DrawRegion("performance-toggle", performanceMonitor != null && performanceMonitor.IsRecording ? "暂停性能采集" : "开始性能采集", MakeDevTrainingRect(layout, 2, 1));
            DrawRegion("performance-export", "导出性能 CSV", MakeDevTrainingRect(layout, 0, 2));
            DrawRegion("demo-export", "导出流程 CSV", MakeDevTrainingRect(layout, 1, 2));
            DrawRegion("menu", "返回主菜单", MakeDevTrainingRect(layout, 2, 2));
        }

        private void DrawCustomGestureRegions(OverlayLayout layout)
        {
            DrawRegion("custom-kind", $"类型：{flowController.CustomGestureKindLabel}", MakeDevTrainingRect(layout, 0, 0));
            DrawRegion("custom-slot", $"采集组：{flowController.CustomGestureDisplayName}", MakeDevTrainingRect(layout, 1, 0));
            DrawRegion("custom-target", $"采集手：{flowController.CustomGestureTargetLabel}", MakeDevTrainingRect(layout, 2, 0));
            DrawRegion("custom-target-spell", $"映射法术：{flowController.CustomGestureTargetSpellLabel}", MakeDevTrainingRect(layout, 0, 1));
            DrawRegion("custom-record", flowController.CustomGestureRecording ? "录制中" : "录制样本", MakeDevTrainingRect(layout, 1, 1));
            DrawRegion("custom-accept", "采用样本", MakeDevTrainingRect(layout, 2, 1));
            DrawRegion("custom-discard", "重录样本", MakeDevTrainingRect(layout, 0, 2));
            DrawRegion("custom-save", "保存模板", MakeDevTrainingRect(layout, 1, 2));
            DrawRegion("custom-reload", "加载模板/验证识别", MakeDevTrainingRect(layout, 2, 2));
            DrawRegion("dev-home", "返回开发者首页", MakeDevTrainingRect(layout, 1, 3));
        }

        private void DrawCustomGestureValidationRegions(OverlayLayout layout)
        {
            DrawRegion("custom-validation-target", $"目标：{flowController.CustomGestureValidationTargetLabel}", MakeDevTrainingRect(layout, 0, 0));
            DrawRegion("custom-validation-toggle", flowController.CustomGestureValidationActive ? "暂停持续验证" : "开始持续验证", MakeDevTrainingRect(layout, 1, 0));
            DrawRegion("custom-validation-reload", "重新加载库", MakeDevTrainingRect(layout, 2, 0));
            DrawRegion("custom-validation-delete", "删除当前模板", MakeDevTrainingRect(layout, 1, 1));
            DrawRegion("dev-custom-page", "返回录入页", MakeDevTrainingRect(layout, 0, 2));
            DrawRegion("dev-home", "返回开发者首页", MakeDevTrainingRect(layout, 2, 2));
        }

        private void ActivateDeveloperRegion(string key)
        {
            if (key == "dev-custom-page")
            {
                developerCustomGesturePage = true;
                developerCustomGestureValidationPage = false;
                flowController.StopCustomGestureValidation();
                selectedIndex = 0;
                return;
            }
            if (key == "dev-validate-page")
            {
                developerCustomGesturePage = false;
                developerCustomGestureValidationPage = true;
                flowController.StartCustomGestureValidation();
                selectedIndex = 0;
                return;
            }
            if (key == "dev-home")
            {
                developerCustomGesturePage = false;
                developerCustomGestureValidationPage = false;
                flowController.StopCustomGestureValidation();
                selectedIndex = 0;
                return;
            }

            if (key == "input-mock") flowController.SetInputMode(SpellGuard.InputSystem.GestureInputRouter.InputMode.Mock);
            else if (key == "input-native") flowController.SetInputMode(SpellGuard.InputSystem.GestureInputRouter.InputMode.NativeMediapipe);
            else if (key == "input-external") flowController.SetInputMode(SpellGuard.InputSystem.GestureInputRouter.InputMode.ExternalBridge);
            else if (key == "custom-kind") flowController.CycleCustomGestureKind();
            else if (key == "custom-slot") flowController.CycleCustomGestureSlot();
            else if (key == "custom-target") flowController.CycleCustomGestureHandedness();
            else if (key == "custom-target-spell") flowController.CycleCustomGestureTargetSpell();
            else if (key == "custom-record") flowController.StartCustomGestureRecording();
            else if (key == "custom-accept") flowController.AcceptCustomGestureSample();
            else if (key == "custom-discard") flowController.DiscardCustomGestureSample();
            else if (key == "custom-save") flowController.SaveCustomGestureTemplate();
            else if (key == "custom-reload") flowController.ReloadCustomGestureTemplates();
            else if (key == "custom-validation-target") flowController.CycleCustomGestureValidationTarget();
            else if (key == "custom-validation-toggle") flowController.ToggleCustomGestureValidation();
            else if (key == "custom-validation-reload") flowController.StartCustomGestureValidation();
            else if (key == "custom-validation-delete") flowController.DeleteSelectedCustomGestureTemplate();
            else if (key == "performance-toggle") TogglePerformanceRecording();
            else if (key == "performance-export") ExportPerformanceCsv();
            else if (key == "demo-export") ExportDemoRunCsv();
            else if (key == "menu") flowController.ReturnToMenu();
        }

        private void TogglePerformanceRecording()
        {
            if (performanceMonitor == null)
            {
                return;
            }

            if (performanceMonitor.IsRecording)
            {
                performanceMonitor.StopRecording();
            }
            else
            {
                performanceMonitor.StartRecording();
            }
        }

        private void ExportPerformanceCsv()
        {
            performanceMonitor?.ExportCsv();
        }

        private void ExportDemoRunCsv()
        {
            demoRunRecorder?.ExportCsv();
        }

        private string BuildDeveloperToolsText(SpellGuardFlowViewData viewData)
        {
            return $"干净采集环境：三维场景隐藏，位移/施法/菜单手势指令禁用。\n当前输入：{flowController.InputModeLabel}（F1 已禁用，请用下方输入按钮切换）\n\n请选择工作区：\n· 自定义手势录入：配置、采样、命名和保存新模板。\n· 验证手势库：选择库里的某一个模板，实时持续监测是否做出。\n· 信息采集：在本页开始/暂停性能采集，并导出性能 CSV 或流程 CSV。\n\n模板库：{viewData.CustomGestureTemplateCount} 个 · 最近命中：{viewData.CustomGestureLastMatchedName}";
        }

        private string BuildCustomGesturePageText(SpellGuardFlowViewData viewData)
        {
            return $"当前阶段：{BuildCustomGestureStageText(viewData)}\n下一步：{BuildCustomGestureNextStepText(viewData)}\n\n配置\n类型：{viewData.CustomGestureKindLabel}\n采集组：{viewData.CustomGestureDisplayName}\n采集手：{viewData.CustomGestureTargetLabel}\n映射法术：{viewData.CustomGestureTargetSpellLabel}\n名称：{FormatTemplateName(viewData.CustomGestureTemplateName)}\n\n采样进度：{viewData.CustomGestureSampleCount}/{viewData.CustomGestureRequiredSamples}\n状态：{viewData.CustomGestureStatusText}\n\n规则：这里录的是库里没有的新动作；录制阶段只检查手是否被追踪、采集手是否一致、关键点是否完整，不做模板匹配评分。保存后会直接绑定到所选法术，再加载模板验证最近命中名。\n最近命中：{viewData.CustomGestureLastMatchedName}";
        }

        private string BuildCustomGestureValidationPageText(SpellGuardFlowViewData viewData)
        {
            var score = flowController != null ? flowController.CustomGestureValidationScore : float.PositiveInfinity;
            return $"目标：{viewData.CustomGestureValidationTargetLabel}  模板：{viewData.CustomGestureTemplateCount}\n状态：{(viewData.CustomGestureValidationActive ? "持续监测中" : "已暂停")}\n最近命中：{viewData.CustomGestureLastMatchedName}\n评分：{(float.IsPositiveInfinity(score) ? "--" : score.ToString("F3"))}\n\n点“目标”切模板，点“开始持续验证”后直接做目标手势。";
        }

        private static string BuildCustomGestureStageText(SpellGuardFlowViewData viewData)
        {
            if (viewData.CustomGestureRecording)
            {
                return "正在采集单个样本";
            }

            if (viewData.CustomGestureSampleCount >= viewData.CustomGestureRequiredSamples)
            {
                return string.IsNullOrWhiteSpace(viewData.CustomGestureTemplateName) ? "等待命名" : "可保存为新模板";
            }

            return viewData.CustomGestureSampleCount <= 0 ? "配置并开始采样" : "继续补齐样本";
        }

        private static string BuildCustomGestureNextStepText(SpellGuardFlowViewData viewData)
        {
            if (viewData.CustomGestureRecording)
            {
                return "保持动作/完成动作，结束后选择采用样本或重录样本。";
            }

            if (viewData.CustomGestureSampleCount < viewData.CustomGestureRequiredSamples)
            {
                return "点击“录制样本”，重复采集同一个新手势的示范。";
            }

            if (string.IsNullOrWhiteSpace(viewData.CustomGestureTemplateName))
            {
                return "在名称输入框给这个新手势命名。";
            }

            return "点击“保存模板”，再点击“加载模板/验证识别”进行验证。";
        }

        private static string FormatTemplateName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未命名（请在下方输入）" : value.Trim();
        }

        private string GetDeveloperInputModeButtonLabel(SpellGuard.InputSystem.GestureInputRouter.InputMode mode, string label)
        {
            return flowController != null && flowController.InputModeLabel == FormatInputModeLabel(mode) ? $"✓ {label}" : label;
        }

        private static string FormatInputModeLabel(SpellGuard.InputSystem.GestureInputRouter.InputMode mode)
        {
            return mode switch
            {
                SpellGuard.InputSystem.GestureInputRouter.InputMode.Mock => "Mock",
                SpellGuard.InputSystem.GestureInputRouter.InputMode.NativeMediapipe => "Native MediaPipe",
                SpellGuard.InputSystem.GestureInputRouter.InputMode.ExternalBridge => "ExternalBridge",
                _ => "Unknown"
            };
        }

        private void AddTrainingRegions(OverlayLayout layout)
        {
            AddRegion("pointer-check", "确认练习", MakeTrainingRect(layout, 0, 0));
            AddRegion("reset-training", "重置", MakeTrainingRect(layout, 1, 0));
            AddRegion("start-from-training", "完成训练并开始守卫", MakeTrainingRect(layout, 0, 1));
            AddRegion("menu", "返回主菜单", MakeTrainingRect(layout, 1, 1));
        }

        private void DrawTrainingRegions(OverlayLayout layout, SpellGuardFlowViewData viewData)
        {
            DrawRegion("pointer-check", "确认练习", MakeTrainingRect(layout, 0, 0));
            DrawRegion("reset-training", "重置", MakeTrainingRect(layout, 1, 0));
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
            if (viewData.DeveloperToolsEnabled)
            {
                return $"开发者工具\n录入：{viewData.CustomGestureKindLabel} · {viewData.CustomGestureDisplayName} · {viewData.CustomGestureTargetLabel} · {viewData.CustomGestureSampleCount}/{viewData.CustomGestureRequiredSamples}\n状态：{viewData.CustomGestureStatusText}\n最近命中：{viewData.CustomGestureLastMatchedName}";
            }

            var completion = viewData.TrainingComplete ? "已完成，可开始正式守卫" : "未完成，请补齐指向确认与三法术";
            var nextStep = viewData.TrainingComplete ? "可点‘开始正式守卫’进入战斗" : viewData.TrainingStepLabel;
            return $"训练：{completion}\n当前：{nextStep}\n火/冰/盾：{viewData.TrainingFireCasts}/{viewData.TrainingIceCasts}/{viewData.TrainingShieldCasts}";
        }

        private static string BuildResultsOverlayText(SpellGuardFlowViewData viewData)
        {
            var outcomeSummary = viewData.RunResult switch
            {
                SpellGuardRunResult.Victory => $"守卫成功：已达到目标得分 {viewData.TargetScoreToWin}",
                SpellGuardRunResult.Defeat => "守卫失败：生命耗尽，防线被突破",
                _ => "本局已结束"
            };

            return $"{outcomeSummary}\n得分：{viewData.CombatScore}/{viewData.TargetScoreToWin} · 最高分：{viewData.BestScore}\n命中：{viewData.CombatHits} · 施法：{viewData.CombatCasts} · 命中率：{viewData.HitRate}%";
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
            return viewData.DeveloperToolsEnabled
                ? "开发者实验室\n三维场景隐藏 · 游戏手势指令禁用\n自定义手势录入 · 性能/流程数据采集"
                : $"正式体验\n最高分：{viewData.BestScore}\n建议流程：玩法说明 → 开始守卫";
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
                panelWidth = flowController.DeveloperToolsEnabled
                    ? Mathf.Clamp(width * 0.64f, 620f, 860f)
                    : Mathf.Clamp(width * 0.48f, 500f, 700f);
                panelHeight = flowController.DeveloperToolsEnabled
                    ? Mathf.Clamp(height * 0.76f, 560f, 740f)
                    : Mathf.Clamp(height * 0.34f, 300f, 390f);
                panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            }

            var content = Shrink(panel, padding, padding + 18f * scale, padding, padding);
            var title = new Rect(content.x, content.y, content.width, 30f * scale);
            var bodyHeight = flowController.Screen == SpellGuardScreen.Training && flowController.DeveloperToolsEnabled
                ? Mathf.Max(120f, content.height * 0.18f)
                : Mathf.Max(40f, content.height * 0.48f);
            var body = new Rect(content.x, content.y + 34f * scale, content.width, bodyHeight);
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
            var rows = flowController.Screen == SpellGuardScreen.Training || flowController.Screen == SpellGuardScreen.Settings ? 2 : 1;
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

        private Rect MakeDevNameRect(OverlayLayout layout)
        {
            var spacing = Mathf.Clamp(10f * layout.Scale, 8f, 14f);
            var height = Mathf.Clamp(34f * layout.Scale, 30f, 40f);
            var buttonTop = MakeDevTrainingRect(layout, 0, 0).y;
            return new Rect(layout.Content.x, buttonTop - height - spacing * 1.6f, layout.Content.width, height);
        }

        private Rect MakeDevTrainingRect(OverlayLayout layout, int column, int row)
        {
            var spacing = Mathf.Clamp(10f * layout.Scale, 8f, 14f);
            var width = (layout.Content.width - spacing * 2f) / 3f;
            var height = Mathf.Clamp(42f * layout.Scale, 38f, 48f);
            var rows = 3;
            var baseY = layout.Panel.yMax - layout.Padding - height * rows - spacing * (rows - 1f);
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

        private sealed class ReferenceClipSequencePlayer
        {
            private static readonly System.Reflection.MethodInfo LoadImageMethod =
                Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                    ?.GetMethod("LoadImage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            private readonly List<Texture2D> frames = new List<Texture2D>();
            private string loadedTemplateId;
            private string loadedSampleId;
            private float lastFrameAt = -999f;
            private int frameIndex;
            private const float FrameInterval = 1f / 18f;

            public Texture2D CurrentTexture => frames.Count > 0 ? frames[Mathf.Clamp(frameIndex, 0, frames.Count - 1)] : null;
            public string StatusText { get; private set; } = "等待参考视频";

            public void SetTemplate(CustomGestureTemplate template)
            {
                if (template == null)
                {
                    Clear();
                    StatusText = "未选择模板";
                    return;
                }

                var templateId = template.GestureId ?? string.Empty;
                var sampleId = template.Samples != null && template.Samples.Count > 0 ? template.Samples[0]?.SampleId : string.Empty;
                if (string.Equals(templateId, loadedTemplateId, StringComparison.OrdinalIgnoreCase) && string.Equals(sampleId, loadedSampleId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Load(templateId, sampleId);
            }

            public void Update(float now)
            {
                if (frames.Count <= 1)
                {
                    return;
                }

                if (now - lastFrameAt < FrameInterval)
                {
                    return;
                }

                lastFrameAt = now;
                frameIndex = (frameIndex + 1) % frames.Count;
            }

            private void Load(string templateId, string sampleId)
            {
                Clear();
                loadedTemplateId = templateId;
                loadedSampleId = sampleId;

                var basePath = Path.Combine(Application.streamingAssetsPath, "CustomGestureReferenceVideos", templateId ?? string.Empty);
                if (!Directory.Exists(basePath))
                {
                    StatusText = string.IsNullOrWhiteSpace(sampleId)
                        ? $"未找到 {templateId} 的参考帧目录"
                        : $"未找到 {templateId} / {sampleId} 的参考帧目录";
                    return;
                }

                var files = Directory.GetFiles(basePath, "*.jpg", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                if (files.Length == 0)
                {
                    files = Directory.GetFiles(basePath, "*.png", SearchOption.TopDirectoryOnly);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                }

                for (var index = 0; index < files.Length; index++)
                {
                    var bytes = File.ReadAllBytes(files[index]);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!TryLoadImage(texture, bytes))
                    {
                        UnityEngine.Object.Destroy(texture);
                        continue;
                    }

                    frames.Add(texture);
                }

                frameIndex = 0;
                lastFrameAt = Time.unscaledTime;
                StatusText = frames.Count > 0
                    ? $"正在循环播放样本 {sampleId}，共 {frames.Count} 帧"
                    : $"未能读取 {templateId} 的参考帧";
            }

            private void Clear()
            {
                for (var index = 0; index < frames.Count; index++)
                {
                    if (frames[index] != null)
                    {
                        UnityEngine.Object.Destroy(frames[index]);
                    }
                }

                frames.Clear();
                frameIndex = 0;
                lastFrameAt = -999f;
            }

            private static bool TryLoadImage(Texture2D texture, byte[] bytes)
            {
                if (LoadImageMethod == null)
                {
                    return false;
                }

                var result = LoadImageMethod.Invoke(null, new object[] { texture, bytes, false });
                return result is bool loaded && loaded;
            }
        }

        private static Rect Shrink(Rect rect, float left, float top, float right, float bottom)
        {
            return new Rect(rect.x + left, rect.y + top, Mathf.Max(1f, rect.width - left - right), Mathf.Max(1f, rect.height - top - bottom));
        }
    }

}

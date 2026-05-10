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

        private readonly Region[] regions = new Region[12];
        private int regionCount;
        private string focusedKey;
        private string dwellKey;
        private float dwellStartedAt;
        private float backStartedAt;
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

            DrawCursor();
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
            RebuildRegions();

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            if (snapshot.HandPresent && snapshot.Gesture == GestureType.OpenPalm &&
                flowController.Screen != SpellGuardScreen.Menu &&
                flowController.Screen != SpellGuardScreen.Playing &&
                flowController.Screen != SpellGuardScreen.Training)
            {
                if (backStartedAt <= 0f)
                {
                    backStartedAt = Time.unscaledTime;
                }

                if (Time.unscaledTime - backStartedAt >= settings.MenuBackHoldSeconds)
                {
                    flowController.ReturnToMenu();
                }

                return;
            }

            backStartedAt = 0f;

            if (!snapshot.HandPresent)
            {
                focusedKey = null;
                dwellKey = null;
                regionCount = 0;
                return;
            }

            var cursor = new Vector2(snapshot.ViewportPosition.x * UnityEngine.Screen.width, (1f - snapshot.ViewportPosition.y) * UnityEngine.Screen.height);
            var region = GetFocusedRegion(cursor);
            if (region == null)
            {
                focusedKey = null;
                dwellKey = null;
                return;
            }

            focusedKey = region.Value.key;
            if (HandleMotionGesture(region.Value.key))
            {
                return;
            }

            if (dwellKey != focusedKey)
            {
                dwellKey = focusedKey;
                dwellStartedAt = Time.unscaledTime;
            }

            if (Time.unscaledTime - dwellStartedAt >= GetRequiredHoldSeconds(region.Value.key))
            {
                ActivateRegion(region.Value.key);
                dwellKey = null;
                focusedKey = null;
            }
        }

        private bool HandleMotionGesture(string focusedRegionKey)
        {
            if (inputProvider == null)
            {
                return false;
            }

            var command = inputProvider.CurrentGestureCommand;
            if (!command.IsValid || command.Kind != GestureCommandKind.Motion || command.TriggeredTime <= lastHandledMotionTime)
            {
                return false;
            }

            switch (command.MotionGesture)
            {
                case MotionGestureType.SwipeRightToLeft:
                case MotionGestureType.OpenPalmSlapRightToLeft:
                    lastHandledMotionTime = command.TriggeredTime;
                    if (flowController.Screen == SpellGuardScreen.Settings)
                    {
                        if (focusedRegionKey == "confirm")
                        {
                            flowController.CycleConfirmSetting();
                            dwellKey = null;
                            LogFlowEvent($"motion settings cycle confirm via {command.MotionGesture}");
                            return true;
                        }

                        if (focusedRegionKey == "difficulty")
                        {
                            flowController.CycleDifficultySetting();
                            dwellKey = null;
                            LogFlowEvent($"motion settings cycle difficulty via {command.MotionGesture}");
                            return true;
                        }

                        if (focusedRegionKey == "music-volume")
                        {
                            flowController.CycleMusicVolumeSetting();
                            dwellKey = null;
                            LogFlowEvent($"motion settings cycle music volume via {command.MotionGesture}");
                            return true;
                        }

                        if (focusedRegionKey == "sfx-volume")
                        {
                            flowController.CycleSfxVolumeSetting();
                            dwellKey = null;
                            LogFlowEvent($"motion settings cycle sfx volume via {command.MotionGesture}");
                            return true;
                        }
                    }

                    if (flowController.Screen == SpellGuardScreen.Settings || flowController.Screen == SpellGuardScreen.Tutorial || flowController.Screen == SpellGuardScreen.Results)
                    {
                        LogFlowEvent($"motion return to menu via {command.MotionGesture} from {flowController.Screen}");
                        flowController.ReturnToMenu();
                        return true;
                    }
                    break;

                case MotionGestureType.SwipeLeftToRight:
                case MotionGestureType.OpenPalmSlapLeftToRight:
                    if (flowController.Screen == SpellGuardScreen.Settings && (focusedRegionKey == "confirm" || focusedRegionKey == "difficulty" || focusedRegionKey == "music-volume" || focusedRegionKey == "sfx-volume"))
                    {
                        lastHandledMotionTime = command.TriggeredTime;
                        if (focusedRegionKey == "confirm")
                        {
                            flowController.CycleConfirmSetting();
                            LogFlowEvent($"motion settings cycle confirm via {command.MotionGesture}");
                        }
                        else
                        if (focusedRegionKey == "difficulty")
                        {
                            flowController.CycleDifficultySetting();
                            LogFlowEvent($"motion settings cycle difficulty via {command.MotionGesture}");
                        }
                        else if (focusedRegionKey == "music-volume")
                        {
                            flowController.CycleMusicVolumeSetting();
                            LogFlowEvent($"motion settings cycle music volume via {command.MotionGesture}");
                        }
                        else
                        {
                            flowController.CycleSfxVolumeSetting();
                            LogFlowEvent($"motion settings cycle sfx volume via {command.MotionGesture}");
                        }

                        dwellKey = null;
                        return true;
                    }
                    break;
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
                    else if (key == "training") flowController.StartTraining();
                    else if (key == "settings") flowController.OpenSettings();
                    else if (key == "tutorial") flowController.OpenTutorial();
                    break;
                case SpellGuardScreen.Settings:
                    if (key == "confirm") flowController.CycleConfirmSetting();
                    else if (key == "difficulty") flowController.CycleDifficultySetting();
                    else if (key == "music-volume") flowController.CycleMusicVolumeSetting();
                    else if (key == "sfx-volume") flowController.CycleSfxVolumeSetting();
                    else if (key == "back") flowController.ReturnToMenu();
                    break;
                case SpellGuardScreen.Tutorial:
                    if (key == "play") flowController.StartRun();
                    else if (key == "training") flowController.StartTraining();
                    else if (key == "back") flowController.ReturnToMenu();
                    break;
                case SpellGuardScreen.Training:
                    if (key == "pointer-check") flowController.RecordTrainingPointerCheck();
                    else if (key == "reset-training") flowController.ResetTrainingStats();
                    else if (key == "start-from-training") flowController.StartRunFromTraining();
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

        private Region? GetFocusedRegion(Vector2 cursor)
        {
            for (var index = 0; index < regionCount; index++)
            {
                if (regions[index].rect.Contains(cursor))
                {
                    return regions[index];
                }
            }

            return null;
        }

        private void RebuildRegions()
        {
            regionCount = 0;

            var layout = GetOverlayLayout();
            switch (flowController.Screen)
            {
                case SpellGuardScreen.Menu:
                    AddRegion("start", "开始守卫", MakeButtonRect(layout, 0, 0, 4));
                    AddRegion("tutorial", "上手教程", MakeButtonRect(layout, 1, 0, 4));
                    AddRegion("training", "手势训练场", MakeButtonRect(layout, 2, 0, 4));
                    AddRegion("settings", "调整设置", MakeButtonRect(layout, 3, 0, 4));
                    break;
                case SpellGuardScreen.Settings:
                    AddRegion("confirm", $"结印确认时长：{flowController.ConfirmLabel}", MakeButtonRect(layout, 0, 0, 5));
                    AddRegion("difficulty", $"敌人节奏：{flowController.DifficultyLabel}", MakeButtonRect(layout, 1, 0, 5));
                    AddRegion("music-volume", $"音乐音量：{flowController.MusicVolumeLabel}", MakeButtonRect(layout, 2, 0, 5));
                    AddRegion("sfx-volume", $"音效音量：{flowController.SfxVolumeLabel}", MakeButtonRect(layout, 3, 0, 5));
                    AddRegion("back", "返回主菜单", MakeButtonRect(layout, 4, 0, 5));
                    break;
                case SpellGuardScreen.Tutorial:
                    AddRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
                    AddRegion("training", "进入训练场", MakeButtonRect(layout, 1, 0, 3));
                    AddRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
                    break;
                case SpellGuardScreen.Training:
                    AddRegion("pointer-check", "指向确认练习", MakeTrainingRect(layout, 0, 0));
                    AddRegion("reset-training", "重置训练计数", MakeTrainingRect(layout, 1, 0));
                    AddRegion("start-from-training", "完成训练并开始守卫", MakeTrainingRect(layout, 0, 1));
                    AddRegion("menu", "返回主菜单", MakeTrainingRect(layout, 1, 1));
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
            DrawRegion("training", "手势训练场", MakeButtonRect(layout, 2, 0, 4));
            DrawRegion("settings", "调整设置", MakeButtonRect(layout, 3, 0, 4));
        }

        private void DrawSettings()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.07f, 0.09f, 0.14f, 0.95f), new Color(0.34f, 0.56f, 1f, 0.9f));
            GUI.Label(layout.Title, "战斗设置", overlayTitleStyle);
            GUI.Label(layout.Body, "调整施法确认、敌人节奏和音频音量，为正式战斗做准备。", overlayBodyStyle);
            DrawRegion("confirm", $"结印确认时长：{flowController.ConfirmLabel}", MakeButtonRect(layout, 0, 0, 5));
            DrawRegion("difficulty", $"敌人节奏：{flowController.DifficultyLabel}", MakeButtonRect(layout, 1, 0, 5));
            DrawRegion("music-volume", $"音乐音量：{flowController.MusicVolumeLabel}", MakeButtonRect(layout, 2, 0, 5));
            DrawRegion("sfx-volume", $"音效音量：{flowController.SfxVolumeLabel}", MakeButtonRect(layout, 3, 0, 5));
            DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 4, 0, 5));
        }

        private void DrawTutorial()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.06f, 0.08f, 0.13f, 0.95f), new Color(0.95f, 0.72f, 0.28f, 0.92f));
            GUI.Label(layout.Title, "上手教程", overlayTitleStyle);
            GUI.Label(layout.Body, "先理解流程，再进入训练场热身，准备好后进入战斗。\n• Point：移动焦点并触发菜单停留\n• Fist / Snap：火焰术，正面打击目标\n• V / 扇手：冰霜术与节奏施法\n• OpenPalm：护盾术，提供一次防护", overlayBodyStyle);
            GUI.Label(layout.Hint, flowController.HintText, overlayHintStyle);
            DrawRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
            DrawRegion("training", "进入训练场", MakeButtonRect(layout, 1, 0, 3));
            DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
        }

        private void DrawTraining()
        {
            var viewData = flowController.GetViewData();
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.05f, 0.08f, 0.13f, 0.94f), new Color(0.31f, 0.78f, 1f, 0.92f));
            GUI.Label(layout.Title, "训练场", overlayTitleStyle);
            GUI.Label(layout.Body, BuildTrainingOverlayText(viewData), overlayBodyStyle);
            GUI.Label(layout.Hint, viewData.HintText, overlayHintStyle);
            DrawRegion("pointer-check", "指向确认练习", MakeTrainingRect(layout, 0, 0));
            DrawRegion("reset-training", "重置训练计数", MakeTrainingRect(layout, 1, 0));
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
            var nextStep = viewData.TrainingComplete ? "可点‘开始正式守卫’进入战斗" : "完成指向确认和三法术后再开始";
            return $"训练目标：指向确认 → 火焰 → 冰霜 → 护盾 → 开始守卫\n状态：{completion}\n下一步：{nextStep}\n总训练：{viewData.TrainingCasts}\n指向确认：{viewData.TrainingPointerChecks}\n火焰/冰霜/护盾：{viewData.TrainingFireCasts}/{viewData.TrainingIceCasts}/{viewData.TrainingShieldCasts}\n最近一次：{viewData.LastTrainingSpell.ToChinese()}";
        }

        private static string BuildResultsOverlayText(SpellGuardFlowViewData viewData)
        {
            var outcomeSummary = viewData.RunResult switch
            {
                SpellGuardRunResult.Victory => $"守卫成功：已达到目标得分 {viewData.TargetScoreToWin}",
                SpellGuardRunResult.Defeat => "守卫失败：生命耗尽，防线被突破",
                _ => "本局已结束"
            };

            return $"{outcomeSummary}\n得分：{viewData.CombatScore}/{viewData.TargetScoreToWin}\n最高分：{viewData.BestScore}\n命中：{viewData.CombatHits}\n施法：{viewData.CombatCasts}\n命中率：{viewData.HitRate}%";
        }

        private static string BuildPausedOverlayText(SpellGuardFlowViewData viewData)
        {
            return $"当前战斗已暂停\n得分：{viewData.CombatScore}/{viewData.TargetScoreToWin}\n命中：{viewData.CombatHits}\n施法：{viewData.CombatCasts}\n按 Esc 可继续战斗";
        }

        private static string BuildMenuOverlayText(SpellGuardFlowViewData viewData)
        {
            var tutorialStatus = viewData.TutorialSeen ? "已阅读" : "未阅读";
            return $"先看教程，再去训练场热身，最后开始守卫。\n教程状态：{tutorialStatus} · 最高分：{viewData.BestScore}\n鼠标可直接点击按钮，手势仍保留停留确认。";
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
            var text = label;
            if (IsRecentlyActivated(key))
            {
                text = $"{label}   已确认";
            }
            if (focusedKey == key && dwellKey == key)
            {
                var progress = Mathf.Clamp01((Time.unscaledTime - dwellStartedAt) / GetRequiredHoldSeconds(key));
                text = $"{label}   {Mathf.RoundToInt(progress * 100f)}%";
            }

            var previousColor = GUI.color;
            var isFocused = focusedKey == key;
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
                panelWidth = Mathf.Clamp(width * 0.31f, 320f, 460f);
                panelHeight = Mathf.Clamp(height * 0.26f, 210f, 280f);
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
            var width = (layout.Content.width - spacing) * 0.5f;
            var height = Mathf.Clamp(42f * layout.Scale, 38f, 48f);
            var baseY = layout.Panel.yMax - layout.Padding - height * 2f - spacing;
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

        private void DrawCursor()
        {
            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            if (!snapshot.HandPresent)
            {
                return;
            }

            var x = snapshot.ViewportPosition.x * UnityEngine.Screen.width;
            var y = (1f - snapshot.ViewportPosition.y) * UnityEngine.Screen.height;
            var isPointing = snapshot.Gesture == GestureType.Point;

            var progress = 0f;
            if (!string.IsNullOrEmpty(dwellKey) && dwellKey == focusedKey)
            {
                var requiredHold = GetRequiredHoldSeconds(dwellKey);
                progress = Mathf.Clamp01((Time.unscaledTime - dwellStartedAt) / Mathf.Max(0.01f, requiredHold));
            }

            var previousColor = GUI.color;
            GUI.color = isPointing ? new Color(1f, 0.84f, 0.35f, 0.95f) : new Color(0.7f, 0.75f, 0.82f, 0.9f);
            GUI.DrawTexture(new Rect(x - 16f, y - 16f, 32f, 32f), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            GUI.DrawTexture(new Rect(x - 11f, y - 11f, 22f, 22f), Texture2D.whiteTexture);
            GUI.color = isPointing ? new Color(1f, 0.84f, 0.35f, 1f) : new Color(0.85f, 0.9f, 0.96f, 0.95f);
            GUI.DrawTexture(new Rect(x - 3f, y - 3f, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (progress > 0f)
            {
                var requiredHold = GetRequiredHoldSeconds(dwellKey);
                GUI.Box(new Rect(x + 16f, y - 8f, 120f, 22f), $"{Mathf.RoundToInt(progress * 100f)}% / {requiredHold:F1}s");
            }
            else if (!isPointing)
            {
                GUI.Box(new Rect(x + 16f, y - 8f, 110f, 22f), snapshot.Gesture.ToChinese());
            }
        }

        private float GetRequiredHoldSeconds(string key)
        {
            if (flowController.Screen == SpellGuardScreen.Training && (key == "menu" || key == "start-from-training"))
            {
                return flowController.TrainingMenuHoldSeconds;
            }

            return settings != null ? settings.MenuDwellSeconds : 0.8f;
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

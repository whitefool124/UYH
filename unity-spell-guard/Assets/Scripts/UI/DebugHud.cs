using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.Diagnostics;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.UI
{
    public class DebugHud : MonoBehaviour
    {
        private static readonly (int from, int to)[] HandConnections =
        {
            (0, 1), (1, 2), (2, 3), (3, 4),
            (0, 5), (5, 6), (6, 7), (7, 8),
            (5, 9), (9, 10), (10, 11), (11, 12),
            (9, 13), (13, 14), (14, 15), (15, 16),
            (13, 17), (17, 18), (18, 19), (19, 20),
            (0, 17)
        };

        private static readonly (int from, int to)[] PoseConnections =
        {
            (11, 12), (11, 13), (13, 15),
            (12, 14), (14, 16),
            (11, 23), (12, 24), (23, 24),
            (23, 25), (25, 27),
            (24, 26), (26, 28)
        };

        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private ExternalGestureBridgeProvider externalBridge;
        [SerializeField] private UdpGestureReceiver udpGestureReceiver;
        [SerializeField] private FpsGestureMotor motor;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private GameFlowManager gameFlow;
        [SerializeField] private SpellGuardFlowController flowController;
        [SerializeField] private GesturePerformanceMonitor performanceMonitor;

        private GUIStyle quickActionStyle;
        private GUIStyle quickActionPanelStyle;
        private GUIStyle quickActionLabelStyle;

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;
        private GUIStyle subTitleStyle;
        private GUIStyle accentStyle;
        private GUIStyle panelStyle;
        private float cachedStyleScale = -1f;

        private struct HudLayout
        {
            public Rect PrimaryPanel;
            public Rect SecondaryPanel;
            public Rect PreviewPanel;
            public Rect PreviewContent;
            public float Scale;
            public float Padding;
        }

        private void OnGUI()
        {
            var layout = GetLayout();
            EnsureStyles(layout.Scale);

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            var frame = inputProvider != null ? inputProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            var viewData = BuildViewData();
            DrawPrimaryHud(snapshot, frame, viewData, layout);
            DrawSecondaryHud(snapshot, frame, viewData, layout);
            DrawPreview(snapshot, layout);
            DrawQuickActions(viewData, layout);

        }

        private HudLayout GetLayout()
        {
            var width = Mathf.Max(1f, UnityEngine.Screen.width);
            var height = Mathf.Max(1f, UnityEngine.Screen.height);
            var scale = Mathf.Clamp(Mathf.Min(width / 1280f, height / 720f), 0.88f, 1.3f);
            var marginX = Mathf.Clamp(width * 0.022f, 16f, 36f);
            var marginY = Mathf.Clamp(height * 0.022f, 16f, 32f);
            var gap = Mathf.Clamp(12f * scale, 10f, 18f);
            var padding = Mathf.Clamp(16f * scale, 12f, 22f);

            var sideWidth = Mathf.Clamp(width * 0.33f, 320f, 440f);
            var primaryHeight = Mathf.Clamp(height * 0.25f, 196f, 258f);
            var secondaryHeight = Mathf.Clamp(height * 0.29f, 198f, 260f);
            var previewWidth = Mathf.Clamp(width * 0.27f, 280f, 420f);
            var previewHeight = Mathf.Clamp(previewWidth * 0.78f, 220f, 360f);

            var wideLayout = width >= 1180f;
            var primaryPanel = new Rect(marginX, marginY, sideWidth, primaryHeight);
            var secondaryPanel = new Rect(marginX, primaryPanel.yMax + gap, sideWidth, secondaryHeight);
            var previewPanel = wideLayout
                ? new Rect(width - marginX - previewWidth, marginY, previewWidth, previewHeight)
                : new Rect(marginX, secondaryPanel.yMax + gap, Mathf.Clamp(width - marginX * 2f, 280f, width - marginX * 2f), Mathf.Max(180f, height - secondaryPanel.yMax - gap - marginY));

            var previewContent = Shrink(previewPanel, padding, padding + 24f * scale, padding, padding + 10f * scale);

            return new HudLayout
            {
                PrimaryPanel = primaryPanel,
                SecondaryPanel = secondaryPanel,
                PreviewPanel = previewPanel,
                PreviewContent = previewContent,
                Scale = scale,
                Padding = padding,
            };
        }

        private void DrawPrimaryHud(GestureSnapshot snapshot, GestureFrame frame, SpellGuardHudViewData viewData, HudLayout layout)
        {
            DrawPanel(layout.PrimaryPanel, new Color(0.06f, 0.08f, 0.12f, 0.92f), new Color(0.95f, 0.68f, 0.25f, 0.96f));
            GUILayout.BeginArea(Shrink(layout.PrimaryPanel, layout.Padding, layout.Padding + 22f * layout.Scale, layout.Padding, layout.Padding));
            GUILayout.Label("SPELL GUARD", titleStyle);
            GUILayout.Label(viewData.ScreenLabel, subTitleStyle);
            GUILayout.Space(4f * layout.Scale);
            GUILayout.Label($"输入模式：{viewData.InputModeLabel}", accentStyle);
            GUILayout.Label($"动态状态：{viewData.MotionCaptureSignal}", labelStyle);
            GUILayout.Label($"当前手势：{snapshot.Gesture.ToChinese()} · 置信度 {snapshot.Confidence:F2}", labelStyle);
            GUILayout.Label($"运行时来源：{frame.Source} · 手数 {frame.HandCount}", labelStyle);
            GUILayout.Label($"施法反馈：{(spellCaster != null ? spellCaster.StatusText : "无")}", labelStyle);
            GUILayout.Label($"生命 {viewData.HealthText} · 护盾 {viewData.ShieldText} · 敌人 {viewData.EnemyText}", labelStyle);
            GUILayout.Label($"位移状态：{GetMovementStateText()}", labelStyle);

            if (gameFlow != null && gameFlow.GameOver)
            {
                GUILayout.Space(6f * layout.Scale);
                GUILayout.Label($"战斗结束 · {GetRunResultText(gameFlow.RunResult)}", accentStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawSecondaryHud(GestureSnapshot snapshot, GestureFrame frame, SpellGuardHudViewData viewData, HudLayout layout)
        {
            DrawPanel(layout.SecondaryPanel, new Color(0.06f, 0.08f, 0.12f, 0.9f), new Color(0.32f, 0.55f, 0.96f, 0.94f));
            GUILayout.BeginArea(Shrink(layout.SecondaryPanel, layout.Padding, layout.Padding + 22f * layout.Scale, layout.Padding, layout.Padding));
            GUILayout.Label("识别与调试信息", subTitleStyle);
            GUILayout.Label("F1 切换输入模式 · 左右摆手横移 · 上下摆手前后移 · Fist / V / Palm / Snap 施法", labelStyle);
            GUILayout.Space(4f * layout.Scale);
            GUILayout.Label($"手位：{snapshot.ViewportPosition:F2}", labelStyle);
            if (frame.HandCount > 0)
            {
                var primaryHand = frame.Hands[0];
                GUILayout.Label($"主手状态：#{primaryHand.TrackId} {primaryHand.StaticGesture.ToChinese()} · {primaryHand.Handedness}", labelStyle);
                GUILayout.Label($"主手掌心：{primaryHand.PalmCenter:F2}", labelStyle);
            }
            GUILayout.Label($"摄像头：{(webcamFeed != null ? webcamFeed.StatusText : "未绑定")}", labelStyle);
            GUILayout.Label($"设备：{(webcamFeed != null ? webcamFeed.ActiveDeviceName : "无")}", labelStyle);
            GUILayout.Label($"原生识别：{(nativeMediapipeProvider != null ? nativeMediapipeProvider.StatusText : "未绑定")}", labelStyle);
            GUILayout.Label($"识别桥：{(externalBridge != null ? externalBridge.BridgeStatus : "未绑定")}", labelStyle);
            GUILayout.Label($"桥接源：{(externalBridge != null ? externalBridge.SourceLabel : "未绑定")}", labelStyle);
            GUILayout.Label($"UDP：{(udpGestureReceiver != null ? udpGestureReceiver.StatusText : "未绑定")}", labelStyle);
            GUILayout.Label($"动态事件：{viewData.MotionGestureLabel}", labelStyle);
            GUILayout.Label($"Pose 点数：{viewData.PoseLandmarkCount}", labelStyle);
            DrawPerformanceLines();
            GUILayout.EndArea();
        }

        private void DrawPerformanceLines()
        {
            if (performanceMonitor == null)
            {
                GUILayout.Label("性能统计：未绑定", labelStyle);
                return;
            }

            var summary = performanceMonitor.CurrentSummary;
            GUILayout.Label($"性能：FPS {summary.AverageFps:F1} / P95 {summary.P95FrameMs:F1} ms", labelStyle);
            GUILayout.Label($"桥接延迟：avg {summary.AverageEstimatedLatencyMs:F1} ms / P95 {summary.P95EstimatedLatencyMs:F1} ms", labelStyle);
            GUILayout.Label($"实验记录：{(summary.IsRecording ? "Recording" : "Stopped")} {(string.IsNullOrWhiteSpace(summary.LastExportPath) ? string.Empty : summary.LastExportPath)}", labelStyle);
        }

        private SpellGuardHudViewData BuildViewData()
        {
            var screenStatus = flowController != null ? flowController.GetScreenStatus() : new SpellGuardRuntimeStatus("未绑定", "无可用流程状态");

            return new SpellGuardHudViewData(
                screenStatus.Title,
                GetInputModeLabel(),
                GetMotionCaptureSignal(),
                GetHealthText(),
                GetShieldText(),
                GetEnemyText(),
                GetMotionGestureLabel(),
                GetPoseLandmarkCount());
        }

        private string GetHealthText() => playerHealth != null ? playerHealth.CurrentHealth.ToString() : "0";
        private string GetShieldText() => playerHealth != null && playerHealth.ShieldActive ? "开启" : "关闭";
        private string GetEnemyText() => enemySpawner != null ? enemySpawner.AliveEnemies.Count.ToString() : "0";

        private string GetMovementStateText()
        {
            if (motor == null || !motor.IsStepInProgress)
            {
                return "待命";
            }

            return motor.CurrentStepDirection switch
            {
                FpsGestureMotor.DiscreteMoveDirection.Forward => "前进一步中",
                FpsGestureMotor.DiscreteMoveDirection.Backward => "后退一步中",
                FpsGestureMotor.DiscreteMoveDirection.Left => "左移一步中",
                FpsGestureMotor.DiscreteMoveDirection.Right => "右移一步中",
                _ => "位移中"
            };
        }

        private static string GetRunResultText(SpellGuardRunResult runResult)
        {
            return runResult switch
            {
                SpellGuardRunResult.Victory => "已达成目标分数，请在结果页继续",
                SpellGuardRunResult.Defeat => "生命耗尽，请在结果页重开或返回",
                _ => "请在结果页继续"
            };
        }

        private void DrawPreview(GestureSnapshot snapshot, HudLayout layout)
        {
            DrawPanel(layout.PreviewPanel, new Color(0.04f, 0.06f, 0.09f, 0.94f), new Color(0.85f, 0.65f, 0.25f, 0.92f));
            GUI.Label(new Rect(layout.PreviewPanel.x + layout.Padding, layout.PreviewPanel.y + 6f * layout.Scale, layout.PreviewPanel.width - layout.Padding * 2f, 24f * layout.Scale), "摄像头预览", subTitleStyle);
            var mirrorPreview = webcamFeed != null && webcamFeed.MirrorPreview;

            if (webcamFeed == null || webcamFeed.Texture == null)
            {
                GUI.Label(new Rect(layout.PreviewContent.x, layout.PreviewContent.y + layout.PreviewContent.height * 0.45f - 12f, layout.PreviewContent.width, 24f), "未绑定摄像头预览", labelStyle);
                return;
            }

            var textureRect = layout.PreviewContent;
            var tex = webcamFeed.Texture;

            if (webcamFeed.MirrorPreview)
            {
                var previousMatrix = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), new Vector2(textureRect.x + textureRect.width * 0.5f, textureRect.y + textureRect.height * 0.5f));
                GUI.DrawTexture(textureRect, tex, ScaleMode.ScaleToFit, false);
                GUI.matrix = previousMatrix;
            }
            else
            {
                GUI.DrawTexture(textureRect, tex, ScaleMode.ScaleToFit, false);
            }

            if (snapshot.HandPresent)
            {
                var marker = ToPreviewPoint(snapshot.ViewportPosition, textureRect, mirrorPreview);
                DrawHandSkeleton(textureRect, mirrorPreview);
                DrawPoseSkeleton(textureRect, mirrorPreview);
                GUI.color = Color.yellow;
                var markerSize = Mathf.Clamp(10f * layout.Scale, 8f, 14f);
                GUI.DrawTexture(new Rect(marker.x - markerSize * 0.5f, marker.y - markerSize * 0.5f, markerSize, markerSize), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else
            {
                DrawPoseSkeleton(textureRect, mirrorPreview);
            }

            DrawMotionCaptureBanner(textureRect);
        }

        private void DrawQuickActions(SpellGuardHudViewData viewData, HudLayout layout)
        {
            EnsureQuickActionStyles(layout.Scale);

            var width = Mathf.Clamp(layout.PrimaryPanel.width + layout.SecondaryPanel.width + layout.PreviewPanel.width + layout.Padding * 2f, 380f, UnityEngine.Screen.width - layout.Padding * 2f);
            var panelHeight = Mathf.Clamp(92f * layout.Scale, 82f, 104f);
            var panel = new Rect(layout.Padding, UnityEngine.Screen.height - layout.Padding - panelHeight, width, panelHeight);
            DrawPanel(panel, new Color(0.05f, 0.06f, 0.09f, 0.9f), new Color(0.78f, 0.76f, 0.28f, 0.9f));

            var content = Shrink(panel, 14f, 12f, 14f, 12f);
            GUI.Label(new Rect(content.x, content.y, content.width, 20f * layout.Scale), GetQuickActionTitle(viewData), quickActionLabelStyle);

            var buttonY = content.y + 26f * layout.Scale;
            var buttonHeight = Mathf.Clamp(32f * layout.Scale, 28f, 36f);
            var gap = Mathf.Clamp(8f * layout.Scale, 6f, 12f);
            var buttonCount = GetQuickActionCount();
            var buttonWidth = (content.width - gap * Mathf.Max(0, buttonCount - 1)) / buttonCount;

            var index = 0;
            DrawQuickActionButton(new Rect(content.x + index++ * (buttonWidth + gap), buttonY, buttonWidth, buttonHeight), GetPrimaryActionLabel(), GetPrimaryAction());
            if (flowController != null && flowController.Screen == SpellGuardScreen.Playing)
            {
                DrawQuickActionButton(new Rect(content.x + index++ * (buttonWidth + gap), buttonY, buttonWidth, buttonHeight), "暂停战斗", () => flowController.PauseRun());
            }
            else if (flowController != null && flowController.Screen == SpellGuardScreen.Paused)
            {
                DrawQuickActionButton(new Rect(content.x + index++ * (buttonWidth + gap), buttonY, buttonWidth, buttonHeight), "继续战斗", () => flowController.ResumeRun());
            }
            else if (flowController != null && flowController.Screen == SpellGuardScreen.Training)
            {
                DrawQuickActionButton(new Rect(content.x + index++ * (buttonWidth + gap), buttonY, buttonWidth, buttonHeight), flowController.TrainingComplete ? "开始正式守卫" : "完成训练后开始", () => flowController.StartRunFromTraining());
            }
            else if (flowController != null && flowController.Screen == SpellGuardScreen.Results)
            {
                DrawQuickActionButton(new Rect(content.x + index++ * (buttonWidth + gap), buttonY, buttonWidth, buttonHeight), "再来一局", () => flowController.StartRun());
            }

            DrawQuickActionButton(new Rect(content.x + (buttonCount - 1) * (buttonWidth + gap), buttonY, buttonWidth, buttonHeight), "返回主菜单", () => flowController?.ReturnToMenu());
        }

        private int GetQuickActionCount()
        {
            if (flowController == null)
            {
                return 2;
            }

            return flowController.Screen switch
            {
                SpellGuardScreen.Playing => 3,
                SpellGuardScreen.Paused => 3,
                SpellGuardScreen.Training => 3,
                SpellGuardScreen.Results => 3,
                _ => 2,
            };
        }

        private string GetQuickActionTitle(SpellGuardHudViewData viewData)
        {
            if (flowController == null)
            {
                return "流程快捷操作";
            }

            return flowController.Screen switch
            {
                SpellGuardScreen.Menu => $"流程快捷操作 · {viewData.ScreenLabel} · 可直接进入训练或开始守卫",
                SpellGuardScreen.Settings => $"流程快捷操作 · {viewData.ScreenLabel} · 可切换参数后返回",
                SpellGuardScreen.Tutorial => $"流程快捷操作 · {viewData.ScreenLabel} · 可直接进入训练或战斗",
                SpellGuardScreen.Training => $"流程快捷操作 · {viewData.ScreenLabel} · 先完成训练，再进入正式守卫",
                SpellGuardScreen.Playing => $"流程快捷操作 · {viewData.ScreenLabel} · 可随时暂停",
                SpellGuardScreen.Paused => $"流程快捷操作 · {viewData.ScreenLabel} · 可继续、重开或返回",
                SpellGuardScreen.Results => $"流程快捷操作 · {viewData.ScreenLabel} · 可再来一局或返回",
                _ => "流程快捷操作",
            };
        }

        private string GetPrimaryActionLabel()
        {
            if (flowController == null)
            {
                return "开始守卫";
            }

            return flowController.Screen switch
            {
                SpellGuardScreen.Menu => "开始守卫",
                SpellGuardScreen.Settings => "调整设置",
                SpellGuardScreen.Tutorial => "开始守卫",
                SpellGuardScreen.Training => "进入训练场",
                SpellGuardScreen.Playing => "战斗中",
                SpellGuardScreen.Paused => "暂停中",
                SpellGuardScreen.Results => "结果页",
                _ => "开始守卫",
            };
        }

        private System.Action GetPrimaryAction()
        {
            if (flowController == null)
            {
                return null;
            }

            return flowController.Screen switch
            {
                SpellGuardScreen.Menu => () => flowController.StartRun(),
                SpellGuardScreen.Settings => () => flowController.ReturnToMenu(),
                SpellGuardScreen.Tutorial => () => flowController.StartRun(),
                SpellGuardScreen.Training => () => flowController.StartRunFromTraining(),
                SpellGuardScreen.Playing => () => flowController.PauseRun(),
                SpellGuardScreen.Paused => () => flowController.ResumeRun(),
                SpellGuardScreen.Results => () => flowController.StartRun(),
                _ => () => flowController.StartRun(),
            };
        }

        private void DrawQuickActionButton(Rect rect, string label, System.Action action)
        {
            if (action == null)
            {
                return;
            }

            if (GUI.Button(rect, label, quickActionStyle))
            {
                action.Invoke();
            }
        }

        private void EnsureQuickActionStyles(float scale)
        {
            if (quickActionStyle != null)
            {
                return;
            }

            quickActionStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };

            quickActionPanelStyle = new GUIStyle(GUI.skin.box);
            quickActionLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.92f, 0.78f, 1f) }
            };
        }

        private void DrawHandSkeleton(Rect textureRect, bool mirrorPreview)
        {
            var landmarks = GetAvailableHandLandmarks();
            if (landmarks == null || landmarks.Count == 0)
            {
                return;
            }

            foreach (var (from, to) in HandConnections)
            {
                if (from >= landmarks.Count || to >= landmarks.Count)
                {
                    continue;
                }

                var start = ToPreviewPoint(landmarks[from], textureRect, mirrorPreview);
                var end = ToPreviewPoint(landmarks[to], textureRect, mirrorPreview);
                DrawLine(start, end, new Color(0.46f, 0.84f, 1f, 0.95f), 3f);
            }

            for (var index = 0; index < landmarks.Count; index++)
            {
                var point = ToPreviewPoint(landmarks[index], textureRect, mirrorPreview);
                GUI.color = index == 8 ? Color.yellow : new Color(0.3f, 1f, 0.72f, 0.95f);
                GUI.DrawTexture(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        private System.Collections.Generic.IReadOnlyList<Vector2> GetAvailableHandLandmarks()
        {
            if (nativeMediapipeProvider != null && nativeMediapipeProvider.HasHandLandmarks)
            {
                return nativeMediapipeProvider.HandLandmarks;
            }

            if (externalBridge != null && externalBridge.HasHandLandmarks)
            {
                return externalBridge.HandLandmarks;
            }

            return null;
        }

        private System.Collections.Generic.IReadOnlyList<Vector2> GetAvailablePoseLandmarks()
        {
            if (externalBridge != null && externalBridge.PoseLandmarks != null && externalBridge.PoseLandmarks.Count > 0)
            {
                return externalBridge.PoseLandmarks;
            }

            return null;
        }

        private void DrawPoseSkeleton(Rect textureRect, bool mirrorPreview)
        {
            var landmarks = GetAvailablePoseLandmarks();
            if (landmarks == null || landmarks.Count == 0)
            {
                return;
            }

            foreach (var (from, to) in PoseConnections)
            {
                if (from >= landmarks.Count || to >= landmarks.Count)
                {
                    continue;
                }

                var start = ToPreviewPoint(landmarks[from], textureRect, mirrorPreview);
                var end = ToPreviewPoint(landmarks[to], textureRect, mirrorPreview);
                DrawLine(start, end, new Color(1f, 0.68f, 0.28f, 0.9f), 2f);
            }

            for (var index = 0; index < landmarks.Count; index++)
            {
                var point = ToPreviewPoint(landmarks[index], textureRect, mirrorPreview);
                GUI.color = new Color(1f, 0.75f, 0.35f, 0.9f);
                GUI.DrawTexture(new Rect(point.x - 2.5f, point.y - 2.5f, 5f, 5f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        private string GetMotionGestureLabel()
        {
            if (inputProvider == null)
            {
                return "未绑定";
            }

            var command = inputProvider.CurrentGestureCommand;
            return command.IsValid && command.Kind == GestureCommandKind.Motion ? command.MotionGesture.ToChinese() : "无";
        }

        private string GetMotionCaptureSignal()
        {
            if (inputProvider == null)
            {
                return "未绑定";
            }

            var command = inputProvider.CurrentGestureCommand;
            return command.IsValid && command.Kind == GestureCommandKind.Motion ? $"已捕捉 {command.MotionGesture.ToChinese()}" : "等待动态手势";
        }

        private int GetPoseLandmarkCount()
        {
            var landmarks = GetAvailablePoseLandmarks();
            return landmarks?.Count ?? 0;
        }

        private void DrawMotionCaptureBanner(Rect textureRect)
        {
            if (inputProvider == null)
            {
                return;
            }

            var command = inputProvider.CurrentGestureCommand;
            if (!command.IsValid || command.Kind != GestureCommandKind.Motion)
            {
                return;
            }

            var bannerRect = new Rect(textureRect.x + 8f, textureRect.y + 8f, textureRect.width - 16f, 36f);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 0.45f, 0.12f, 0.92f);
            GUI.Box(bannerRect, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(bannerRect.x + 10f, bannerRect.y + 6f, bannerRect.width - 20f, bannerRect.height - 12f), $"已捕捉动态手势：{command.MotionGesture.ToChinese()}", subTitleStyle);
            GUI.color = previousColor;
        }

        private static Rect Shrink(Rect rect, float left, float top, float right, float bottom)
        {
            return new Rect(rect.x + left, rect.y + top, Mathf.Max(1f, rect.width - left - right), Mathf.Max(1f, rect.height - top - bottom));
        }

        private void DrawPanel(Rect rect, Color fillColor, Color accentColor)
        {
            var previousColor = GUI.color;
            GUI.color = fillColor;
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.color = accentColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static Vector2 ToPreviewPoint(Vector2 normalizedPoint, Rect rect, bool mirrorX)
        {
            var x = mirrorX ? 1f - normalizedPoint.x : normalizedPoint.x;
            return new Vector2(rect.x + x * rect.width, rect.y + (1f - normalizedPoint.y) * rect.height);
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            var matrix = GUI.matrix;
            var colorBackup = GUI.color;
            var angle = Vector3.Angle(end - start, Vector2.right);
            if (start.y > end.y)
            {
                angle = -angle;
            }

            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, (end - start).magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = colorBackup;
        }

        private void EnsureStyles(float scale)
        {
            if (labelStyle != null && Mathf.Abs(cachedStyleScale - scale) < 0.01f)
            {
                return;
            }

            cachedStyleScale = scale;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15f * scale),
                wordWrap = true,
                normal = { textColor = new Color(0.84f, 0.9f, 0.96f, 0.98f) }
            };

            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = Mathf.RoundToInt(25f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.97f, 1f, 1f) }
            };

            subTitleStyle = new GUIStyle(labelStyle)
            {
                fontSize = Mathf.RoundToInt(17f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.94f, 0.98f, 1f) }
            };

            accentStyle = new GUIStyle(labelStyle)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.84f, 0.46f, 0.98f) }
            };

            panelStyle = new GUIStyle(GUI.skin.box);
        }

        private string GetInputModeLabel()
        {
            if (inputRouter == null)
            {
                return "未绑定";
            }

            return inputRouter.Mode switch
            {
                GestureInputRouter.InputMode.Mock => "模拟输入",
                GestureInputRouter.InputMode.NativeMediapipe => "原生识别",
                GestureInputRouter.InputMode.ExternalBridge => "外部桥接",
                _ => "未绑定"
            };
        }

    }
}

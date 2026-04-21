using SpellGuard.Combat;
using SpellGuard.Core;
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

        public void Configure(
            GestureInputProviderBase provider,
            GestureInputRouter router,
            WebcamFeedController feed,
            NativeMediapipeGestureProvider nativeProvider,
            ExternalGestureBridgeProvider bridge,
            UdpGestureReceiver receiver,
            FpsGestureMotor fpsMotor,
            GestureSpellCaster caster,
            PlayerHealth health,
            EnemySpawner spawner,
            GameFlowManager flow,
            SpellGuardFlowController flowUi)
        {
            inputProvider = provider;
            inputRouter = router;
            webcamFeed = feed;
            nativeMediapipeProvider = nativeProvider;
            externalBridge = bridge;
            udpGestureReceiver = receiver;
            motor = fpsMotor;
            spellCaster = caster;
            playerHealth = health;
            enemySpawner = spawner;
            gameFlow = flow;
            flowController = flowUi;
        }

        private void OnGUI()
        {
            var layout = GetLayout();
            EnsureStyles(layout.Scale);

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            var frame = inputProvider != null ? inputProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            DrawPrimaryHud(snapshot, frame, layout);
            DrawSecondaryHud(snapshot, frame, layout);
            DrawPreview(snapshot, layout);

            if (flowController != null)
            {
                flowController.DrawOverlay();
            }
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

        private void DrawPrimaryHud(GestureSnapshot snapshot, GestureFrame frame, HudLayout layout)
        {
            DrawPanel(layout.PrimaryPanel, new Color(0.06f, 0.08f, 0.12f, 0.92f), new Color(0.95f, 0.68f, 0.25f, 0.96f));
            GUILayout.BeginArea(Shrink(layout.PrimaryPanel, layout.Padding, layout.Padding + 22f * layout.Scale, layout.Padding, layout.Padding));
            GUILayout.Label("SPELL GUARD", titleStyle);
            GUILayout.Label(GetScreenLabel(), subTitleStyle);
            GUILayout.Space(4f * layout.Scale);
            GUILayout.Label($"输入模式：{GetInputModeLabel()}", accentStyle);
            GUILayout.Label($"动态状态：{GetMotionCaptureSignal()}", labelStyle);
            GUILayout.Label($"当前手势：{snapshot.Gesture.ToChinese()} · 置信度 {snapshot.Confidence:F2}", labelStyle);
            GUILayout.Label($"运行时来源：{frame.Source} · 手数 {frame.HandCount}", labelStyle);
            GUILayout.Label($"施法反馈：{(spellCaster != null ? spellCaster.StatusText : "无")}", labelStyle);
            GUILayout.Label($"生命 {GetHealthText()} · 护盾 {GetShieldText()} · 敌人 {GetEnemyText()}", labelStyle);
            GUILayout.Label($"前进状态：{(motor != null && motor.IsMovingForward ? "推进中" : "待命")}", labelStyle);

            if (gameFlow != null && gameFlow.GameOver)
            {
                GUILayout.Space(6f * layout.Scale);
                GUILayout.Label("战斗结束 · 按 R 立即重开", accentStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawSecondaryHud(GestureSnapshot snapshot, GestureFrame frame, HudLayout layout)
        {
            DrawPanel(layout.SecondaryPanel, new Color(0.06f, 0.08f, 0.12f, 0.9f), new Color(0.32f, 0.55f, 0.96f, 0.94f));
            GUILayout.BeginArea(Shrink(layout.SecondaryPanel, layout.Padding, layout.Padding + 22f * layout.Scale, layout.Padding, layout.Padding));
            GUILayout.Label("识别与调试信息", subTitleStyle);
            GUILayout.Label("F1 切换输入模式 · Point 转向前进 · Fist / V / Palm / Snap / 扇手施法", labelStyle);
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
            GUILayout.Label($"动态事件：{GetMotionGestureLabel()}", labelStyle);
            GUILayout.Label($"Pose 点数：{GetPoseLandmarkCount()}", labelStyle);
            GUILayout.EndArea();
        }

        private string GetHealthText() => playerHealth != null ? playerHealth.CurrentHealth.ToString() : "0";
        private string GetShieldText() => playerHealth != null && playerHealth.ShieldActive ? "开启" : "关闭";
        private string GetEnemyText() => enemySpawner != null ? enemySpawner.AliveEnemies.Count.ToString() : "0";

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

        private string GetScreenLabel()
        {
            if (flowController == null)
            {
                return "未绑定";
            }

            return flowController.Screen switch
            {
                SpellGuardScreen.Menu => "主菜单",
                SpellGuardScreen.Settings => "设置",
                SpellGuardScreen.Tutorial => "教程",
                SpellGuardScreen.Training => "训练场",
                SpellGuardScreen.Playing => "战斗中",
                SpellGuardScreen.Results => "结果页",
                _ => "未绑定"
            };
        }
    }
}

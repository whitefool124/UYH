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
        [SerializeField] private Rect previewRect = new Rect(560f, 18f, 320f, 240f);

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

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
            EnsureStyles();

            GUILayout.BeginArea(new Rect(18f, 18f, 520f, 420f), GUI.skin.box);
            GUILayout.Label("符印守卫 Unity MVP", titleStyle);

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            GUILayout.Label($"输入模式：{GetInputModeLabel()}", labelStyle);
            GUILayout.Label($"手部检测：{snapshot.HandPresent}", labelStyle);
            GUILayout.Label($"当前手势：{snapshot.Gesture.ToChinese()}", labelStyle);
            GUILayout.Label($"手位：{snapshot.ViewportPosition:F2}", labelStyle);
            GUILayout.Label($"置信度：{snapshot.Confidence:F2}", labelStyle);
            GUILayout.Label($"前进状态：{(motor != null && motor.IsMovingForward ? "前进中" : "停止")}", labelStyle);
            GUILayout.Label($"施法状态：{(spellCaster != null ? spellCaster.StatusText : "无")}", labelStyle);
            GUILayout.Label($"护盾：{(playerHealth != null && playerHealth.ShieldActive ? "开启" : "关闭")}", labelStyle);
            GUILayout.Label($"生命：{(playerHealth != null ? playerHealth.CurrentHealth : 0)}", labelStyle);
            GUILayout.Label($"敌人：{(enemySpawner != null ? enemySpawner.AliveEnemies.Count : 0)}", labelStyle);
            GUILayout.Label($"界面：{GetScreenLabel()}", labelStyle);
            GUILayout.Label($"摄像头：{(webcamFeed != null ? webcamFeed.StatusText : "未绑定")}", labelStyle);
            GUILayout.Label($"设备：{(webcamFeed != null ? webcamFeed.ActiveDeviceName : "无")}", labelStyle);
            GUILayout.Label($"原生识别：{(nativeMediapipeProvider != null ? nativeMediapipeProvider.StatusText : "未绑定")}", labelStyle);
            GUILayout.Label($"识别桥：{(externalBridge != null ? externalBridge.BridgeStatus : "未绑定")}", labelStyle);
            GUILayout.Label($"桥接源：{(externalBridge != null ? externalBridge.SourceLabel : "未绑定")}", labelStyle);
            GUILayout.Label($"UDP：{(udpGestureReceiver != null ? udpGestureReceiver.StatusText : "未绑定")}", labelStyle);
            GUILayout.Label($"动态事件：{GetMotionGestureLabel()}", labelStyle);
            GUILayout.Label($"动态捕捉：{GetMotionCaptureSignal()}", titleStyle);
            GUILayout.Label($"Pose 点数：{GetPoseLandmarkCount()}", labelStyle);

            GUILayout.Space(10f);
            GUILayout.Label("F1 切换输入模式：Mock / NativeMediapipe / ExternalBridge", labelStyle);
            GUILayout.Label("Mock 输入：Tab 检测手 / 1 Point / 2 Fist / 3 V / 4 Palm / IJKL 移动虚拟手", labelStyle);
            GUILayout.Label("原生优先：导入 MediaPipe Unity 插件后由 NativeMediapipeProvider 在 Unity 内直接驱动玩法。", labelStyle);
            GUILayout.Label("兼容方案：ExternalGestureBridgeProvider 仍可接外部识别结果。", labelStyle);
            GUILayout.Label("离线测试：先切到 ExternalBridge，再运行 bridge/start_offline_gesture_test.py --video <本地视频路径>。", labelStyle);
            GUILayout.Label("Point 控制转向，Point 抬高到屏幕上部时前进；Fist/V/Palm 分别施放火焰/冰霜/护盾。", labelStyle);

            if (flowController != null)
            {
                GUILayout.Space(10f);
                GUILayout.Label(flowController.BuildOverlayText(), labelStyle);
            }

            if (gameFlow != null && gameFlow.GameOver)
            {
                GUILayout.Space(12f);
                GUILayout.Label("游戏结束：按 R 重开场景", titleStyle);
            }

            GUILayout.EndArea();

            DrawPreview(snapshot);

            if (flowController != null)
            {
                flowController.DrawOverlay();
            }
        }

        private void DrawPreview(GestureSnapshot snapshot)
        {
            GUI.Box(previewRect, "摄像头预览");

            if (webcamFeed == null || webcamFeed.Texture == null)
            {
                GUI.Label(new Rect(previewRect.x + 12f, previewRect.y + 28f, previewRect.width - 24f, 24f), "未绑定摄像头预览", labelStyle);
                return;
            }

            var textureRect = new Rect(previewRect.x + 8f, previewRect.y + 28f, previewRect.width - 16f, previewRect.height - 56f);
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
                var marker = new Vector2(textureRect.x + snapshot.ViewportPosition.x * textureRect.width, textureRect.y + (1f - snapshot.ViewportPosition.y) * textureRect.height);
                DrawHandSkeleton(textureRect);
                DrawPoseSkeleton(textureRect);
                GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(marker.x - 6f, marker.y - 6f, 12f, 12f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else
            {
                DrawPoseSkeleton(textureRect);
            }

            DrawMotionCaptureBanner(textureRect);
        }

        private void DrawHandSkeleton(Rect textureRect)
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

                var start = ToPreviewPoint(landmarks[from], textureRect);
                var end = ToPreviewPoint(landmarks[to], textureRect);
                DrawLine(start, end, new Color(0.46f, 0.84f, 1f, 0.95f), 3f);
            }

            for (var index = 0; index < landmarks.Count; index++)
            {
                var point = ToPreviewPoint(landmarks[index], textureRect);
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

        private void DrawPoseSkeleton(Rect textureRect)
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

                var start = ToPreviewPoint(landmarks[from], textureRect);
                var end = ToPreviewPoint(landmarks[to], textureRect);
                DrawLine(start, end, new Color(1f, 0.68f, 0.28f, 0.9f), 2f);
            }

            for (var index = 0; index < landmarks.Count; index++)
            {
                var point = ToPreviewPoint(landmarks[index], textureRect);
                GUI.color = new Color(1f, 0.75f, 0.35f, 0.9f);
                GUI.DrawTexture(new Rect(point.x - 2f, point.y - 2f, 4f, 4f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        private string GetMotionGestureLabel()
        {
            if (externalBridge == null)
            {
                return "未绑定";
            }

            var motion = externalBridge.LatestMotionGesture;
            return motion.IsValid ? motion.Gesture.ToChinese() : "无";
        }

        private string GetMotionCaptureSignal()
        {
            if (externalBridge == null)
            {
                return "未绑定";
            }

            var motion = externalBridge.LatestMotionGesture;
            return motion.IsValid ? $"已捕捉 {motion.Gesture.ToChinese()}" : "等待动态手势";
        }

        private int GetPoseLandmarkCount()
        {
            var landmarks = GetAvailablePoseLandmarks();
            return landmarks?.Count ?? 0;
        }

        private void DrawMotionCaptureBanner(Rect textureRect)
        {
            if (externalBridge == null)
            {
                return;
            }

            var motion = externalBridge.LatestMotionGesture;
            if (!motion.IsValid)
            {
                return;
            }

            var bannerRect = new Rect(textureRect.x + 8f, textureRect.y + 8f, textureRect.width - 16f, 34f);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 0.45f, 0.12f, 0.92f);
            GUI.Box(bannerRect, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(bannerRect.x + 10f, bannerRect.y + 6f, bannerRect.width - 20f, bannerRect.height - 12f), $"已捕捉动态手势：{motion.Gesture.ToChinese()}", titleStyle);
            GUI.color = previousColor;
        }

        private static Vector2 ToPreviewPoint(Vector2 normalizedPoint, Rect rect)
        {
            return new Vector2(rect.x + normalizedPoint.x * rect.width, rect.y + (1f - normalizedPoint.y) * rect.height);
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

        private void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true
            };

            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
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

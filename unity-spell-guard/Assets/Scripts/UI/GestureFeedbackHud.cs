using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.UI
{
    public class GestureFeedbackHud : MonoBehaviour
    {
        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private SpellGuardFlowController flowController;
        [SerializeField] private bool visible = true;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle chipStyle;
        private GUIStyle bigSignalStyle;
        private GUIStyle trackerStyle;
        private float cachedScale = -1f;
        private MotionGestureType lastMotion = MotionGestureType.None;
        private float lastMotionAt = -999f;
        private string lastSpellStatus = string.Empty;
        private float lastSpellStatusAt = -999f;
        private Vector2 smoothedHandViewport = new Vector2(0.5f, 0.5f);
        private bool hasSmoothedHandViewport;

        public void Configure(
            GestureInputProviderBase provider,
            GestureSpellCaster caster,
            PlayerHealth health,
            EnemySpawner spawner,
            SpellGuardFlowController controller)
        {
            inputProvider = provider;
            spellCaster = caster;
            playerHealth = health;
            enemySpawner = spawner;
            flowController = controller;
        }

        private void Update()
        {
            if (inputProvider != null)
            {
                var motion = inputProvider.CurrentMotionGesture;
                if (motion.IsValid && motion.TriggeredTime > lastMotionAt)
                {
                    lastMotion = motion.Gesture;
                    lastMotionAt = motion.TriggeredTime;
                }
            }

            if (spellCaster != null && spellCaster.StatusText != lastSpellStatus)
            {
                lastSpellStatus = spellCaster.StatusText;
                lastSpellStatusAt = Time.unscaledTime;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            var scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.82f, 1.18f);
            EnsureStyles(scale);

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            DrawTopGestureBanner(snapshot, scale);
            DrawHandCaptureIndicator(scale);
            DrawBottomFeedbackStrip(snapshot, scale);
            DrawPulseOverlays(scale);
        }

        private void DrawTopGestureBanner(GestureSnapshot snapshot, float scale)
        {
            var width = Mathf.Clamp(Screen.width * 0.48f, 520f, 760f);
            var height = Mathf.Clamp(92f * scale, 82f, 110f);
            var rect = new Rect((Screen.width - width) * 0.5f, Mathf.Clamp(16f * scale, 12f, 24f), width, height);
            var pulse = GetRecentMotionPulse();
            var border = Color.Lerp(new Color(0.28f, 0.72f, 1f, 0.85f), GetMotionColor(lastMotion), pulse);
            DrawPanel(rect, new Color(0.035f, 0.045f, 0.07f, 0.88f), border);

            var left = new Rect(rect.x + 16f * scale, rect.y + 12f * scale, rect.width * 0.42f, rect.height - 24f * scale);
            var right = new Rect(rect.x + rect.width * 0.46f, rect.y + 12f * scale, rect.width * 0.5f, rect.height - 24f * scale);

            GUI.Label(new Rect(left.x, left.y, left.width, 22f * scale), "\u5f53\u524d\u624b\u52bf", smallStyle);
            GUI.Label(new Rect(left.x, left.y + 24f * scale, left.width, 42f * scale), snapshot.HandPresent ? snapshot.Gesture.ToChinese() : "\u672a\u68c0\u6d4b\u5230\u624b", bigSignalStyle);

            GUI.Label(new Rect(right.x, right.y, right.width, 22f * scale), "\u52a8\u6001\u8f68\u8ff9", smallStyle);
            var motionLabel = Time.time - lastMotionAt <= 1.1f ? FormatMotion(lastMotion) : "\u7b49\u5f85\u52a8\u4f5c";
            GUI.color = Color.Lerp(Color.white, GetMotionColor(lastMotion), pulse);
            GUI.Label(new Rect(right.x, right.y + 24f * scale, right.width, 42f * scale), motionLabel, bigSignalStyle);
            GUI.color = Color.white;

            DrawConfidenceBar(new Rect(left.x, rect.yMax - 14f * scale, rect.width - 32f * scale, 5f * scale), snapshot.HandPresent ? snapshot.Confidence : 0f, scale);
        }

        private void DrawHandCaptureIndicator(float scale)
        {
            var frame = inputProvider != null ? inputProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            var hand = frame.PrimaryHand;
            var isTracked = hand.IsTracked;
            var target = isTracked ? ClampViewport(hand.PalmCenter) : new Vector2(0.5f, 0.5f);
            if (!hasSmoothedHandViewport)
            {
                smoothedHandViewport = target;
                hasSmoothedHandViewport = true;
            }
            else
            {
                var smoothing = isTracked ? 0.36f : 0.12f;
                smoothedHandViewport = Vector2.Lerp(smoothedHandViewport, target, smoothing);
            }

            var width = Mathf.Clamp(Screen.width * 0.28f, 260f, 360f);
            var height = Mathf.Clamp(width * 0.62f, 160f, 220f);
            var rect = new Rect(
                Screen.width - width - Mathf.Clamp(18f * scale, 12f, 28f),
                Mathf.Clamp(126f * scale, 104f, 150f),
                width,
                height);
            var border = isTracked ? new Color(0.35f, 0.95f, 0.72f, 0.82f) : new Color(0.62f, 0.66f, 0.74f, 0.54f);
            DrawPanel(rect, new Color(0.025f, 0.035f, 0.055f, 0.78f), border);

            var plot = new Rect(rect.x + 12f * scale, rect.y + 30f * scale, rect.width - 24f * scale, rect.height - 44f * scale);
            DrawCaptureGrid(plot, scale, isTracked);

            var point = new Vector2(
                Mathf.Lerp(plot.x, plot.xMax, smoothedHandViewport.x),
                Mathf.Lerp(plot.y, plot.yMax, 1f - smoothedHandViewport.y));
            DrawCapturePoint(point, scale, isTracked);

            var label = isTracked
                ? $"\u624b\u90e8\u6355\u6349\u70b9  x:{hand.PalmCenter.x:0.00}  y:{hand.PalmCenter.y:0.00}"
                : "\u624b\u90e8\u6355\u6349\u70b9  \u672a\u6355\u6349";
            GUI.color = isTracked ? Color.white : new Color(0.78f, 0.82f, 0.9f, 0.72f);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 7f * scale, rect.width - 24f * scale, 20f * scale), label, trackerStyle);
            GUI.color = Color.white;
        }

        private static Vector2 ClampViewport(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        private static void DrawCaptureGrid(Rect rect, float scale, bool isTracked)
        {
            var color = isTracked ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.08f);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.center.y, rect.width, Mathf.Max(1f, scale)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.center.x, rect.y, Mathf.Max(1f, scale), rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, scale)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - Mathf.Max(1f, scale), rect.width, Mathf.Max(1f, scale)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, Mathf.Max(1f, scale), rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - Mathf.Max(1f, scale), rect.y, Mathf.Max(1f, scale), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawCapturePoint(Vector2 point, float scale, bool isTracked)
        {
            var size = isTracked ? 16f * scale : 10f * scale;
            var pulse = isTracked ? 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.12f : 1f;
            var outer = size * 1.9f * pulse;
            GUI.color = isTracked ? new Color(0.35f, 0.95f, 0.72f, 0.22f) : new Color(0.78f, 0.82f, 0.9f, 0.16f);
            GUI.DrawTexture(new Rect(point.x - outer * 0.5f, point.y - outer * 0.5f, outer, outer), Texture2D.whiteTexture);
            GUI.color = isTracked ? new Color(0.35f, 0.95f, 0.72f, 0.96f) : new Color(0.78f, 0.82f, 0.9f, 0.65f);
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawBottomFeedbackStrip(GestureSnapshot snapshot, float scale)
        {
            var width = Mathf.Clamp(Screen.width * 0.68f, 720f, 980f);
            var height = Mathf.Clamp(92f * scale, 78f, 108f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - Mathf.Clamp(18f * scale, 12f, 26f), width, height);
            DrawPanel(rect, new Color(0.04f, 0.045f, 0.065f, 0.9f), new Color(0.95f, 0.68f, 0.25f, 0.8f));

            var padding = 14f * scale;
            var status = spellCaster != null ? spellCaster.StatusText : "\u7b49\u5f85\u65bd\u6cd5";
            var prompt = spellCaster != null ? spellCaster.SpellPromptText : "\u672a\u7ed1\u5b9a\u65bd\u6cd5\u5668";
            GUI.Label(new Rect(rect.x + padding, rect.y + 9f * scale, rect.width - padding * 2f, 24f * scale), status, titleStyle);
            GUI.Label(new Rect(rect.x + padding, rect.y + 35f * scale, rect.width - padding * 2f, 20f * scale), prompt, labelStyle);

            var chipY = rect.y + rect.height - 30f * scale;
            DrawChip(new Rect(rect.x + padding, chipY, 150f * scale, 22f * scale), "\u706b\u7130", IsRecentFire() ? new Color(1f, 0.36f, 0.12f) : new Color(0.22f, 0.24f, 0.28f));
            DrawChip(new Rect(rect.x + padding + 160f * scale, chipY, 150f * scale, 22f * scale), "\u51b0\u971c", IsRecentIce() ? new Color(0.36f, 0.82f, 1f) : new Color(0.22f, 0.24f, 0.28f));
            DrawChip(new Rect(rect.x + padding + 320f * scale, chipY, 170f * scale, 22f * scale), "\u62a4\u76fe\u53cd\u51fb", IsRecentShieldCounter() ? new Color(0.45f, 0.72f, 1f) : new Color(0.22f, 0.24f, 0.28f));

            var rightText = BuildRuntimeText(snapshot);
            GUI.Label(new Rect(rect.xMax - 260f * scale, chipY - 1f * scale, 250f * scale, 24f * scale), rightText, smallStyle);
        }

        private void DrawPulseOverlays(float scale)
        {
            var motionPulse = GetRecentMotionPulse();
            if (motionPulse > 0f)
            {
                var size = Mathf.Lerp(92f, 180f, 1f - motionPulse) * scale;
                var rect = new Rect((Screen.width - size) * 0.5f, Screen.height * 0.34f - size * 0.5f, size, size);
                GUI.color = new Color(GetMotionColor(lastMotion).r, GetMotionColor(lastMotion).g, GetMotionColor(lastMotion).b, 0.16f * motionPulse);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (IsRecentShieldCounter())
            {
                var pulse = Mathf.Clamp01(1f - (Time.unscaledTime - lastSpellStatusAt) / 0.9f);
                var border = Mathf.Lerp(10f, 28f, 1f - pulse) * scale;
                GUI.color = new Color(0.45f, 0.72f, 1f, 0.14f * pulse);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, border), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, Screen.height - border, Screen.width, border), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, 0f, border, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width - border, 0f, border, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        private string BuildRuntimeText(GestureSnapshot snapshot)
        {
            var health = playerHealth != null ? playerHealth.CurrentHealth.ToString() : "-";
            var shield = playerHealth != null && playerHealth.ShieldActive ? "\u5f00" : "\u5173";
            var enemies = enemySpawner != null ? enemySpawner.AliveEnemies.Count.ToString() : "-";
            var screen = flowController != null ? flowController.Screen.ToString() : "-";
            return $"{screen}  HP {health}  Shield {shield}  Targets {enemies}";
        }

        private bool IsRecentFire()
        {
            return Time.unscaledTime - lastSpellStatusAt <= 0.8f && lastSpellStatus.Contains("\u706b\u7130");
        }

        private bool IsRecentShieldCounter()
        {
            return Time.unscaledTime - lastSpellStatusAt <= 1.0f && lastSpellStatus.Contains("\u62a4\u76fe\u53cd\u51fb");
        }

        private bool IsRecentIce()
        {
            return Time.unscaledTime - lastSpellStatusAt <= 0.8f && lastSpellStatus.Contains("\u51b0\u971c");
        }

        private bool IsRecentMotion(MotionGestureType gesture)
        {
            return lastMotion == gesture && Time.time - lastMotionAt <= 0.8f;
        }

        private float GetRecentMotionPulse()
        {
            return Mathf.Clamp01(1f - (Time.time - lastMotionAt) / 0.85f);
        }

        private static Color GetMotionColor(MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.Snap:
                case MotionGestureType.PointToFist:
                    return new Color(1f, 0.42f, 0.12f, 1f);
                case MotionGestureType.OpenPalmSlapLeftToRight:
                case MotionGestureType.OpenPalmSlapRightToLeft:
                    return new Color(0.45f, 0.72f, 1f, 1f);
                case MotionGestureType.SwipeLeftToRight:
                case MotionGestureType.SwipeRightToLeft:
                case MotionGestureType.SwipeBottomToTop:
                case MotionGestureType.SwipeTopToBottom:
                    return new Color(0.35f, 0.95f, 0.72f, 1f);
                default:
                    return new Color(0.8f, 0.82f, 0.88f, 1f);
            }
        }

        private static string FormatMotion(MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.SwipeLeftToRight:
                    return "\u6a2a\u626b \u2192";
                case MotionGestureType.SwipeRightToLeft:
                    return "\u6a2a\u626b \u2190";
                case MotionGestureType.SwipeBottomToTop:
                    return "\u4e0a\u626b \u2191";
                case MotionGestureType.SwipeTopToBottom:
                    return "\u4e0b\u626b \u2193";
                case MotionGestureType.OpenPalmSlapLeftToRight:
                    return "\u5f20\u638c\u53cd\u51fb \u2192";
                case MotionGestureType.OpenPalmSlapRightToLeft:
                    return "\u5f20\u638c\u53cd\u51fb \u2190";
                case MotionGestureType.Snap:
                    return "\u54cd\u6307\u5feb\u653b";
                case MotionGestureType.PointToFist:
                    return "\u6307\u5411\u63e1\u62f3";
                case MotionGestureType.BodyShiftLeft:
                    return "\u8eab\u4f53\u5de6\u79fb";
                case MotionGestureType.BodyShiftRight:
                    return "\u8eab\u4f53\u53f3\u79fb";
                default:
                    return "\u65e0";
            }
        }

        private void DrawConfidenceBar(Rect rect, float confidence, float scale)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.95f, 0.72f, 0.95f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(confidence), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawChip(Rect rect, string text, Color color)
        {
            DrawPanel(rect, new Color(color.r, color.g, color.b, 0.24f), new Color(color.r, color.g, color.b, 0.92f));
            GUI.Label(rect, text, chipStyle);
        }

        private void DrawPanel(Rect rect, Color fill, Color border)
        {
            GUI.color = fill;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = border;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void EnsureStyles(float scale)
        {
            if (Mathf.Approximately(cachedScale, scale) && titleStyle != null)
            {
                return;
            }

            cachedScale = scale;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.42f) },
                clipping = TextClipping.Clip
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                normal = { textColor = new Color(0.88f, 0.92f, 1f) },
                clipping = TextClipping.Clip
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                normal = { textColor = new Color(0.66f, 0.76f, 0.88f) },
                clipping = TextClipping.Clip
            };
            chipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };
            bigSignalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };
            trackerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };
        }
    }
}

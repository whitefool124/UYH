using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.Diagnostics;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.UI
{
    public class GestureFeedbackHud : MonoBehaviour
    {
        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private FpsGestureMotor fpsMotor;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private SpellGuardFlowController flowController;
        [SerializeField] private GesturePerformanceMonitor performanceMonitor;
        [SerializeField] private WebcamHealthProbe webcamHealthProbe;
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
        private Vector2 previousRawHandViewport = new Vector2(0.5f, 0.5f);
        private float lastRawHandViewportAt = -999f;
        private float lastIndicatorUpdateAt = -999f;
        private bool hasSmoothedHandViewport;

        public void Configure(
            GestureInputProviderBase provider,
            GestureSpellCaster caster,
            FpsGestureMotor motor,
            PlayerHealth health,
            EnemySpawner spawner,
            SpellGuardFlowController controller,
            GesturePerformanceMonitor monitor,
            WebcamHealthProbe probe = null)
        {
            inputProvider = provider;
            spellCaster = caster;
            fpsMotor = motor;
            playerHealth = health;
            enemySpawner = spawner;
            flowController = controller;
            performanceMonitor = monitor;
            webcamHealthProbe = probe;
        }

        private void Update()
        {
            EnsurePerformanceMonitorBound();

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

            SpellGuardRuntimeSkin.EnsureLoaded();
            var scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.82f, 1.18f);
            EnsureStyles(scale);

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            DrawTopGestureBanner(snapshot, scale);
            DrawHandCaptureIndicator(scale);
            DrawBottomFeedbackStrip(snapshot, scale);
            DrawPerformanceControls(scale);
            DrawPulseOverlays(scale);
        }

        private void DrawTopGestureBanner(GestureSnapshot snapshot, float scale)
        {
            var width = Mathf.Clamp(Screen.width * 0.48f, 520f, 760f);
            var height = Mathf.Clamp(92f * scale, 82f, 110f);
            var rect = new Rect((Screen.width - width) * 0.5f, Mathf.Clamp(16f * scale, 12f, 24f), width, height);
            var pulse = GetRecentMotionPulse();
            var border = Color.Lerp(SpellGuardRuntimeSkin.Cyan, GetMotionColor(lastMotion), pulse);
            DrawPanel(rect, new Color(0.035f, 0.045f, 0.07f, 0.82f), border);
            SpellGuardRuntimeSkin.DrawScanLines(rect, scale, border);

            var left = new Rect(rect.x + 16f * scale, rect.y + 12f * scale, rect.width * 0.42f, rect.height - 24f * scale);
            var right = new Rect(rect.x + rect.width * 0.46f, rect.y + 12f * scale, rect.width * 0.5f, rect.height - 24f * scale);

            GUI.Label(new Rect(left.x, left.y, left.width, 22f * scale), "\u5f53\u524d\u624b\u52bf", smallStyle);
            GUI.Label(new Rect(left.x, left.y + 24f * scale, left.width, 42f * scale), snapshot.HandPresent ? snapshot.Gesture.ToChinese() : "\u672a\u68c0\u6d4b\u5230\u624b", bigSignalStyle);
            var handTexture = SpellGuardRuntimeSkin.GetHandTexture(snapshot.HandPresent ? snapshot.Gesture.ToChinese() : string.Empty);
            if (handTexture != null)
            {
                var iconSize = 50f * scale;
                var iconRect = new Rect(left.xMax - iconSize - 8f * scale, left.y + 8f * scale, iconSize, iconSize);
                GUI.color = new Color(1f, 1f, 1f, 0.86f);
                GUI.DrawTexture(iconRect, handTexture, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }

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
            var target = ResolveCaptureIndicatorTarget(hand, isTracked, frame.Timestamp);
            if (!hasSmoothedHandViewport)
            {
                smoothedHandViewport = target;
                hasSmoothedHandViewport = true;
            }
            else
            {
                var deltaTime = lastIndicatorUpdateAt > 0f ? Mathf.Clamp(Time.unscaledTime - lastIndicatorUpdateAt, 0.001f, 0.05f) : Time.unscaledDeltaTime;
                smoothedHandViewport = SmoothCaptureIndicator(smoothedHandViewport, target, isTracked, deltaTime);
            }
            lastIndicatorUpdateAt = Time.unscaledTime;

            var point = new Vector2(
                Mathf.Lerp(0f, Screen.width, smoothedHandViewport.x),
                Mathf.Lerp(0f, Screen.height, smoothedHandViewport.y));
            DrawCaptureGuides(point, scale, isTracked);
            DrawCapturePoint(point, scale, isTracked);

            var labelPosition = ResolveTrackedDisplayPoint(hand);
            var label = isTracked
                ? $"\u624b\u90e8\u6355\u6349\u70b9  x:{labelPosition.x:0.00}  y:{labelPosition.y:0.00}"
                : "\u624b\u90e8\u6355\u6349\u70b9  \u672a\u6355\u6349";
            var labelWidth = 220f * scale;
            var labelHeight = 22f * scale;
            var labelX = Mathf.Clamp(point.x + 14f * scale, 8f * scale, Screen.width - labelWidth - 8f * scale);
            var labelY = Mathf.Clamp(point.y - labelHeight - 10f * scale, 118f * scale, Screen.height - 178f * scale);
            var labelRect = new Rect(labelX, labelY, labelWidth, labelHeight);
            GUI.color = isTracked ? new Color(0.025f, 0.035f, 0.055f, 0.76f) : new Color(0.025f, 0.035f, 0.055f, 0.48f);
            GUI.DrawTexture(labelRect, Texture2D.whiteTexture);
            GUI.color = isTracked ? Color.white : new Color(0.78f, 0.82f, 0.9f, 0.72f);
            GUI.Label(new Rect(labelRect.x + 8f * scale, labelRect.y + 1f * scale, labelRect.width - 16f * scale, labelRect.height), label, trackerStyle);
            GUI.color = Color.white;
        }

        private Vector2 ResolveCaptureIndicatorTarget(TrackedHandState hand, bool isTracked, float frameTimestamp)
        {
            if (!isTracked)
            {
                lastRawHandViewportAt = -999f;
                return hasSmoothedHandViewport ? smoothedHandViewport : new Vector2(0.5f, 0.5f);
            }

            var raw = ClampViewport(ResolveTrackedDisplayPoint(hand));
            var sampleTime = frameTimestamp > 0f ? frameTimestamp : Time.unscaledTime;
            if (lastRawHandViewportAt > 0f)
            {
                var jump = raw - previousRawHandViewport;
                if (jump.magnitude > 0.34f)
                {
                    raw = previousRawHandViewport + Vector2.ClampMagnitude(jump, 0.18f);
                }

                if (Mathf.Abs(raw.y - previousRawHandViewport.y) < 0.018f)
                {
                    raw.y = previousRawHandViewport.y;
                }
            }

            previousRawHandViewport = raw;
            lastRawHandViewportAt = sampleTime;
            return raw;
        }

        private static Vector2 SmoothCaptureIndicator(Vector2 current, Vector2 target, bool isTracked, float deltaTime)
        {
            if (!isTracked)
            {
                return current;
            }

            var delta = target - current;
            if (Mathf.Abs(delta.y) < 0.012f)
            {
                target.y = current.y;
                delta.y = 0f;
            }

            var response = delta.magnitude > 0.18f ? 9.5f : 6.5f;
            var lerp = 1f - Mathf.Exp(-response * Mathf.Clamp(deltaTime, 0.001f, 0.05f));
            var next = Vector2.Lerp(current, target, lerp);
            var maxStep = Mathf.Lerp(0.045f, 0.14f, Mathf.Clamp01(delta.magnitude / 0.45f));
            var step = next - current;
            if (step.magnitude > maxStep)
            {
                next = current + Vector2.ClampMagnitude(step, maxStep);
            }

            return ClampViewport(next);
        }

        private static Vector2 ResolveTrackedDisplayPoint(TrackedHandState hand)
        {
            return hand.Landmarks != null && hand.Landmarks.Length > 8
                ? hand.Landmarks[8]
                : hand.PalmCenter;
        }

        private static Vector2 ClampViewport(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        private static void DrawCaptureGuides(Vector2 point, float scale, bool isTracked)
        {
            var alpha = isTracked ? 0.12f : 0.06f;
            var thickness = Mathf.Max(1f, scale);
            GUI.color = new Color(0.35f, 0.95f, 0.72f, alpha);
            GUI.DrawTexture(new Rect(0f, point.y - thickness * 0.5f, Screen.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(point.x - thickness * 0.5f, 0f, thickness, Screen.height), Texture2D.whiteTexture);
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
            var height = Mathf.Clamp(132f * scale, 118f, 150f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - Mathf.Clamp(18f * scale, 12f, 26f), width, height);
            DrawPanel(rect, new Color(0.04f, 0.045f, 0.065f, 0.86f), new Color(0.95f, 0.68f, 0.25f, 0.82f));

            var padding = 14f * scale;
            SpellGuardRuntimeSkin.DrawDivider(new Rect(rect.x + padding, rect.y + 60f * scale, rect.width - padding * 2f, 2f * scale), new Color(0.35f, 0.88f, 1f, 0.34f));
            var status = spellCaster != null ? spellCaster.StatusText : "\u7b49\u5f85\u65bd\u6cd5";
            var prompt = spellCaster != null ? spellCaster.SpellPromptText : "\u672a\u7ed1\u5b9a\u65bd\u6cd5\u5668";
            GUI.Label(new Rect(rect.x + padding, rect.y + 9f * scale, rect.width - padding * 2f, 24f * scale), status, titleStyle);
            GUI.Label(new Rect(rect.x + padding, rect.y + 35f * scale, rect.width - padding * 2f, 20f * scale), prompt, labelStyle);

            var chipY = rect.y + rect.height - 30f * scale;
            DrawChip(new Rect(rect.x + padding, chipY, 150f * scale, 22f * scale), "\u706b\u7130", IsRecentFire() ? new Color(1f, 0.36f, 0.12f) : new Color(0.22f, 0.24f, 0.28f));
            DrawChip(new Rect(rect.x + padding + 160f * scale, chipY, 150f * scale, 22f * scale), "\u51b0\u971c", IsRecentIce() ? new Color(0.36f, 0.82f, 1f) : new Color(0.22f, 0.24f, 0.28f));
            DrawChip(new Rect(rect.x + padding + 320f * scale, chipY, 170f * scale, 22f * scale), "\u62a4\u76fe\u53cd\u51fb", IsRecentShieldCounter() ? new Color(0.45f, 0.72f, 1f) : new Color(0.22f, 0.24f, 0.28f));

            DrawStatusIcon(new Rect(rect.xMax - 306f * scale, chipY - 2f * scale, 24f * scale, 24f * scale), SpellGuardRuntimeSkin.IconHealth, playerHealth != null && playerHealth.CurrentHealth > 0 ? SpellGuardRuntimeSkin.Mint : SpellGuardRuntimeSkin.Red);
            var rightText = BuildRuntimeText(snapshot);
            GUI.Label(new Rect(rect.xMax - 260f * scale, chipY - 1f * scale, 250f * scale, 24f * scale), rightText, smallStyle);
            DrawCooldownStrip(new Rect(rect.x + padding, rect.yMax - 38f * scale, rect.width - padding * 2f, 30f * scale), scale);
        }

        private void DrawCooldownStrip(Rect rect, float scale)
        {
            var gap = 8f * scale;
            var itemWidth = (rect.width - gap * 4f) / 5f;
            DrawCooldownSlot(new Rect(rect.x, rect.y, itemWidth, rect.height), "\u6a2a\u79fb", fpsMotor != null ? fpsMotor.HorizontalMoveCooldownProgress : 1f, new Color(0.35f, 0.95f, 0.72f), scale);
            DrawCooldownSlot(new Rect(rect.x + (itemWidth + gap), rect.y, itemWidth, rect.height), "\u524d\u540e", fpsMotor != null ? fpsMotor.VerticalMoveCooldownProgress : 1f, new Color(0.35f, 0.72f, 1f), scale);
            DrawCooldownSlot(new Rect(rect.x + (itemWidth + gap) * 2f, rect.y, itemWidth, rect.height), "\u706b\u7130", spellCaster != null ? spellCaster.FireCooldownProgress : 1f, new Color(1f, 0.36f, 0.12f), scale);
            DrawCooldownSlot(new Rect(rect.x + (itemWidth + gap) * 3f, rect.y, itemWidth, rect.height), "\u51b0\u971c", spellCaster != null ? spellCaster.IceCooldownProgress : 1f, new Color(0.36f, 0.82f, 1f), scale);
            DrawCooldownSlot(new Rect(rect.x + (itemWidth + gap) * 4f, rect.y, itemWidth, rect.height), "\u62a4\u76fe", spellCaster != null ? spellCaster.ShieldCooldownProgress : 1f, new Color(0.55f, 0.68f, 1f), scale);
        }

        private void DrawCooldownSlot(Rect rect, string label, float progress, Color color, float scale)
        {
            progress = Mathf.Clamp01(progress);
            var ready = progress >= 0.999f;
            var fill = ready ? new Color(color.r, color.g, color.b, 0.16f) : new Color(0.04f, 0.045f, 0.065f, 0.88f);
            DrawPanel(rect, fill, ready ? new Color(color.r, color.g, color.b, 0.82f) : new Color(1f, 1f, 1f, 0.18f));

            var bar = new Rect(rect.x + 6f * scale, rect.yMax - 8f * scale, rect.width - 12f * scale, 4f * scale);
            SpellGuardRuntimeSkin.DrawProgress(bar, progress, color);
            GUI.color = ready ? Color.white : new Color(0.78f, 0.82f, 0.9f, 0.82f);
            GUI.Label(new Rect(rect.x + 6f * scale, rect.y + 3f * scale, rect.width - 12f * scale, 18f * scale), ready ? $"{label} OK" : $"{label} {Mathf.RoundToInt(progress * 100f)}%", smallStyle);
            GUI.color = Color.white;
        }

        private void DrawPerformanceControls(float scale)
        {
            EnsurePerformanceMonitorBound();

            var width = Mathf.Clamp(270f * scale, 240f, 310f);
            var height = Mathf.Clamp(106f * scale, 94f, 126f);
            var rect = new Rect(
                Screen.width - width - Mathf.Clamp(18f * scale, 12f, 26f),
                Mathf.Clamp(Screen.height * 0.46f, 122f * scale, Screen.height - height - 24f * scale),
                width,
                height);
            if (performanceMonitor == null)
            {
                DrawPanel(rect, new Color(0.035f, 0.04f, 0.06f, 0.88f), new Color(0.9f, 0.22f, 0.22f, 0.82f));
                GUI.Label(new Rect(rect.x + 8f * scale, rect.y + 8f * scale, rect.width - 16f * scale, 20f * scale), "\u6027\u80fd\u91c7\u96c6\u5668\u672a\u521d\u59cb\u5316", smallStyle);
                return;
            }

            var summary = performanceMonitor.CurrentSummary;
            var border = performanceMonitor.IsRecording ? new Color(1f, 0.68f, 0.22f, 0.9f) : new Color(0.42f, 0.52f, 0.66f, 0.72f);
            DrawPanel(rect, new Color(0.035f, 0.04f, 0.06f, 0.84f), border);

            var padding = 8f * scale;
            var status = performanceMonitor.IsRecording ? "\u6027\u80fd\u91c7\u96c6\u4e2d" : "\u6027\u80fd\u672a\u91c7\u96c6";
            GUI.Label(new Rect(rect.x + padding, rect.y + 5f * scale, rect.width - padding * 2f, 18f * scale), $"{status}  FPS {summary.AverageFps:0}", smallStyle);
            var feed = FindObjectOfType<WebcamFeedController>();
            var cameraLabel = feed != null ? feed.RequestedFormatLabel : "No camera";
            GUI.Label(new Rect(rect.x + padding, rect.y + 24f * scale, rect.width - padding * 2f, 18f * scale), $"Cam {summary.CameraFps:0}  MP {summary.NativeResultFps:0}  Hand {summary.AverageHandUpdateIntervalMs:0}ms", smallStyle);
            GUI.Label(new Rect(rect.x + padding, rect.y + 42f * scale, rect.width - padding * 2f, 18f * scale), cameraLabel, smallStyle);
            var probeLabel = webcamHealthProbe != null && webcamHealthProbe.IsRunning
                ? webcamHealthProbe.StatusText
                : webcamHealthProbe != null && webcamHealthProbe.BestResult.IsValid
                    ? $"Best {webcamHealthProbe.BestResult.FormatLabel} {webcamHealthProbe.BestResult.AverageFps:0}fps P95 {webcamHealthProbe.BestResult.P95IntervalMs:0}ms"
                    : "Probe idle";
            GUI.Label(new Rect(rect.x + padding, rect.y + 60f * scale, rect.width - padding * 2f, 18f * scale), probeLabel, smallStyle);

            var buttonY = rect.yMax - 28f * scale;
            var gap = 6f * scale;
            var buttonWidth = (rect.width - padding * 2f - gap * 4f) / 5f;
            if (GUI.Button(new Rect(rect.x + padding, buttonY, buttonWidth, 22f * scale), performanceMonitor.IsRecording ? "\u505c\u6b62\u91c7\u96c6" : "\u5f00\u59cb\u91c7\u96c6"))
            {
                if (performanceMonitor.IsRecording)
                {
                    performanceMonitor.StopRecording();
                }
                else
                {
                    performanceMonitor.StartRecording();
                }
            }

            if (GUI.Button(new Rect(rect.x + padding + buttonWidth + gap, buttonY, buttonWidth, 22f * scale), "\u5bfc\u51faCSV"))
            {
                performanceMonitor.ExportCsv();
            }

            if (GUI.Button(new Rect(rect.x + padding + (buttonWidth + gap) * 2f, buttonY, buttonWidth, 22f * scale), "\u91cd\u542f\u6444\u50cf"))
            {
                feed?.RestartCamera();
            }

            if (GUI.Button(new Rect(rect.x + padding + (buttonWidth + gap) * 3f, buttonY, buttonWidth, 22f * scale), "\u5207\u6863"))
            {
                feed?.CyclePerformanceFormat();
            }

            if (GUI.Button(new Rect(rect.x + padding + (buttonWidth + gap) * 4f, buttonY, buttonWidth, 22f * scale), webcamHealthProbe != null && webcamHealthProbe.IsRunning ? "\u4f53\u68c0\u4e2d" : "\u4f53\u68c0"))
            {
                EnsureWebcamHealthProbeBound();
                webcamHealthProbe?.StartProbe();
            }
        }

        private void EnsurePerformanceMonitorBound()
        {
            if (performanceMonitor != null)
            {
                EnsureWebcamHealthProbeBound();
                return;
            }

            performanceMonitor = FindObjectOfType<GesturePerformanceMonitor>();
            if (performanceMonitor != null)
            {
                TryConfigurePerformanceMonitor(performanceMonitor);
                return;
            }

            var owner = gameObject != null ? gameObject : FindObjectOfType<SpellGuardSceneContext>()?.gameObject;
            if (owner == null)
            {
                return;
            }

            performanceMonitor = owner.AddComponent<GesturePerformanceMonitor>();
            TryConfigurePerformanceMonitor(performanceMonitor);
            EnsureWebcamHealthProbeBound();
        }

        private void TryConfigurePerformanceMonitor(GesturePerformanceMonitor monitor)
        {
            if (monitor == null)
            {
                return;
            }

            var router = inputProvider as GestureInputRouter ?? FindObjectOfType<GestureInputRouter>();
            var bridge = FindObjectOfType<ExternalGestureBridgeProvider>();
            var feed = FindObjectOfType<WebcamFeedController>();
            var runner = FindObjectOfType<NativeMediapipeGestureRunner>();
            monitor.Configure(router, bridge, feed, runner);
        }

        private void EnsureWebcamHealthProbeBound()
        {
            if (webcamHealthProbe != null)
            {
                return;
            }

            webcamHealthProbe = FindObjectOfType<WebcamHealthProbe>();
            if (webcamHealthProbe == null)
            {
                var owner = gameObject != null ? gameObject : FindObjectOfType<SpellGuardSceneContext>()?.gameObject;
                if (owner == null)
                {
                    return;
                }

                webcamHealthProbe = owner.AddComponent<WebcamHealthProbe>();
            }

            webcamHealthProbe.Configure(FindObjectOfType<WebcamFeedController>(), FindObjectOfType<NativeMediapipeGestureRunner>());
        }

        private void DrawPulseOverlays(float scale)
        {
            var motionPulse = GetRecentMotionPulse();
            if (motionPulse > 0f)
            {
                var size = Mathf.Lerp(92f, 180f, 1f - motionPulse) * scale;
                var rect = new Rect((Screen.width - size) * 0.5f, Screen.height * 0.34f - size * 0.5f, size, size);
                GUI.color = new Color(GetMotionColor(lastMotion).r, GetMotionColor(lastMotion).g, GetMotionColor(lastMotion).b, 0.16f * motionPulse);
                GUI.DrawTexture(rect, SpellGuardRuntimeSkin.IconEnergy != null ? SpellGuardRuntimeSkin.IconEnergy : Texture2D.whiteTexture, ScaleMode.ScaleToFit, true);
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
            SpellGuardRuntimeSkin.DrawProgress(rect, confidence, SpellGuardRuntimeSkin.Mint);
        }

        private void DrawChip(Rect rect, string text, Color color)
        {
            DrawPanel(rect, new Color(color.r, color.g, color.b, 0.24f), new Color(color.r, color.g, color.b, 0.92f));
            GUI.Label(rect, text, chipStyle);
        }

        private void DrawPanel(Rect rect, Color fill, Color border)
        {
            SpellGuardRuntimeSkin.DrawImagePanel(rect, SpellGuardRuntimeSkin.HudFrame ?? SpellGuardRuntimeSkin.PanelSmall, fill, border, 1.5f);
        }

        private static void DrawStatusIcon(Rect rect, Texture2D texture, Color tint)
        {
            if (texture == null)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, 0.9f);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
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
                normal = { textColor = SpellGuardRuntimeSkin.Text },
                clipping = TextClipping.Clip
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                normal = { textColor = SpellGuardRuntimeSkin.Text },
                clipping = TextClipping.Clip
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                normal = { textColor = SpellGuardRuntimeSkin.MutedText },
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

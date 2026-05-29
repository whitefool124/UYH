using SpellGuard.InputSystem;
using SpellGuard.Audio;
using SpellGuard.Diagnostics;
using SpellGuard.UI;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardBootstrap : MonoBehaviour
    {
        [SerializeField] private SpellGuardSceneContext sceneContext;
        [SerializeField] private bool bootstrapOnAwake = true;

        private GestureInputRouter subscribedInputRouter;
        private GestureInputRouter.InputMode lastSyncedMode = GestureInputRouter.InputMode.Mock;

        public bool IsBootstrapped { get; private set; }

        private void Awake()
        {
            if (bootstrapOnAwake)
            {
                Bootstrap();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromInputRouter();
        }

        [ContextMenu("Bootstrap Now")]
        public void Bootstrap()
        {
            if (sceneContext == null)
            {
                Debug.LogError("SpellGuardBootstrap 缺少 SceneContext。", this);
                return;
            }

            EnsureAudioController();

            sceneContext.ValidateSerializedReferences();

            if (!sceneContext.IsValid(out var reason))
            {
                Debug.LogError($"SpellGuardBootstrap 装配失败：{reason}", this);
                return;
            }

            if (sceneContext.NativeMediapipeProvider != null)
            {
                sceneContext.NativeMediapipeProvider.Configure(sceneContext.WebcamFeed);
            }
            if (sceneContext.NativeMotionGestureRecognizer != null)
            {
                sceneContext.NativeMotionGestureRecognizer.Configure(sceneContext.NativeMediapipeProvider);
            }
            if (sceneContext.UdpGestureReceiver != null)
            {
                sceneContext.UdpGestureReceiver.Configure(sceneContext.ExternalBridge, sceneContext.WebcamFeed, sceneContext.ExternalBridgeProcessLauncher);
            }
            EnsureExternalBridgeProcessLauncher();
            if (sceneContext.ExternalMotionGestureRecognizer != null)
            {
                sceneContext.ExternalMotionGestureRecognizer.Configure(sceneContext.ExternalBridge);
            }
            if (sceneContext.MotionGestureFeedbackBoard != null)
            {
                sceneContext.MotionGestureFeedbackBoard.Configure(sceneContext.InputProvider, sceneContext.MainCamera);
            }
            if (sceneContext.PerformanceMonitor != null)
            {
                sceneContext.PerformanceMonitor.Configure(sceneContext.InputRouter, sceneContext.ExternalBridge, sceneContext.WebcamFeed, sceneContext.NativeMediapipeRunner);
            }
            EnsurePerformanceMonitor();
            if (sceneContext.WebcamHealthProbe != null)
            {
                sceneContext.WebcamHealthProbe.Configure(sceneContext.WebcamFeed, sceneContext.NativeMediapipeRunner);
            }
            EnsureWebcamHealthProbe();
            if (sceneContext.AudioController != null)
            {
                sceneContext.AudioController.ApplySettings(sceneContext.GameSettings);
                sceneContext.AudioController.PlayMenuMusic();
            }
            EnsureGestureFeedbackHud();

            if (sceneContext.InputRouter != null && sceneContext.GameSettings != null)
            {
                sceneContext.InputRouter.SetMode(sceneContext.GameSettings.InputMode);
            }

            SubscribeToInputRouter();
            SyncInputBackendLifecycle(lastSyncedMode, sceneContext.InputRouter != null ? sceneContext.InputRouter.Mode : GestureInputRouter.InputMode.Mock);

            IsBootstrapped = true;
        }

        private void EnsureAudioController()
        {
            if (sceneContext == null || sceneContext.AudioController != null)
            {
                return;
            }

            var existing = sceneContext.GetComponent<SpellGuardAudioController>();
            if (existing != null)
            {
                SetPrivateField(sceneContext, "audioController", existing);
                return;
            }

            var created = sceneContext.gameObject.AddComponent<SpellGuardAudioController>();
            SetPrivateField(sceneContext, "audioController", created);
        }

        private void EnsureGestureFeedbackHud()
        {
            if (sceneContext == null)
            {
                return;
            }

            var feedbackHud = sceneContext.GetComponent<GestureFeedbackHud>();
            if (feedbackHud == null)
            {
                feedbackHud = sceneContext.gameObject.AddComponent<GestureFeedbackHud>();
            }

            feedbackHud.Configure(
                sceneContext.InputProvider,
                sceneContext.SpellCaster,
                sceneContext.FpsMotor,
                sceneContext.PlayerHealth,
                sceneContext.EnemySpawner,
                sceneContext.FlowController,
                sceneContext.PerformanceMonitor,
                sceneContext.WebcamHealthProbe);
        }

        private void EnsurePerformanceMonitor()
        {
            if (sceneContext == null || sceneContext.PerformanceMonitor != null)
            {
                return;
            }

            var performanceMonitor = sceneContext.GetComponent<GesturePerformanceMonitor>();
            if (performanceMonitor == null)
            {
                performanceMonitor = sceneContext.gameObject.AddComponent<GesturePerformanceMonitor>();
            }

            performanceMonitor.Configure(sceneContext.InputRouter, sceneContext.ExternalBridge, sceneContext.WebcamFeed, sceneContext.NativeMediapipeRunner);
            SetPrivateField(sceneContext, "performanceMonitor", performanceMonitor);
        }

        private void EnsureWebcamHealthProbe()
        {
            if (sceneContext == null || sceneContext.WebcamHealthProbe != null)
            {
                return;
            }

            var probe = sceneContext.GetComponent<WebcamHealthProbe>();
            if (probe == null)
            {
                probe = sceneContext.gameObject.AddComponent<WebcamHealthProbe>();
            }

            probe.Configure(sceneContext.WebcamFeed, sceneContext.NativeMediapipeRunner);
            SetPrivateField(sceneContext, "webcamHealthProbe", probe);
        }

        private void EnsureExternalBridgeProcessLauncher()
        {
            if (sceneContext == null || sceneContext.ExternalBridgeProcessLauncher != null)
            {
                return;
            }

            var launcher = sceneContext.GetComponent<ExternalBridgeProcessLauncher>();
            if (launcher == null)
            {
                launcher = sceneContext.gameObject.AddComponent<ExternalBridgeProcessLauncher>();
            }

            SetPrivateField(sceneContext, "externalBridgeProcessLauncher", launcher);
            sceneContext.UdpGestureReceiver?.Configure(sceneContext.ExternalBridge, sceneContext.WebcamFeed, launcher);
        }

        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            if (target == null)
            {
                return;
            }

            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            field.SetValue(target, value);
        }

        private void SubscribeToInputRouter()
        {
            UnsubscribeFromInputRouter();
            if (sceneContext?.InputRouter == null)
            {
                return;
            }

            subscribedInputRouter = sceneContext.InputRouter;
            subscribedInputRouter.ModeChanged += HandleInputModeChanged;
        }

        private void UnsubscribeFromInputRouter()
        {
            if (subscribedInputRouter == null)
            {
                return;
            }

            subscribedInputRouter.ModeChanged -= HandleInputModeChanged;
            subscribedInputRouter = null;
        }

        private void HandleInputModeChanged(GestureInputRouter.InputMode _)
        {
            sceneContext?.GameSettings?.SetInputMode(_);
            var previous = lastSyncedMode;
            lastSyncedMode = _;
            SyncInputBackendLifecycle(previous, _);
        }

        private void SyncInputBackendLifecycle(GestureInputRouter.InputMode previousMode, GestureInputRouter.InputMode nextMode)
        {
            if (sceneContext == null)
            {
                return;
            }

            var useNativeMediapipe = nextMode == GestureInputRouter.InputMode.NativeMediapipe;
            var useExternalBridge = nextMode == GestureInputRouter.InputMode.ExternalBridge;

            if (previousMode == GestureInputRouter.InputMode.NativeMediapipe && !useNativeMediapipe)
            {
                sceneContext.NativeMediapipeRunner?.StopRunner();
                sceneContext.WebcamFeed?.ForceStopSharedCamera();
            }

            if (previousMode == GestureInputRouter.InputMode.ExternalBridge && !useExternalBridge)
            {
                sceneContext.UdpGestureReceiver?.StopReceiver();
                sceneContext.ExternalBridge?.ClearSnapshot();
            }

            if (sceneContext.NativeMediapipeRunner != null)
            {
                sceneContext.NativeMediapipeRunner.enabled = useNativeMediapipe;
            }

            if (sceneContext.WebcamFeed != null && useNativeMediapipe && !sceneContext.WebcamFeed.IsRunning)
            {
                sceneContext.WebcamFeed.StartCamera();
                if (sceneContext.WebcamFeed.Texture == null)
                {
                    sceneContext.InputRouter?.SetMode(GestureInputRouter.InputMode.Mock);
                    if (sceneContext.NativeMediapipeRunner != null)
                    {
                        sceneContext.NativeMediapipeRunner.enabled = false;
                    }
                    sceneContext.NativeMediapipeProvider?.SetStatusText("摄像头不可用，已回退到 Mock");
                    sceneContext.AudioController?.PlayMenuMusic();
                    lastSyncedMode = GestureInputRouter.InputMode.Mock;
                    return;
                }
            }

            if (sceneContext.NativeMediapipeRunner != null && useNativeMediapipe)
            {
                sceneContext.NativeMediapipeRunner.Configure(sceneContext.NativeMediapipeProvider, sceneContext.WebcamFeed);
                sceneContext.NativeMediapipeRunner.StartRunner();
            }

            if (sceneContext.UdpGestureReceiver != null)
            {
                if (useExternalBridge)
                {
                    sceneContext.WebcamFeed?.ForceStopSharedCamera();
                    sceneContext.UdpGestureReceiver.StartReceiver();
                }
                else
                {
                    sceneContext.UdpGestureReceiver.StopReceiver();
                }
            }

            lastSyncedMode = nextMode;
        }
    }
}

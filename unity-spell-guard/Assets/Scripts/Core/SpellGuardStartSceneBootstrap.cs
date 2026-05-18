using SpellGuard.Audio;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardStartSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private NativeMediapipeGestureRunner nativeMediapipeRunner;
        [SerializeField] private NativeMotionGestureRecognizer nativeMotionGestureRecognizer;
        [SerializeField] private ExternalGestureBridgeProvider externalBridge;
        [SerializeField] private ExternalMotionGestureRecognizer externalMotionGestureRecognizer;
        [SerializeField] private UdpGestureReceiver udpGestureReceiver;
        [SerializeField] private SpellGuardGameSettings settings;
        [SerializeField] private SpellGuardAudioController audioController;
        [SerializeField] private bool bootstrapOnAwake = true;

        private GestureInputRouter subscribedInputRouter;
        private GestureInputRouter.InputMode lastSyncedMode = GestureInputRouter.InputMode.Mock;

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

        [ContextMenu("Bootstrap Start Scene")]
        public void Bootstrap()
        {
            if (nativeMediapipeProvider != null)
            {
                nativeMediapipeProvider.Configure(webcamFeed);
            }

            if (nativeMediapipeRunner != null)
            {
                nativeMediapipeRunner.Configure(nativeMediapipeProvider, webcamFeed);
            }

            if (nativeMotionGestureRecognizer != null)
            {
                nativeMotionGestureRecognizer.Configure(nativeMediapipeProvider);
            }

            if (externalBridge != null && udpGestureReceiver != null)
            {
                udpGestureReceiver.Configure(externalBridge, webcamFeed);
            }

            if (externalMotionGestureRecognizer != null)
            {
                externalMotionGestureRecognizer.Configure(externalBridge);
            }

            if (audioController != null)
            {
                audioController.ApplySettings(settings);
                audioController.PlayMenuMusic();
            }

            if (inputRouter != null && settings != null)
            {
                inputRouter.SetMode(settings.InputMode);
            }

            SubscribeToInputRouter();
            SyncInputBackendLifecycle(lastSyncedMode, inputRouter != null ? inputRouter.Mode : GestureInputRouter.InputMode.Mock);
        }

        private void SubscribeToInputRouter()
        {
            UnsubscribeFromInputRouter();
            if (inputRouter == null)
            {
                return;
            }

            subscribedInputRouter = inputRouter;
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
            settings?.SetInputMode(_);
            var previous = lastSyncedMode;
            lastSyncedMode = _;
            SyncInputBackendLifecycle(previous, _);
        }

        private void SyncInputBackendLifecycle(GestureInputRouter.InputMode previousMode, GestureInputRouter.InputMode nextMode)
        {
            var useNativeMediapipe = nextMode == GestureInputRouter.InputMode.NativeMediapipe;
            var useExternalBridge = nextMode == GestureInputRouter.InputMode.ExternalBridge;

            if (previousMode == GestureInputRouter.InputMode.NativeMediapipe && !useNativeMediapipe)
            {
                nativeMediapipeRunner?.StopRunner();
                webcamFeed?.ForceStopSharedCamera();
            }

            if (previousMode == GestureInputRouter.InputMode.ExternalBridge && !useExternalBridge)
            {
                udpGestureReceiver?.StopReceiver();
                externalBridge?.ClearSnapshot();
            }

            if (nativeMediapipeRunner != null)
            {
                nativeMediapipeRunner.enabled = useNativeMediapipe;
            }

            if (webcamFeed != null && useNativeMediapipe && !webcamFeed.IsRunning)
            {
                webcamFeed.StartCamera();
                if (webcamFeed.Texture == null)
                {
                    inputRouter?.SetMode(GestureInputRouter.InputMode.Mock);
                    if (nativeMediapipeRunner != null)
                    {
                        nativeMediapipeRunner.enabled = false;
                    }

                    nativeMediapipeProvider?.SetStatusText("摄像头不可用，已回退到 Mock");
                    lastSyncedMode = GestureInputRouter.InputMode.Mock;
                    return;
                }
            }

            if (nativeMediapipeRunner != null && useNativeMediapipe)
            {
                nativeMediapipeRunner.Configure(nativeMediapipeProvider, webcamFeed);
                nativeMediapipeRunner.StartRunner();
            }

            if (udpGestureReceiver != null)
            {
                if (useExternalBridge)
                {
                    webcamFeed?.ForceStopSharedCamera();
                    udpGestureReceiver.StartReceiver();
                }
                else
                {
                    udpGestureReceiver.StopReceiver();
                }
            }

            lastSyncedMode = nextMode;
        }
    }
}

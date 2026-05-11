using SpellGuard.InputSystem;
using SpellGuard.Audio;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardBootstrap : MonoBehaviour
    {
        [SerializeField] private SpellGuardSceneContext sceneContext;
        [SerializeField] private bool bootstrapOnAwake = true;

        private GestureInputRouter subscribedInputRouter;

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
                sceneContext.UdpGestureReceiver.Configure(sceneContext.ExternalBridge, sceneContext.WebcamFeed);
            }
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
                sceneContext.PerformanceMonitor.Configure(sceneContext.InputRouter, sceneContext.ExternalBridge);
            }
            if (sceneContext.AudioController != null)
            {
                sceneContext.AudioController.ApplySettings(sceneContext.GameSettings);
                sceneContext.AudioController.PlayMenuMusic();
            }

            if (sceneContext.InputRouter != null && sceneContext.GameSettings != null)
            {
                sceneContext.InputRouter.SetMode(sceneContext.GameSettings.InputMode);
            }

            SubscribeToInputRouter();
            SyncInputBackendLifecycle();

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
            SyncInputBackendLifecycle();
        }

        private void SyncInputBackendLifecycle()
        {
            if (sceneContext == null)
            {
                return;
            }

            var mode = sceneContext.InputRouter != null ? sceneContext.InputRouter.Mode : GestureInputRouter.InputMode.Mock;
            var useNativeMediapipe = mode == GestureInputRouter.InputMode.NativeMediapipe;
            var useExternalBridge = mode == GestureInputRouter.InputMode.ExternalBridge;

            if (sceneContext.NativeMediapipeRunner != null)
            {
                sceneContext.NativeMediapipeRunner.enabled = useNativeMediapipe;
                if (useNativeMediapipe)
                {
                    sceneContext.NativeMediapipeRunner.Configure(sceneContext.NativeMediapipeProvider, sceneContext.WebcamFeed);
                    sceneContext.NativeMediapipeRunner.StartRunner();
                }
            }

            if (sceneContext.UdpGestureReceiver != null)
            {
                if (useExternalBridge)
                {
                    sceneContext.UdpGestureReceiver.StartReceiver();
                }
                else
                {
                    sceneContext.UdpGestureReceiver.StopReceiver();
                }
            }

            if (sceneContext.WebcamFeed != null)
            {
                if (useNativeMediapipe && !sceneContext.WebcamFeed.IsRunning)
                {
                    sceneContext.WebcamFeed.StartCamera();
                    if (!sceneContext.WebcamFeed.IsRunning)
                    {
                        sceneContext.InputRouter?.SetMode(GestureInputRouter.InputMode.Mock);
                        if (sceneContext.NativeMediapipeRunner != null)
                        {
                            sceneContext.NativeMediapipeRunner.enabled = false;
                        }
                        sceneContext.NativeMediapipeProvider?.SetStatusText("摄像头不可用，已回退到 Mock");
                        sceneContext.AudioController?.PlayMenuMusic();
                    }
                }
                else if (!useNativeMediapipe && sceneContext.WebcamFeed.IsRunning)
                {
                    sceneContext.WebcamFeed.StopCamera();
                }
            }
        }
    }
}

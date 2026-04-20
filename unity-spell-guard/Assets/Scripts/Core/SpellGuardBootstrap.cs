using SpellGuard.InputSystem;
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

            sceneContext.AutoBindMissingReferences();

            if (!sceneContext.IsValid(out var reason))
            {
                Debug.LogError($"SpellGuardBootstrap 装配失败：{reason}", this);
                return;
            }

            sceneContext.FpsMotor.Configure(sceneContext.InputProvider, sceneContext.CameraPivot);
            sceneContext.SpellCaster.Configure(sceneContext.InputProvider, sceneContext.MainCamera, sceneContext.PlayerHealth);
            sceneContext.EnemySpawner.Configure(sceneContext.PlayerRoot, sceneContext.PlayerHealth);
            sceneContext.GameFlowManager.Configure(sceneContext.PlayerHealth, sceneContext.EnemySpawner);
            sceneContext.FlowController.Configure(
                sceneContext.InputProvider,
                sceneContext.ExternalBridge,
                sceneContext.GameSettings,
                sceneContext.FpsMotor,
                sceneContext.SpellCaster,
                sceneContext.PlayerHealth,
                sceneContext.PlayerRoot,
                sceneContext.EnemySpawner,
                sceneContext.GameFlowManager,
                sceneContext.MainCamera
            );

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
            sceneContext.DebugHud.Configure(
                sceneContext.InputProvider,
                sceneContext.InputRouter,
                sceneContext.WebcamFeed,
                sceneContext.NativeMediapipeProvider,
                sceneContext.ExternalBridge,
                sceneContext.UdpGestureReceiver,
                sceneContext.FpsMotor,
                sceneContext.SpellCaster,
                sceneContext.PlayerHealth,
                sceneContext.EnemySpawner,
                sceneContext.GameFlowManager,
                sceneContext.FlowController
            );

            if (sceneContext.MotionGestureFeedbackBoard != null)
            {
                sceneContext.MotionGestureFeedbackBoard.Configure(sceneContext.InputProvider, sceneContext.MainCamera);
            }

            SubscribeToInputRouter();
            SyncInputBackendLifecycle();

            IsBootstrapped = true;
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
            SyncInputBackendLifecycle();
        }

        private void SyncInputBackendLifecycle()
        {
            if (sceneContext == null)
            {
                return;
            }

            var useExternalBridge = sceneContext.InputProvider == sceneContext.ExternalBridge ||
                                    (sceneContext.InputRouter != null && sceneContext.InputRouter.Mode == GestureInputRouter.InputMode.ExternalBridge);

            if (sceneContext.NativeMediapipeRunner != null && !useExternalBridge)
            {
                sceneContext.NativeMediapipeRunner.Configure(sceneContext.NativeMediapipeProvider, sceneContext.WebcamFeed);
                sceneContext.NativeMediapipeRunner.StartRunner();
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

            if (!useExternalBridge && sceneContext.WebcamFeed != null && !sceneContext.WebcamFeed.IsRunning)
            {
                sceneContext.WebcamFeed.StartCamera();
            }
        }
    }
}

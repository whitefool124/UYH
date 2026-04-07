using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardBootstrap : MonoBehaviour
    {
        [SerializeField] private SpellGuardSceneContext sceneContext;
        [SerializeField] private bool bootstrapOnAwake = true;

        public bool IsBootstrapped { get; private set; }

        private void Awake()
        {
            if (bootstrapOnAwake)
            {
                Bootstrap();
            }
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
            if (sceneContext.NativeMediapipeRunner != null)
            {
                sceneContext.NativeMediapipeRunner.Configure(sceneContext.NativeMediapipeProvider, sceneContext.WebcamFeed);
                sceneContext.NativeMediapipeRunner.StartRunner();
            }
            if (sceneContext.UdpGestureReceiver != null)
            {
                sceneContext.UdpGestureReceiver.Configure(sceneContext.ExternalBridge, sceneContext.WebcamFeed);
                sceneContext.UdpGestureReceiver.StopReceiver();
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

            IsBootstrapped = true;
        }
    }
}

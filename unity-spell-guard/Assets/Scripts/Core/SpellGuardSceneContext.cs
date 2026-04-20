using SpellGuard.Combat;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using SpellGuard.UI;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardSceneContext : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private MockGestureInputProvider mockProvider;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private NativeMediapipeGestureRunner nativeMediapipeRunner;
        [SerializeField] private NativeMotionGestureRecognizer nativeMotionGestureRecognizer;
        [SerializeField] private ExternalGestureBridgeProvider externalBridge;
        [SerializeField] private ExternalMotionGestureRecognizer externalMotionGestureRecognizer;
        [SerializeField] private UdpGestureReceiver udpGestureReceiver;
        [SerializeField] private WebcamFeedController webcamFeed;

        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private FpsGestureMotor fpsMotor;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Combat")]
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private GameFlowManager gameFlowManager;
        [SerializeField] private SpellGuardGameSettings gameSettings;
        [SerializeField] private SpellGuardFlowController flowController;

        [Header("UI")]
        [SerializeField] private DebugHud debugHud;

        [Header("Feedback")]
        [SerializeField] private MotionGestureFeedbackBoard motionGestureFeedbackBoard;

        public GestureInputProviderBase InputProvider => inputProvider;
        public GestureInputRouter InputRouter => inputRouter;
        public MockGestureInputProvider MockProvider => mockProvider;
        public NativeMediapipeGestureProvider NativeMediapipeProvider => nativeMediapipeProvider;
        public NativeMediapipeGestureRunner NativeMediapipeRunner => nativeMediapipeRunner;
        public NativeMotionGestureRecognizer NativeMotionGestureRecognizer => nativeMotionGestureRecognizer;
        public ExternalGestureBridgeProvider ExternalBridge => externalBridge;
        public ExternalMotionGestureRecognizer ExternalMotionGestureRecognizer => externalMotionGestureRecognizer;
        public UdpGestureReceiver UdpGestureReceiver => udpGestureReceiver;
        public WebcamFeedController WebcamFeed => webcamFeed;
        public Transform PlayerRoot => playerRoot;
        public Transform CameraPivot => cameraPivot;
        public Camera MainCamera => mainCamera;
        public FpsGestureMotor FpsMotor => fpsMotor;
        public GestureSpellCaster SpellCaster => spellCaster;
        public PlayerHealth PlayerHealth => playerHealth;
        public EnemySpawner EnemySpawner => enemySpawner;
        public GameFlowManager GameFlowManager => gameFlowManager;
        public SpellGuardGameSettings GameSettings => gameSettings;
        public SpellGuardFlowController FlowController => flowController;
        public DebugHud DebugHud => debugHud;
        public MotionGestureFeedbackBoard MotionGestureFeedbackBoard => motionGestureFeedbackBoard;

        public void AutoBindMissingReferences()
        {
            gameSettings ??= GetOrAddComponent<SpellGuardGameSettings>(gameObject);
            flowController ??= GetOrAddComponent<SpellGuardFlowController>(gameObject);
            enemySpawner ??= GetOrAddComponent<EnemySpawner>(gameObject);
            gameFlowManager ??= GetOrAddComponent<GameFlowManager>(gameObject);
            debugHud ??= GetOrAddComponent<DebugHud>(gameObject);

            inputRouter ??= FindObjectOfType<GestureInputRouter>(true);
            mockProvider ??= FindObjectOfType<MockGestureInputProvider>(true);
            nativeMediapipeProvider ??= FindObjectOfType<NativeMediapipeGestureProvider>(true);
            nativeMediapipeRunner ??= FindObjectOfType<NativeMediapipeGestureRunner>(true);
            nativeMotionGestureRecognizer ??= FindObjectOfType<NativeMotionGestureRecognizer>(true);
            externalBridge ??= FindObjectOfType<ExternalGestureBridgeProvider>(true);
            externalMotionGestureRecognizer ??= FindObjectOfType<ExternalMotionGestureRecognizer>(true);
            udpGestureReceiver ??= FindObjectOfType<UdpGestureReceiver>(true);
            webcamFeed ??= FindObjectOfType<WebcamFeedController>(true);
            fpsMotor ??= FindObjectOfType<FpsGestureMotor>(true);
            spellCaster ??= FindObjectOfType<GestureSpellCaster>(true);
            playerHealth ??= FindObjectOfType<PlayerHealth>(true);
            enemySpawner ??= FindObjectOfType<EnemySpawner>(true);
            gameFlowManager ??= FindObjectOfType<GameFlowManager>(true);
            gameSettings ??= FindObjectOfType<SpellGuardGameSettings>(true);
            flowController ??= FindObjectOfType<SpellGuardFlowController>(true);
            debugHud ??= FindObjectOfType<DebugHud>(true);
            motionGestureFeedbackBoard ??= FindObjectOfType<MotionGestureFeedbackBoard>(true);

            if (inputProvider == null)
            {
                inputProvider = inputRouter != null ? inputRouter : FindObjectOfType<GestureInputProviderBase>(true);
            }

            if (externalMotionGestureRecognizer == null && externalBridge != null)
            {
                externalMotionGestureRecognizer = GetOrAddComponent<ExternalMotionGestureRecognizer>(externalBridge.gameObject);
            }

            if (nativeMotionGestureRecognizer == null && nativeMediapipeProvider != null)
            {
                nativeMotionGestureRecognizer = GetOrAddComponent<NativeMotionGestureRecognizer>(nativeMediapipeProvider.gameObject);
            }

            if (playerRoot == null && fpsMotor != null)
            {
                playerRoot = fpsMotor.transform;
            }

            if (cameraPivot == null && mainCamera != null && mainCamera.transform.parent != null)
            {
                cameraPivot = mainCamera.transform.parent;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    mainCamera = FindObjectOfType<Camera>(true);
                }
            }
        }

        public bool IsValid(out string reason)
        {
            if (inputProvider == null)
            {
                reason = "InputProvider 未绑定";
                return false;
            }

            if (playerRoot == null || cameraPivot == null || mainCamera == null)
            {
                reason = "玩家或相机引用不完整";
                return false;
            }

            if (fpsMotor == null || spellCaster == null || playerHealth == null)
            {
                reason = "玩家组件引用不完整";
                return false;
            }

            if (enemySpawner == null || gameFlowManager == null || debugHud == null || gameSettings == null || flowController == null)
            {
                reason = "流程、战斗或 HUD 组件引用不完整";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }
    }
}

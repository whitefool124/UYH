using SpellGuard.Combat;
using SpellGuard.Audio;
using SpellGuard.Diagnostics;
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
        [SerializeField] private SpellGuardMenuOverlay menuOverlay;

        [Header("Audio")]
        [SerializeField] private SpellGuardAudioController audioController;

        [Header("Feedback")]
        [SerializeField] private MotionGestureFeedbackBoard motionGestureFeedbackBoard;

        [Header("Diagnostics")]
        [SerializeField] private GesturePerformanceMonitor performanceMonitor;

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
        public SpellGuardMenuOverlay MenuOverlay => menuOverlay;
        public SpellGuardAudioController AudioController => audioController;
        public MotionGestureFeedbackBoard MotionGestureFeedbackBoard => motionGestureFeedbackBoard;
        public GesturePerformanceMonitor PerformanceMonitor => performanceMonitor;

        public void ValidateSerializedReferences()
        {
            if (inputProvider == null && inputRouter != null)
            {
                inputProvider = inputRouter;
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

            if (enemySpawner == null || gameFlowManager == null || menuOverlay == null || gameSettings == null || flowController == null || audioController == null)
            {
                reason = "流程、战斗、玩家 UI 或音频组件引用不完整";
                return false;
            }

            reason = string.Empty;
            return true;
        }

    }
}

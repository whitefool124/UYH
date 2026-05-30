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
        [SerializeField] private ExternalBridgeProcessLauncher externalBridgeProcessLauncher;
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
        [SerializeField] private WebcamHealthProbe webcamHealthProbe;

        public GestureInputProviderBase InputProvider => inputProvider;
        public GestureInputRouter InputRouter => inputRouter;
        public MockGestureInputProvider MockProvider => mockProvider;
        public NativeMediapipeGestureProvider NativeMediapipeProvider => nativeMediapipeProvider;
        public NativeMediapipeGestureRunner NativeMediapipeRunner => nativeMediapipeRunner;
        public NativeMotionGestureRecognizer NativeMotionGestureRecognizer => nativeMotionGestureRecognizer;
        public ExternalGestureBridgeProvider ExternalBridge => externalBridge;
        public ExternalMotionGestureRecognizer ExternalMotionGestureRecognizer => externalMotionGestureRecognizer;
        public UdpGestureReceiver UdpGestureReceiver => udpGestureReceiver;
        public ExternalBridgeProcessLauncher ExternalBridgeProcessLauncher => externalBridgeProcessLauncher;
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
        public WebcamHealthProbe WebcamHealthProbe => webcamHealthProbe;

        public void ValidateSerializedReferences()
        {
            if (inputProvider == null && inputRouter != null)
            {
                inputProvider = inputRouter;
            }

            // Auto-detect components on same GameObject if references are missing
            if (audioController == null) audioController = GetComponent<SpellGuardAudioController>();
            if (gameSettings == null) gameSettings = GetComponent<SpellGuardGameSettings>();
            if (flowController == null) flowController = GetComponent<SpellGuardFlowController>();
            if (enemySpawner == null) enemySpawner = GetComponent<EnemySpawner>();
            if (gameFlowManager == null) gameFlowManager = GetComponent<GameFlowManager>();
        }

        public bool IsValid(out string reason)
        {
            if (inputProvider == null) { reason = "InputProvider 未绑定"; return false; }
            if (playerRoot == null) { reason = "PlayerRoot 未绑定"; return false; }
            if (cameraPivot == null) { reason = "CameraPivot 未绑定"; return false; }
            if (mainCamera == null) { reason = "MainCamera 未绑定"; return false; }
            if (fpsMotor == null) { reason = "FpsMotor 未绑定"; return false; }
            if (spellCaster == null) { reason = "SpellCaster 未绑定"; return false; }
            if (playerHealth == null) { reason = "PlayerHealth 未绑定"; return false; }
            if (enemySpawner == null) { reason = "EnemySpawner 未绑定"; return false; }
            if (gameFlowManager == null) { reason = "GameFlowManager 未绑定"; return false; }
            if (gameSettings == null) { reason = "GameSettings 未绑定"; return false; }
            if (flowController == null) { reason = "FlowController 未绑定"; return false; }
            if (audioController == null) { reason = "AudioController 未绑定"; return false; }

            reason = string.Empty;
            return true;
        }

    }
}

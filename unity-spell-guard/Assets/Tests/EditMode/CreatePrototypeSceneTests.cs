using SpellGuard.Core;
using SpellGuard.EditorTools;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using SpellGuard.Combat;
using SpellGuard.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpellGuard.Tests.EditMode
{
    public class CreatePrototypeSceneTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        [Test]
        public void CreateScene_WiresMotionRecognizersIntoSceneContext()
        {
            CreatePrototypeScene.CreateScene();

            var playerRoot = GameObject.Find("PlayerRoot");
            var ritualLane = GameObject.Find("RitualLane");
            var spellDais = GameObject.Find("SpellDais");
            var arenaSign = GameObject.Find("ArenaSign");
            var ritualGate = GameObject.Find("RitualGate");
            var nativeRecognizer = playerRoot != null ? playerRoot.GetComponent<NativeMotionGestureRecognizer>() : null;
            var recognizer = playerRoot != null ? playerRoot.GetComponent<ExternalMotionGestureRecognizer>() : null;
            var nativeProvider = playerRoot != null ? playerRoot.GetComponent<NativeMediapipeGestureProvider>() : null;
            var bridgeProvider = playerRoot != null ? playerRoot.GetComponent<ExternalGestureBridgeProvider>() : null;
            var inputRouter = playerRoot != null ? playerRoot.GetComponent<GestureInputRouter>() : null;
            var motor = playerRoot != null ? playerRoot.GetComponent<FpsGestureMotor>() : null;
            var spellCaster = playerRoot != null ? playerRoot.GetComponent<GestureSpellCaster>() : null;
            var playerHealth = playerRoot != null ? playerRoot.GetComponent<PlayerHealth>() : null;
            var webcamFeed = playerRoot != null ? playerRoot.GetComponent<WebcamFeedController>() : null;
            var udpReceiver = playerRoot != null ? playerRoot.GetComponent<UdpGestureReceiver>() : null;
            var sceneContext = Object.FindObjectOfType<SpellGuardSceneContext>();
            var feedbackBoard = Object.FindObjectOfType<MotionGestureFeedbackBoard>(true);
            var menuOverlay = Object.FindObjectOfType<SpellGuardMenuOverlay>(true);
            var flowController = Object.FindObjectOfType<SpellGuardFlowController>(true);

            Assert.That(playerRoot, Is.Not.Null, "PlayerRoot should exist in the generated prototype scene.");
            Assert.That(ritualLane, Is.Not.Null, "Generated scene should include a ritual lane for arena composition.");
            Assert.That(spellDais, Is.Not.Null, "Generated scene should include a central spell dais.");
            Assert.That(arenaSign, Is.Not.Null, "Generated scene should include an arena sign.");
            Assert.That(ritualGate, Is.Not.Null, "Generated scene should include a ritual gate focal structure.");
            Assert.That(nativeRecognizer, Is.Not.Null, "Generated scene should explicitly include NativeMotionGestureRecognizer.");
            Assert.That(recognizer, Is.Not.Null, "Generated scene should explicitly include ExternalMotionGestureRecognizer.");
            Assert.That(sceneContext, Is.Not.Null, "SceneContext should exist in the generated prototype scene.");
            Assert.That(sceneContext.NativeMotionGestureRecognizer, Is.SameAs(nativeRecognizer));
            Assert.That(sceneContext.ExternalMotionGestureRecognizer, Is.SameAs(recognizer));
            Assert.That(feedbackBoard, Is.Not.Null, "Generated scene should include a world-space motion feedback board.");
            Assert.That(menuOverlay, Is.Not.Null, "Generated scene should include a menu overlay for non-playing screens.");
            Assert.That(sceneContext.MenuOverlay, Is.SameAs(menuOverlay));
            Assert.That(sceneContext.MotionGestureFeedbackBoard, Is.SameAs(feedbackBoard));
            Assert.That(flowController, Is.Not.Null, "Generated scene should include the flow controller.");

            var nativeRecognizerObject = new SerializedObject(nativeRecognizer);
            var nativeProviderProperty = nativeRecognizerObject.FindProperty("nativeProvider");
            Assert.That(nativeProviderProperty, Is.Not.Null);
            Assert.That(nativeProviderProperty.objectReferenceValue, Is.SameAs(nativeProvider));

            var recognizerObject = new SerializedObject(recognizer);
            var bridgeProperty = recognizerObject.FindProperty("bridgeProvider");
            Assert.That(bridgeProperty, Is.Not.Null);
            Assert.That(bridgeProperty.objectReferenceValue, Is.SameAs(bridgeProvider));

            var feedbackBoardObject = new SerializedObject(feedbackBoard);
            var feedbackInputProperty = feedbackBoardObject.FindProperty("inputProvider");
            var feedbackCameraProperty = feedbackBoardObject.FindProperty("faceCamera");
            Assert.That(feedbackInputProperty, Is.Not.Null);
            Assert.That(feedbackInputProperty.objectReferenceValue, Is.SameAs(inputRouter));
            Assert.That(feedbackCameraProperty, Is.Not.Null);
            Assert.That(feedbackCameraProperty.objectReferenceValue, Is.SameAs(Camera.main));

            var menuOverlayObject = new SerializedObject(menuOverlay);
            var menuInputProperty = menuOverlayObject.FindProperty("inputProvider");
            var menuSettingsProperty = menuOverlayObject.FindProperty("settings");
            var menuFlowProperty = menuOverlayObject.FindProperty("flowController");
            Assert.That(menuInputProperty, Is.Not.Null);
            Assert.That(menuInputProperty.objectReferenceValue, Is.SameAs(inputRouter));
            Assert.That(menuSettingsProperty, Is.Not.Null);
            Assert.That(menuSettingsProperty.objectReferenceValue, Is.SameAs(sceneContext.GameSettings));
            Assert.That(menuFlowProperty, Is.Not.Null);
            Assert.That(menuFlowProperty.objectReferenceValue, Is.SameAs(flowController));

            var flowControllerObject = new SerializedObject(flowController);
            Assert.That(flowControllerObject.FindProperty("settings")?.objectReferenceValue, Is.SameAs(sceneContext.GameSettings));
            Assert.That(flowControllerObject.FindProperty("motor")?.objectReferenceValue, Is.SameAs(motor));
            Assert.That(flowControllerObject.FindProperty("spellCaster")?.objectReferenceValue, Is.SameAs(spellCaster));
            Assert.That(flowControllerObject.FindProperty("playerHealth")?.objectReferenceValue, Is.SameAs(playerHealth));
            Assert.That(flowControllerObject.FindProperty("enemySpawner")?.objectReferenceValue, Is.SameAs(sceneContext.EnemySpawner));
            Assert.That(flowControllerObject.FindProperty("gameFlow")?.objectReferenceValue, Is.SameAs(sceneContext.GameFlowManager));

            var motorObject = new SerializedObject(motor);
            Assert.That(motorObject.FindProperty("inputProvider")?.objectReferenceValue, Is.SameAs(inputRouter));
            Assert.That(motorObject.FindProperty("cameraPivot")?.objectReferenceValue, Is.SameAs(sceneContext.CameraPivot));

            var spellCasterObject = new SerializedObject(spellCaster);
            Assert.That(spellCasterObject.FindProperty("inputProvider")?.objectReferenceValue, Is.SameAs(inputRouter));
            Assert.That(spellCasterObject.FindProperty("castCamera")?.objectReferenceValue, Is.SameAs(Camera.main));
            Assert.That(spellCasterObject.FindProperty("playerHealth")?.objectReferenceValue, Is.SameAs(playerHealth));

            var enemySpawnerObject = new SerializedObject(sceneContext.EnemySpawner);
            Assert.That(enemySpawnerObject.FindProperty("playerRoot")?.objectReferenceValue, Is.SameAs(playerRoot.transform));
            Assert.That(enemySpawnerObject.FindProperty("playerHealth")?.objectReferenceValue, Is.SameAs(playerHealth));

            var gameFlowObject = new SerializedObject(sceneContext.GameFlowManager);
            Assert.That(gameFlowObject.FindProperty("playerHealth")?.objectReferenceValue, Is.SameAs(playerHealth));
            Assert.That(gameFlowObject.FindProperty("enemySpawner")?.objectReferenceValue, Is.SameAs(sceneContext.EnemySpawner));

            var hudObject = new SerializedObject(sceneContext.DebugHud);
            Assert.That(hudObject.FindProperty("inputProvider")?.objectReferenceValue, Is.SameAs(inputRouter));
            Assert.That(hudObject.FindProperty("inputRouter")?.objectReferenceValue, Is.SameAs(inputRouter));
            Assert.That(hudObject.FindProperty("webcamFeed")?.objectReferenceValue, Is.SameAs(webcamFeed));
            Assert.That(hudObject.FindProperty("nativeMediapipeProvider")?.objectReferenceValue, Is.SameAs(nativeProvider));
            Assert.That(hudObject.FindProperty("externalBridge")?.objectReferenceValue, Is.SameAs(bridgeProvider));
            Assert.That(hudObject.FindProperty("udpGestureReceiver")?.objectReferenceValue, Is.SameAs(udpReceiver));
            Assert.That(hudObject.FindProperty("motor")?.objectReferenceValue, Is.SameAs(motor));
            Assert.That(hudObject.FindProperty("spellCaster")?.objectReferenceValue, Is.SameAs(spellCaster));
            Assert.That(hudObject.FindProperty("playerHealth")?.objectReferenceValue, Is.SameAs(playerHealth));
            Assert.That(hudObject.FindProperty("enemySpawner")?.objectReferenceValue, Is.SameAs(sceneContext.EnemySpawner));
            Assert.That(hudObject.FindProperty("gameFlow")?.objectReferenceValue, Is.SameAs(sceneContext.GameFlowManager));
            Assert.That(hudObject.FindProperty("flowController")?.objectReferenceValue, Is.SameAs(flowController));
        }
    }
}

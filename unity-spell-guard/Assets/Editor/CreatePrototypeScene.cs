#if UNITY_EDITOR
using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using SpellGuard.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellGuard.EditorTools
{
    public static class CreatePrototypeScene
    {
        [MenuItem("Spell Guard/Create Prototype Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "SpellGuardPrototype";

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(0.16f, 0.18f, 0.22f);

            var corridorLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            corridorLeft.name = "Wall_Left";
            corridorLeft.transform.position = new Vector3(-8f, 2f, 18f);
            corridorLeft.transform.localScale = new Vector3(1f, 4f, 40f);

            var corridorRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            corridorRight.name = "Wall_Right";
            corridorRight.transform.position = new Vector3(8f, 2f, 18f);
            corridorRight.transform.localScale = new Vector3(1f, 4f, 40f);

            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "Wall_Back";
            backWall.transform.position = new Vector3(0f, 2f, -8f);
            backWall.transform.localScale = new Vector3(16f, 4f, 1f);

            var player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(0f, 1.1f, 0f);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var webcamFeed = player.AddComponent<WebcamFeedController>();
            var mockProvider = player.AddComponent<MockGestureInputProvider>();
            var nativeMediapipeProvider = player.AddComponent<NativeMediapipeGestureProvider>();
            var nativeMediapipeRunner = player.AddComponent<NativeMediapipeGestureRunner>();
            var externalBridge = player.AddComponent<ExternalGestureBridgeProvider>();
            var udpReceiver = player.AddComponent<UdpGestureReceiver>();
            var inputRouter = player.AddComponent<GestureInputRouter>();
            var health = player.AddComponent<PlayerHealth>();
            var motor = player.AddComponent<FpsGestureMotor>();
            var spellCaster = player.AddComponent<GestureSpellCaster>();

            var cameraPivot = new GameObject("CameraPivot").transform;
            cameraPivot.SetParent(player.transform, false);
            cameraPivot.localPosition = new Vector3(0f, 1.45f, 0f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraPivot, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;

            var flow = new GameObject("GameFlow");
            var sceneContext = flow.AddComponent<SpellGuardSceneContext>();
            var bootstrap = flow.AddComponent<SpellGuardBootstrap>();
            var settings = flow.AddComponent<SpellGuardGameSettings>();
            var flowController = flow.AddComponent<SpellGuardFlowController>();
            var spawner = flow.AddComponent<EnemySpawner>();
            var gameFlow = flow.AddComponent<GameFlowManager>();
            var hud = flow.AddComponent<DebugHud>();

            SetField(inputRouter, "mockProvider", mockProvider);
            SetField(inputRouter, "nativeMediapipeProvider", nativeMediapipeProvider);
            SetField(inputRouter, "externalBridgeProvider", externalBridge);

            SetField(sceneContext, "inputProvider", inputRouter);
            SetField(sceneContext, "inputRouter", inputRouter);
            SetField(sceneContext, "mockProvider", mockProvider);
            SetField(sceneContext, "nativeMediapipeProvider", nativeMediapipeProvider);
            SetField(sceneContext, "nativeMediapipeRunner", nativeMediapipeRunner);
            SetField(sceneContext, "externalBridge", externalBridge);
            SetField(sceneContext, "udpGestureReceiver", udpReceiver);
            SetField(sceneContext, "webcamFeed", webcamFeed);
            SetField(sceneContext, "playerRoot", player.transform);
            SetField(sceneContext, "cameraPivot", cameraPivot);
            SetField(sceneContext, "mainCamera", camera);
            SetField(sceneContext, "fpsMotor", motor);
            SetField(sceneContext, "spellCaster", spellCaster);
            SetField(sceneContext, "playerHealth", health);
            SetField(sceneContext, "enemySpawner", spawner);
            SetField(sceneContext, "gameFlowManager", gameFlow);
            SetField(sceneContext, "gameSettings", settings);
            SetField(sceneContext, "flowController", flowController);
            SetField(sceneContext, "debugHud", hud);

            SetField(udpReceiver, "bridgeProvider", externalBridge);
            SetField(udpReceiver, "webcamFeed", webcamFeed);
            SetField(nativeMediapipeProvider, "webcamFeed", webcamFeed);
            SetField(nativeMediapipeRunner, "targetProvider", nativeMediapipeProvider);
            SetField(nativeMediapipeRunner, "webcamFeed", webcamFeed);

            SetField(bootstrap, "sceneContext", sceneContext);

            var scenesFolder = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(scenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Scenes/SpellGuardPrototype.unity");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetField(Object target, string fieldName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif

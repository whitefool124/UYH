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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "SpellGuardPrototype";

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.11f, 0.16f);

            var directionalLightObject = new GameObject("Directional Light");
            directionalLightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var directionalLight = directionalLightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 0.85f;
            directionalLight.color = new Color(0.82f, 0.87f, 1f);

            var fillLightObject = new GameObject("Fill Light");
            fillLightObject.transform.position = new Vector3(0f, 4.8f, 5f);
            fillLightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 22f;
            fillLight.intensity = 2.1f;
            fillLight.color = new Color(0.32f, 0.54f, 0.95f);

            var ritualLightObject = new GameObject("Ritual Light");
            ritualLightObject.transform.position = new Vector3(0f, 1.5f, 12f);
            var ritualLight = ritualLightObject.AddComponent<Light>();
            ritualLight.type = LightType.Point;
            ritualLight.range = 18f;
            ritualLight.intensity = 3f;
            ritualLight.color = new Color(1f, 0.62f, 0.2f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(0.08f, 0.1f, 0.14f);

            var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lane.name = "RitualLane";
            lane.transform.position = new Vector3(0f, 0.05f, 11f);
            lane.transform.localScale = new Vector3(4.2f, 0.08f, 24f);
            lane.GetComponent<Renderer>().sharedMaterial.color = new Color(0.12f, 0.16f, 0.24f);

            var centerPlatform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            centerPlatform.name = "SpellDais";
            centerPlatform.transform.position = new Vector3(0f, 0.12f, 10.5f);
            centerPlatform.transform.localScale = new Vector3(2.8f, 0.12f, 2.8f);
            centerPlatform.GetComponent<Renderer>().sharedMaterial.color = new Color(0.18f, 0.2f, 0.28f);

            var daisRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            daisRing.name = "SpellDaisRing";
            daisRing.transform.position = new Vector3(0f, 0.15f, 10.5f);
            daisRing.transform.localScale = new Vector3(3.4f, 0.04f, 3.4f);
            daisRing.GetComponent<Renderer>().sharedMaterial.color = new Color(1f, 0.58f, 0.2f);

            var corridorLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            corridorLeft.name = "Wall_Left";
            corridorLeft.transform.position = new Vector3(-8f, 2f, 18f);
            corridorLeft.transform.localScale = new Vector3(1f, 4f, 40f);
            corridorLeft.GetComponent<Renderer>().sharedMaterial.color = new Color(0.24f, 0.26f, 0.31f);

            var corridorRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            corridorRight.name = "Wall_Right";
            corridorRight.transform.position = new Vector3(8f, 2f, 18f);
            corridorRight.transform.localScale = new Vector3(1f, 4f, 40f);
            corridorRight.GetComponent<Renderer>().sharedMaterial.color = new Color(0.24f, 0.26f, 0.31f);

            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "Wall_Back";
            backWall.transform.position = new Vector3(0f, 2f, -8f);
            backWall.transform.localScale = new Vector3(16f, 4f, 1f);
            backWall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.2f, 0.22f, 0.28f);

            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < 3; i++)
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = $"Pillar_{(side < 0 ? "L" : "R")}_{i + 1}";
                    pillar.transform.position = new Vector3(side * 6.4f, 2.3f, 3f + i * 8f);
                    pillar.transform.localScale = new Vector3(0.9f, 4.6f, 0.9f);
                    pillar.GetComponent<Renderer>().sharedMaterial.color = new Color(0.3f, 0.3f, 0.34f);

                    var brazier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    brazier.name = $"Brazier_{(side < 0 ? "L" : "R")}_{i + 1}";
                    brazier.transform.position = new Vector3(side * 5.6f, 0.55f, 3f + i * 8f);
                    brazier.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
                    brazier.GetComponent<Renderer>().sharedMaterial.color = new Color(0.68f, 0.44f, 0.2f);

                    var brazierLightObject = new GameObject($"BrazierLight_{(side < 0 ? "L" : "R")}_{i + 1}");
                    brazierLightObject.transform.position = brazier.transform.position + new Vector3(0f, 1.25f, 0f);
                    var brazierLight = brazierLightObject.AddComponent<Light>();
                    brazierLight.type = LightType.Point;
                    brazierLight.range = 6f;
                    brazierLight.intensity = 1.7f;
                    brazierLight.color = new Color(1f, 0.52f, 0.18f);
                }
            }

            var focalFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            focalFrame.name = "RitualGate";
            focalFrame.transform.position = new Vector3(0f, 3.8f, 18f);
            focalFrame.transform.localScale = new Vector3(5.6f, 7.2f, 0.45f);
            focalFrame.GetComponent<Renderer>().sharedMaterial.color = new Color(0.18f, 0.19f, 0.25f);

            var focalCore = GameObject.CreatePrimitive(PrimitiveType.Cube);
            focalCore.name = "RitualCore";
            focalCore.transform.position = new Vector3(0f, 2.6f, 17.8f);
            focalCore.transform.localScale = new Vector3(2f, 3.6f, 0.22f);
            focalCore.GetComponent<Renderer>().sharedMaterial.color = new Color(0.18f, 0.44f, 0.88f);

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
            var nativeMotionGestureRecognizer = player.AddComponent<NativeMotionGestureRecognizer>();
            var externalBridge = player.AddComponent<ExternalGestureBridgeProvider>();
            var externalMotionGestureRecognizer = player.AddComponent<ExternalMotionGestureRecognizer>();
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
            camera.fieldOfView = 72f;
            camera.backgroundColor = new Color(0.02f, 0.03f, 0.07f);

            var motionGestureFeedbackBoard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            motionGestureFeedbackBoard.name = "MotionGestureFeedbackBoard";
            motionGestureFeedbackBoard.transform.position = new Vector3(0f, 2.35f, 4.2f);
            motionGestureFeedbackBoard.transform.localScale = new Vector3(2.9f, 1.12f, 1f);
            motionGestureFeedbackBoard.GetComponent<Renderer>().sharedMaterial.color = new Color(0.1f, 0.12f, 0.18f);

            var feedbackFrame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            feedbackFrame.name = "MotionGestureFrame";
            feedbackFrame.transform.SetParent(motionGestureFeedbackBoard.transform, false);
            feedbackFrame.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            feedbackFrame.transform.localScale = new Vector3(1.08f, 1.16f, 1f);
            feedbackFrame.GetComponent<Renderer>().sharedMaterial.color = new Color(0.82f, 0.62f, 0.24f);

            var motionGestureFeedbackLabel = new GameObject("Label").AddComponent<TextMesh>();
            motionGestureFeedbackLabel.transform.SetParent(motionGestureFeedbackBoard.transform, false);
            motionGestureFeedbackLabel.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            motionGestureFeedbackLabel.anchor = TextAnchor.MiddleCenter;
            motionGestureFeedbackLabel.alignment = TextAlignment.Center;
            motionGestureFeedbackLabel.fontSize = 68;
            motionGestureFeedbackLabel.characterSize = 0.055f;
            motionGestureFeedbackLabel.color = new Color(0.91f, 0.95f, 1f);
            motionGestureFeedbackLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            motionGestureFeedbackLabel.text = "SPELL GUARD\n动态手势待命";
            motionGestureFeedbackLabel.GetComponent<Renderer>().sortingOrder = 10;

            var arenaSign = new GameObject("ArenaSign").AddComponent<TextMesh>();
            arenaSign.transform.position = new Vector3(0f, 4.6f, 17.65f);
            arenaSign.anchor = TextAnchor.MiddleCenter;
            arenaSign.alignment = TextAlignment.Center;
            arenaSign.fontSize = 72;
            arenaSign.characterSize = 0.08f;
            arenaSign.color = new Color(1f, 0.84f, 0.45f);
            arenaSign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arenaSign.text = "SPELL GUARD\nRITUAL CHAMBER";

            var motionGestureFeedbackBoardComponent = motionGestureFeedbackBoard.AddComponent<MotionGestureFeedbackBoard>();

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
            SetField(sceneContext, "nativeMotionGestureRecognizer", nativeMotionGestureRecognizer);
            SetField(sceneContext, "externalBridge", externalBridge);
            SetField(sceneContext, "externalMotionGestureRecognizer", externalMotionGestureRecognizer);
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
            SetField(sceneContext, "motionGestureFeedbackBoard", motionGestureFeedbackBoardComponent);

            SetField(udpReceiver, "bridgeProvider", externalBridge);
            SetField(udpReceiver, "webcamFeed", webcamFeed);
            SetField(externalMotionGestureRecognizer, "bridgeProvider", externalBridge);
            SetField(nativeMediapipeProvider, "webcamFeed", webcamFeed);
            SetField(nativeMediapipeRunner, "targetProvider", nativeMediapipeProvider);
            SetField(nativeMediapipeRunner, "webcamFeed", webcamFeed);
            SetField(nativeMotionGestureRecognizer, "nativeProvider", nativeMediapipeProvider);

            SetField(motionGestureFeedbackBoardComponent, "inputProvider", inputRouter);
            SetField(motionGestureFeedbackBoardComponent, "faceCamera", camera);
            SetField(motionGestureFeedbackBoardComponent, "boardRenderer", motionGestureFeedbackBoard.GetComponent<Renderer>());
            SetField(motionGestureFeedbackBoardComponent, "labelText", motionGestureFeedbackLabel);

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

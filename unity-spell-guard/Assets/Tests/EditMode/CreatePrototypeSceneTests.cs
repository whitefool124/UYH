using SpellGuard.Core;
using SpellGuard.EditorTools;
using SpellGuard.InputSystem;
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
            var nativeRecognizer = playerRoot != null ? playerRoot.GetComponent<NativeMotionGestureRecognizer>() : null;
            var recognizer = playerRoot != null ? playerRoot.GetComponent<ExternalMotionGestureRecognizer>() : null;
            var nativeProvider = playerRoot != null ? playerRoot.GetComponent<NativeMediapipeGestureProvider>() : null;
            var bridgeProvider = playerRoot != null ? playerRoot.GetComponent<ExternalGestureBridgeProvider>() : null;
            var inputRouter = playerRoot != null ? playerRoot.GetComponent<GestureInputRouter>() : null;
            var sceneContext = Object.FindObjectOfType<SpellGuardSceneContext>();
            var feedbackBoard = Object.FindObjectOfType<MotionGestureFeedbackBoard>(true);

            Assert.That(playerRoot, Is.Not.Null, "PlayerRoot should exist in the generated prototype scene.");
            Assert.That(nativeRecognizer, Is.Not.Null, "Generated scene should explicitly include NativeMotionGestureRecognizer.");
            Assert.That(recognizer, Is.Not.Null, "Generated scene should explicitly include ExternalMotionGestureRecognizer.");
            Assert.That(sceneContext, Is.Not.Null, "SceneContext should exist in the generated prototype scene.");
            Assert.That(sceneContext.NativeMotionGestureRecognizer, Is.SameAs(nativeRecognizer));
            Assert.That(sceneContext.ExternalMotionGestureRecognizer, Is.SameAs(recognizer));
            Assert.That(feedbackBoard, Is.Not.Null, "Generated scene should include a world-space motion feedback board.");
            Assert.That(sceneContext.MotionGestureFeedbackBoard, Is.SameAs(feedbackBoard));

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
        }
    }
}

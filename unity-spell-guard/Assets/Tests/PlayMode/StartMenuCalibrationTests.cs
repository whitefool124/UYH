using NUnit.Framework;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class StartMenuCalibrationTests
    {
        private GameObject root;
        private SpellGuardStartMenuController startMenu;
        private NativeMediapipeGestureProvider nativeProvider;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("StartMenuCalibrationTestsRoot");
            startMenu = root.AddComponent<SpellGuardStartMenuController>();
            nativeProvider = root.AddComponent<NativeMediapipeGestureProvider>();
            SetPrivateField(startMenu, "nativeMediapipeProvider", nativeProvider);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void OpenCalibrationShowsNativeStatusText()
        {
            nativeProvider.SetStatusText("Native ready for test");
            nativeProvider.SetSnapshot(true, GestureType.Fist, new Vector2(0.5f, 0.5f), 0.82f);

            startMenu.OpenCalibration();
            var text = InvokeBuildCalibrationText(startMenu);

            Assert.That(text, Does.Contain("Camera: Not Ready"));
            Assert.That(text, Does.Contain("Native ready for test"));
            Assert.That(text, Does.Contain("Hand Detected: Yes"));
            Assert.That(text, Does.Contain("Gesture: 握拳"));
            Assert.That(text, Does.Contain("Confidence: 82%"));
        }

        [Test]
        public void CalibrationPreviewRectStaysInsideHeroPanel()
        {
            startMenu.OpenCalibration();
            var layout = InvokeGetLayout(startMenu);
            var preview = InvokeGetCalibrationPreviewRect(startMenu, layout);
            var hero = (Rect)layout.GetType().GetField("HeroPanel").GetValue(layout);

            Assert.That(preview.xMin, Is.GreaterThanOrEqualTo(hero.xMin));
            Assert.That(preview.xMax, Is.LessThanOrEqualTo(hero.xMax));
            Assert.That(preview.yMin, Is.GreaterThanOrEqualTo(hero.yMin));
            Assert.That(preview.yMax, Is.LessThanOrEqualTo(hero.yMax));
            Assert.That(preview.height, Is.GreaterThan(100f));
        }

        private static string InvokeBuildCalibrationText(SpellGuardStartMenuController target)
        {
            var method = typeof(SpellGuardStartMenuController).GetMethod("BuildCalibrationText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(target, null);
        }

        private static object InvokeGetLayout(SpellGuardStartMenuController target)
        {
            var method = typeof(SpellGuardStartMenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, null);
        }

        private static Rect InvokeGetCalibrationPreviewRect(SpellGuardStartMenuController target, object layout)
        {
            var method = typeof(SpellGuardStartMenuController).GetMethod("GetCalibrationPreviewRect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Rect)method.Invoke(target, new[] { layout });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class CustomGestureSystemTests
    {
        [Test]
        public void FeatureExtractorRejectsMissingLandmarks()
        {
            Assert.That(CustomGestureFeatureExtractor.TryExtract(new Vector2[5], 1f, 0.5f, out _), Is.False);
        }

        [Test]
        public void FeatureExtractorIsTranslationAndScaleStable()
        {
            var baseHand = BuildLandmarks(Vector2.zero, 1f, 0f);
            var movedScaledHand = BuildLandmarks(new Vector2(0.25f, -0.4f), 2.5f, 0f);

            Assert.That(CustomGestureFeatureExtractor.TryExtract(baseHand, 1f, 0.5f, out var first), Is.True);
            Assert.That(CustomGestureFeatureExtractor.TryExtract(movedScaledHand, 1f, 0.5f, out var second), Is.True);

            Assert.That(CustomGestureFeatureExtractor.Distance(first, second), Is.LessThan(0.001f));
        }

        [Test]
        public void LibrarySavesLoadsAndSkipsBadJson()
        {
            var folder = Path.Combine(Application.temporaryCachePath, "CustomGestureLibraryTests");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var library = new CustomGestureLibrary(folder);
            var template = BuildTemplate(GestureIntent.CustomGesture);

            Assert.That(library.Save(template), Is.True);
            File.WriteAllText(Path.Combine(folder, "broken.json"), "not-json");

            var reloaded = new CustomGestureLibrary(folder);
            reloaded.LoadAll();

            Assert.That(reloaded.Templates.Count, Is.EqualTo(1));
            Assert.That(reloaded.Templates[0].TargetIntent, Is.EqualTo(GestureIntent.CustomGesture));
            Assert.That(reloaded.Templates[0].RequiredHandedness, Is.EqualTo(GestureHandedness.Right));
        }

        [Test]
        public void DefaultLibraryUsesProjectGestureFolder()
        {
            var library = new CustomGestureLibrary();

            Assert.That(library.FolderPath, Does.Contain("ProjectGestureLibrary"));
            Assert.That(library.FolderPath, Does.Contain("CustomGestures"));
        }

        [Test]
        public void RecorderCapturesValidSingleHandSample()
        {
            var recorder = new CustomGestureRecorder();
            recorder.Configure(0f, 0.24f, 0.06f, 0.5f);
            recorder.Begin(10f);

            for (var index = 0; index < 6; index++)
            {
                recorder.Update(BuildFrame(10f + index * 0.06f, index * 0.02f), 10f + index * 0.06f);
            }

            Assert.That(recorder.LastSample, Is.Not.Null);
            Assert.That(recorder.LastSample.Frames.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(recorder.LastSample.Handedness, Is.EqualTo(GestureHandedness.Right));
        }

        [Test]
        public void RecorderRejectsFramesFromUnselectedHand()
        {
            var recorder = new CustomGestureRecorder();
            recorder.Configure(0f, 0.24f, 0.06f, 0.5f);
            recorder.SetTargetHandedness(GestureHandedness.Left);
            recorder.Begin(10f);

            for (var index = 0; index < 6; index++)
            {
                recorder.Update(BuildFrame(10f + index * 0.06f, index * 0.02f, GestureHandedness.Right), 10f + index * 0.06f);
            }

            Assert.That(recorder.LastSample, Is.Null);
            Assert.That(recorder.InvalidFrameCount, Is.GreaterThan(0));
        }

        [Test]
        public void RecognizerOutputsAllowedGestureActionForMatchingTemplate()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 1.6f, 0.1f);
            var template = BuildTemplate(GestureIntent.CustomGesture);
            var templates = new List<CustomGestureTemplate> { template };
            var matched = false;
            var action = GestureAction.None;

            for (var index = 0; index < 18; index++)
            {
                if (recognizer.TryResolve(BuildFrame(20f + index * 0.06f, index * 0.02f), templates, 20f + index * 0.06f, out var resolvedAction))
                {
                    matched = true;
                    action = resolvedAction;
                }
            }

            Assert.That(matched, Is.True);
            Assert.That(action.Intent, Is.EqualTo(GestureIntent.CustomGesture));
            Assert.That(action.SourceKind, Is.EqualTo(GestureCommandKind.Motion));
        }

        [Test]
        public void RecognizerOutputsCustomGestureForLegacySpellTemplate()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 1.6f, 0.1f);
            var templates = new List<CustomGestureTemplate> { BuildTemplate(GestureIntent.CastFire) };
            var action = GestureAction.None;

            for (var index = 0; index < 18; index++)
            {
                if (recognizer.TryResolve(BuildFrame(25f + index * 0.06f, index * 0.02f), templates, 25f + index * 0.06f, out var resolvedAction))
                {
                    action = resolvedAction;
                }
            }

            Assert.That(action.Intent, Is.EqualTo(GestureIntent.CustomGesture));
        }

        [Test]
        public void RecognizerRejectsAmbiguousSimilarTemplates()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 1.6f, 0.1f);
            var templates = new List<CustomGestureTemplate>
            {
                BuildTemplate(GestureIntent.CastFire),
                BuildTemplate(GestureIntent.CastShield, "custom_conflict", 0.002f)
            };

            var matched = false;
            for (var index = 0; index < 18; index++)
            {
                matched = recognizer.TryResolve(BuildFrame(30f + index * 0.06f, index * 0.02f), templates, 30f + index * 0.06f, out _) || matched;
            }

            Assert.That(matched, Is.False);
            Assert.That(recognizer.LastMatchedName, Is.EqualTo("相近手势冲突"));
        }

        [Test]
        public void RecognizerRejectsOppositeHandedTemplate()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 1.6f, 0.1f);
            var templates = new List<CustomGestureTemplate> { BuildTemplate(GestureIntent.CastFire) };
            var matched = false;

            for (var index = 0; index < 18; index++)
            {
                matched = recognizer.TryResolve(BuildFrame(40f + index * 0.06f, index * 0.02f, GestureHandedness.Left), templates, 40f + index * 0.06f, out _) || matched;
            }

            Assert.That(matched, Is.False);
        }

        [Test]
        public void LibraryNormalizesUnsupportedIntentToCustomGesture()
        {
            var folder = Path.Combine(Application.temporaryCachePath, "CustomGestureRejectTests");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var library = new CustomGestureLibrary(folder);
            var template = BuildTemplate(GestureIntent.MoveLeft);

            Assert.That(library.Save(template), Is.True);
            Assert.That(library.Templates[0].TargetIntent, Is.EqualTo(GestureIntent.CustomGesture));
        }

        private static CustomGestureTemplate BuildTemplate(GestureIntent intent, string gestureId = "custom_test", float phaseOffset = 0f)
        {
            var frames = new List<CustomGestureFrameSample>();
            for (var index = 0; index < 18; index++)
            {
                frames.Add(new CustomGestureFrameSample
                {
                    Time = index * 0.06f,
                    Confidence = 1f,
                    Landmarks = BuildLandmarks(Vector2.zero, 1f, index * 0.02f + phaseOffset)
                });
            }

            return new CustomGestureTemplate
            {
                GestureId = gestureId,
                DisplayName = "Custom Test",
                Kind = CustomGestureKind.DynamicMotion,
                TargetIntent = intent,
                MatchThreshold = CustomGestureRecognizer.DefaultDynamicThreshold,
                Samples = new List<CustomGestureSample>
                {
                    new CustomGestureSample
                    {
                        SampleId = "sample_test",
                        Handedness = GestureHandedness.Right,
                        DurationSeconds = 1.08f,
                        Frames = frames
                    }
                }
            };
        }

        private static GestureFrame BuildFrame(float time, float phase, GestureHandedness handedness = GestureHandedness.Right)
        {
            var landmarks = BuildLandmarks(Vector2.zero, 1f, phase);
            var snapshot = new GestureSnapshot
            {
                HandPresent = true,
                Gesture = GestureType.OpenPalm,
                ViewportPosition = new Vector2(0.5f, 0.5f),
                Confidence = 1f
            };

            return LegacyGestureRuntimeAdapter.BuildSingleHandFrame(snapshot, landmarks, Mathf.RoundToInt(time * 100f), time, GestureSourceKind.Mock, MotionGestureEvent.None, handedness, 7);
        }

        private static Vector2[] BuildLandmarks(Vector2 offset, float scale, float phase)
        {
            var landmarks = new Vector2[CustomGestureFeatureExtractor.RequiredLandmarkCount];
            landmarks[0] = offset;
            for (var index = 1; index < landmarks.Length; index++)
            {
                var finger = (index - 1) / 4;
                var joint = (index - 1) % 4 + 1;
                var x = (finger - 2) * 0.12f + phase * (0.15f + finger * 0.02f);
                var y = joint * 0.11f + Mathf.Sin(phase + finger * 0.3f) * 0.02f;
                landmarks[index] = offset + new Vector2(x, y) * scale;
            }

            return landmarks;
        }
    }
}

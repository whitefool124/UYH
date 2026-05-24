using System;
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
        public void LibraryLoadsWrappedOrDirectTemplateJson()
        {
            var folder = Path.Combine(Application.temporaryCachePath, "CustomGestureLibraryFormatTests");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);
            var template = BuildTemplate(GestureIntent.CastShield, "wrapped_template");
            File.WriteAllText(Path.Combine(folder, "wrapped.json"), JsonUtility.ToJson(new WrappedTemplate { Template = template }, true));
            File.WriteAllText(Path.Combine(folder, "direct.json"), JsonUtility.ToJson(template, true));

            var library = new CustomGestureLibrary(folder);
            library.LoadAll();

            Assert.That(library.Templates.Count, Is.EqualTo(2));
            Assert.That(library.Templates[0].TargetIntent, Is.EqualTo(GestureIntent.CastShield));
            Assert.That(library.Templates[1].TargetIntent, Is.EqualTo(GestureIntent.CastShield));
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
        public void DynamicTemplateBuildsTrajectoryTemplatesForDtwMatching()
        {
            var template = BuildTemplate(GestureIntent.CustomGesture);

            Assert.That(template.TrajectoryTemplates, Is.Not.Null);
            Assert.That(template.TrajectoryTemplates.Count, Is.GreaterThan(0));
            Assert.That(template.TrajectoryTemplates[0].Points.Length, Is.GreaterThanOrEqualTo(16));
        }

        [Test]
        public void DynamicTemplatePersistsInferredRule()
        {
            var folder = Path.Combine(Application.temporaryCachePath, "CustomGestureDynamicRuleLibraryTests");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var library = new CustomGestureLibrary(folder);
            var template = BuildTemplate(GestureIntent.CustomGesture);

            Assert.That(library.Save(template), Is.True);

            var reloaded = new CustomGestureLibrary(folder);
            reloaded.LoadAll();

            Assert.That(reloaded.Templates.Count, Is.EqualTo(1));
            Assert.That(reloaded.Templates[0].DynamicRule, Is.Not.Null);
            Assert.That(reloaded.Templates[0].DynamicRule.Pattern, Is.EqualTo(template.DynamicRule.Pattern));
            Assert.That(reloaded.Templates[0].DynamicRule.Direction, Is.EqualTo(template.DynamicRule.Direction));
        }

        [Test]
        public void DtwRecognizerRejectsReversedDynamicTrajectory()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 1.6f, 0.1f);
            var templates = new List<CustomGestureTemplate> { BuildTemplate(GestureIntent.CustomGesture) };
            var matched = false;

            for (var index = 0; index < 18; index++)
            {
                var phase = 0.34f - index * 0.02f;
                matched = recognizer.TryResolve(BuildFrame(22f + index * 0.06f, phase), templates, 22f + index * 0.06f, out _) || matched;
            }

            Assert.That(matched, Is.False);
        }

        [Test]
        public void DtwRecognizerMatchesGestureInsideNoisyValidationWindow()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 2.4f, 0.1f);
            var templates = new List<CustomGestureTemplate> { BuildTemplate(GestureIntent.CustomGesture) };
            var matched = false;

            for (var index = 0; index < 6; index++)
            {
                matched = recognizer.TryResolve(BuildFrame(24f + index * 0.06f, 0f), templates, 24f + index * 0.06f, out _) || matched;
            }

            for (var index = 0; index < 18; index++)
            {
                matched = recognizer.TryResolve(BuildFrame(24.4f + index * 0.06f, index * 0.02f), templates, 24.4f + index * 0.06f, out _) || matched;
            }

            Assert.That(matched, Is.True);
        }

        [Test]
        public void RecognizerIgnoresRepeatedReadsOfSameInputFrame()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 2.4f, 0.1f);
            var templates = new List<CustomGestureTemplate> { BuildTemplate(GestureIntent.CustomGesture) };
            var matched = false;

            for (var index = 0; index < 18; index++)
            {
                var frame = BuildFrame(60f + index * 0.06f, index * 0.02f);
                for (var repeat = 0; repeat < 4; repeat++)
                {
                    matched = recognizer.TryResolve(frame, templates, 60f + index * 0.06f + repeat * 0.01f, out _) || matched;
                }
            }

            Assert.That(matched, Is.True);
        }

        [Test]
        public void RecognizerOutputsTargetIntentForMatchingTemplate()
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

            Assert.That(action.Intent, Is.EqualTo(GestureIntent.CastFire));
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
        public void LibraryPreservesMovementIntentForValidationTemplates()
        {
            var folder = Path.Combine(Application.temporaryCachePath, "CustomGestureRejectTests");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var library = new CustomGestureLibrary(folder);
            var template = BuildTemplate(GestureIntent.MoveLeft);

            Assert.That(library.Save(template), Is.True);
            Assert.That(library.Templates[0].TargetIntent, Is.EqualTo(GestureIntent.MoveLeft));
        }

        [Test]
        public void LibraryPreservesStaticPoseTemplateKind()
        {
            var folder = Path.Combine(Application.temporaryCachePath, "CustomGestureStaticLibraryTests");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var library = new CustomGestureLibrary(folder);
            var template = BuildStaticTemplate(GestureIntent.CustomGesture);

            Assert.That(library.Save(template), Is.True);

            var reloaded = new CustomGestureLibrary(folder);
            reloaded.LoadAll();

            Assert.That(reloaded.Templates.Count, Is.EqualTo(1));
            Assert.That(reloaded.Templates[0].Kind, Is.EqualTo(CustomGestureKind.StaticPose));
            Assert.That(reloaded.Templates[0].MatchThreshold, Is.EqualTo(CustomGestureRecognizer.DefaultStaticThreshold));
        }

        [Test]
        public void LibraryPreservesGameplayIntentForCustomGestureTemplates()
        {
            var folder = Path.Combine(Application.temporaryCachePath, "CustomGestureIntentLibraryTests");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var library = new CustomGestureLibrary(folder);
            var template = BuildTemplate(GestureIntent.CastIce);

            Assert.That(library.Save(template), Is.True);

            var reloaded = new CustomGestureLibrary(folder);
            reloaded.LoadAll();

            Assert.That(reloaded.Templates.Count, Is.EqualTo(1));
            Assert.That(reloaded.Templates[0].TargetIntent, Is.EqualTo(GestureIntent.CastIce));
        }

        [Test]
        public void RecognizerOutputsStaticPoseActionForMatchingTemplate()
        {
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 1.6f, 0.1f);
            var templates = new List<CustomGestureTemplate> { BuildStaticTemplate(GestureIntent.CustomGesture) };

            Assert.That(recognizer.TryResolve(BuildFrame(50f, 0f), templates, 50f, out var action), Is.True);
            Assert.That(action.Intent, Is.EqualTo(GestureIntent.CustomGesture));
            Assert.That(action.SourceKind, Is.EqualTo(GestureCommandKind.StaticPose));
        }

        [Test]
        public void DynamicRuleEvaluatorInfersReasonableRulePerSample()
        {
            var samples = new List<CustomGestureSample>
            {
                BuildDynamicSample(0.2f),
                BuildDynamicSample(0.6f)
            };

            var rule = CustomGestureDynamicRuleEvaluator.InferRule(samples);

            Assert.That(rule.Pattern, Is.EqualTo(CustomGestureDynamicPattern.Directional).Or.EqualTo(CustomGestureDynamicPattern.Repeat));
            Assert.That(rule.MinimumDistance, Is.LessThan(0.2f));
            Assert.That(rule.RepeatCount, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void DynamicRuleEvaluatorAcceptsLooseDirectionalOpenPalmMotion()
        {
            var rule = new CustomGestureDynamicRule
            {
                Pattern = CustomGestureDynamicPattern.Directional,
                Direction = CustomGestureMotionDirection.LeftToRight,
                RequireOpenPalm = true,
                MinimumOpenPalmRatio = 0.65f,
                MinimumDistance = 0.08f,
                MaximumDrift = 0.14f,
                MinimumDuration = 0.05f,
                MaximumDuration = 2f
            };
            var frames = new List<CustomGestureFrameSample>();
            for (var index = 0; index < 8; index++)
            {
                frames.Add(new CustomGestureFrameSample
                {
                    Time = index * 0.08f,
                    Confidence = 1f,
                    StaticGesture = GestureType.OpenPalm,
                    PalmCenter = new Vector2(0.45f + index * 0.02f, 0.5f + Mathf.Sin(index) * 0.005f),
                    Landmarks = BuildLandmarks(Vector2.zero, 1f, index * 0.01f)
                });
            }

            Assert.That(CustomGestureDynamicRuleEvaluator.TryMatch(rule, frames, 0.5f, out var confidence), Is.True);
            Assert.That(confidence, Is.GreaterThan(0.5f));
        }

        [Test]
        public void ProjectVideoTemplatesValidateTheirOwnRecordedSamples()
        {
            var folder = Path.Combine(Application.dataPath, "ProjectGestureLibrary", "CustomGestures");
            if (!Directory.Exists(folder))
            {
                Assert.Ignore("Project gesture library is not available in this test environment.");
            }

            var library = new CustomGestureLibrary(folder);
            library.LoadAll();
            var checkedTemplates = 0;
            var failures = new List<string>();

            for (var templateIndex = 0; templateIndex < library.Templates.Count; templateIndex++)
            {
                var template = library.Templates[templateIndex];
                if (template == null || template.Kind != CustomGestureKind.DynamicMotion || string.IsNullOrWhiteSpace(template.GestureId) || !template.GestureId.StartsWith("ext_motion_", StringComparison.Ordinal))
                {
                    continue;
                }

                checkedTemplates += 1;
                var recognizer = new CustomGestureRecognizer();
                recognizer.Configure(0.35f, 2.4f, 0.05f);
                var matched = false;
                var now = 100f;
                var sample = template.Samples != null && template.Samples.Count > 0 ? template.Samples[0] : null;
                if (sample?.Frames != null)
                {
                    for (var frameIndex = 0; frameIndex < sample.Frames.Count; frameIndex++)
                    {
                        var frameSample = sample.Frames[frameIndex];
                        var frame = BuildFrameFromSample(frameSample, frameIndex + 1, sample.Handedness);
                        matched = recognizer.TryResolveSingle(frame, template, now + frameSample.Time) || matched;
                    }
                }

                if (!matched)
                {
                    failures.Add($"{template.GestureId}: {recognizer.LastFailureReason}");
                }
            }

            Assert.That(checkedTemplates, Is.GreaterThan(0));
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private static CustomGestureSample BuildDynamicSample(float startOffset)
        {
            var frames = new List<CustomGestureFrameSample>();
            for (var index = 0; index < 10; index++)
            {
                frames.Add(new CustomGestureFrameSample
                {
                    Time = index * 0.08f,
                    Confidence = 1f,
                    StaticGesture = GestureType.OpenPalm,
                    PalmCenter = new Vector2(0.35f + index * 0.02f + startOffset, 0.5f + Mathf.Sin(index * 0.6f) * 0.005f),
                    Landmarks = BuildLandmarks(Vector2.zero, 1f, index * 0.02f + startOffset)
                });
            }

            return new CustomGestureSample
            {
                SampleId = $"sample_{startOffset:0.00}",
                Handedness = GestureHandedness.Right,
                DurationSeconds = 0.72f,
                Frames = frames
            };
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
                    StaticGesture = GestureType.OpenPalm,
                    PalmCenter = new Vector2(0.5f + index * 0.01f + phaseOffset * 0.1f, 0.5f),
                    Landmarks = BuildLandmarks(Vector2.zero, 1f, index * 0.02f + phaseOffset)
                });
            }

            var sample = new CustomGestureSample
            {
                SampleId = "sample_test",
                Handedness = GestureHandedness.Right,
                DurationSeconds = 1.08f,
                Frames = frames
            };
            var samples = new List<CustomGestureSample> { sample };

            return new CustomGestureTemplate
            {
                GestureId = gestureId,
                DisplayName = "Custom Test",
                Kind = CustomGestureKind.DynamicMotion,
                TargetIntent = intent,
                MatchThreshold = CustomGestureRecognizer.DefaultDynamicThreshold,
                DynamicRule = new CustomGestureDynamicRule
                {
                    Pattern = CustomGestureDynamicPattern.Directional,
                    Direction = CustomGestureMotionDirection.LeftToRight,
                    RequireOpenPalm = true,
                    MinimumOpenPalmRatio = 0.65f,
                    MinimumDistance = 0.08f,
                    MaximumDrift = 0.14f,
                    MinimumDuration = 0.05f,
                    MaximumDuration = 2f
                },
                TrajectoryTemplates = CustomGestureTrajectoryTemplateBuilder.Build(samples),
                Samples = samples
            };
        }

        private static CustomGestureTemplate BuildStaticTemplate(GestureIntent intent)
        {
            return new CustomGestureTemplate
            {
                GestureId = "custom_static_test",
                DisplayName = "Custom Static Test",
                Kind = CustomGestureKind.StaticPose,
                TargetIntent = intent,
                MatchThreshold = 0f,
                Samples = new List<CustomGestureSample>
                {
                    new CustomGestureSample
                    {
                        SampleId = "static_sample_test",
                        Handedness = GestureHandedness.Right,
                        DurationSeconds = 0.12f,
                        Frames = new List<CustomGestureFrameSample>
                        {
                            new CustomGestureFrameSample
                            {
                                Time = 0f,
                                Confidence = 1f,
                                Landmarks = BuildLandmarks(Vector2.zero, 1f, 0f)
                            }
                        }
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

        private static GestureFrame BuildFrameFromSample(CustomGestureFrameSample sample, int frameId, GestureHandedness handedness)
        {
            var snapshot = new GestureSnapshot
            {
                HandPresent = true,
                Gesture = sample.StaticGesture,
                ViewportPosition = sample.PalmCenter,
                Confidence = sample.Confidence
            };

            return LegacyGestureRuntimeAdapter.BuildSingleHandFrame(snapshot, sample.Landmarks, frameId, sample.Time, GestureSourceKind.ExternalBridge, MotionGestureEvent.None, handedness, 1);
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

        [Serializable]
        private sealed class WrappedTemplate
        {
            public CustomGestureTemplate Template;
        }
    }
}

using System.IO;
using NUnit.Framework;
using SpellGuard.Tools;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class CustomGestureBatchTesterTests
    {
        [Test]
        public void BatchTesterBuildsTemplateAndEvaluatesHeldOutClip()
        {
            var dataset = BuildDataset();

            var report = CustomGestureBatchTester.Run(dataset);

            Assert.That(report.TemplateCount, Is.EqualTo(1));
            Assert.That(report.EvaluatedClips, Is.EqualTo(1));
            Assert.That(report.CorrectClips, Is.EqualTo(1));
            Assert.That(report.Accuracy, Is.EqualTo(1f));
        }

        [Test]
        public void BatchTesterWritesSummaryAndCsv()
        {
            var datasetPath = Path.Combine(Application.temporaryCachePath, "custom_gesture_batch_fixture.json");
            var outputPath = Path.Combine(Application.temporaryCachePath, "custom_gesture_batch_report");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            File.WriteAllText(datasetPath, JsonUtility.ToJson(BuildDataset(), true));

            var report = CustomGestureBatchTester.RunFromFile(datasetPath, outputPath);

            Assert.That(report.CorrectClips, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(outputPath, "custom_gesture_batch_summary.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(outputPath, "custom_gesture_batch_results.csv")), Is.True);
        }

        [Test]
        public void BatchTesterRunsGeneratedJesterSubsetWhenPresent()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var workspaceRoot = projectRoot == null ? null : Directory.GetParent(projectRoot)?.FullName;
            var datasetPath = workspaceRoot == null
                ? string.Empty
                : Path.Combine(workspaceRoot, "build-temp", "jester_mined_motion_subset.json");
            if (!File.Exists(datasetPath) && workspaceRoot != null)
            {
                datasetPath = Path.Combine(workspaceRoot, "build-temp", "jester_spellguard_subset_placeholder.json");
            }

            if (!File.Exists(datasetPath))
            {
                Assert.Ignore("Generated Jester subset json is not present.");
            }

            var outputPath = Path.Combine(workspaceRoot, "build-temp", "jester_spellguard_reports");
            var report = CustomGestureBatchTester.RunFromFile(datasetPath, outputPath);

            Assert.That(report.TemplateCount, Is.GreaterThan(0));
            Assert.That(report.EvaluatedClips, Is.GreaterThan(0));
            Assert.That(File.Exists(Path.Combine(outputPath, "custom_gesture_batch_results.csv")), Is.True);
        }

        private static CustomGestureBatchDataset BuildDataset()
        {
            var dataset = new CustomGestureBatchDataset
            {
                DatasetName = "fixture",
                DefaultFps = 30f
            };
            dataset.Clips.Add(BuildClip("train_1", "open_palm_right", "train", 0f));
            dataset.Clips.Add(BuildClip("test_1", "open_palm_right", "test", 0.01f));
            return dataset;
        }

        private static CustomGestureBatchClip BuildClip(string clipId, string label, string split, float phaseOffset)
        {
            var clip = new CustomGestureBatchClip
            {
                ClipId = clipId,
                Label = label,
                Split = split,
                Handedness = "Right",
                Fps = 30f
            };

            for (var frameIndex = 0; frameIndex < 18; frameIndex++)
            {
                var frame = new CustomGestureBatchFrame
                {
                    Time = frameIndex * 0.06f,
                    Confidence = 1f,
                    StaticGesture = "OpenPalm"
                };

                var landmarks = BuildLandmarks(frameIndex * 0.02f + phaseOffset);
                for (var pointIndex = 0; pointIndex < landmarks.Length; pointIndex++)
                {
                    frame.Landmarks.Add(new CustomGestureBatchPoint
                    {
                        X = landmarks[pointIndex].x,
                        Y = landmarks[pointIndex].y
                    });
                }

                clip.Frames.Add(frame);
            }

            return clip;
        }

        private static Vector2[] BuildLandmarks(float phase)
        {
            var landmarks = new Vector2[21];
            landmarks[0] = Vector2.zero;
            for (var index = 1; index < landmarks.Length; index++)
            {
                var finger = (index - 1) / 4;
                var joint = (index - 1) % 4 + 1;
                var x = (finger - 2) * 0.12f + phase * (0.15f + finger * 0.02f);
                var y = joint * 0.11f + Mathf.Sin(phase + finger * 0.3f) * 0.02f;
                landmarks[index] = new Vector2(x, y);
            }

            return landmarks;
        }
    }
}

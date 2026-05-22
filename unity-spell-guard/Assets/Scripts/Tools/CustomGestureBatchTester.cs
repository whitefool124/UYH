using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tools
{
    public static class CustomGestureBatchTester
    {
        public static CustomGestureBatchReport Run(CustomGestureBatchDataset dataset, CustomGestureBatchOptions options = null)
        {
            if (dataset == null)
            {
                throw new ArgumentNullException(nameof(dataset));
            }

            options ??= new CustomGestureBatchOptions();
            var templates = BuildTemplates(dataset, options);
            var report = new CustomGestureBatchReport
            {
                DatasetName = dataset.DatasetName,
                TemplateCount = templates.Count
            };

            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(options.MinimumConfidence, options.WindowSeconds, options.CooldownSeconds);

            for (var clipIndex = 0; clipIndex < dataset.Clips.Count; clipIndex++)
            {
                var clip = dataset.Clips[clipIndex];
                if (clip == null || clip.IsTrain || !ShouldEvaluateSplit(clip, options))
                {
                    continue;
                }

                recognizer.Reset();
                var result = EvaluateClip(clip, dataset.DefaultFps, templates, recognizer);
                report.Results.Add(result);
            }

            report.Recalculate();
            return report;
        }

        public static CustomGestureBatchReport RunFromFile(string datasetPath, string outputDirectory, CustomGestureBatchOptions options = null)
        {
            var dataset = CustomGestureBatchDataset.LoadFromFile(datasetPath);
            var report = Run(dataset, options);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "custom_gesture_batch_summary.json"), JsonUtility.ToJson(report, true));
                File.WriteAllText(Path.Combine(outputDirectory, "custom_gesture_batch_results.csv"), report.ToCsv());
            }

            return report;
        }

        private static List<CustomGestureTemplate> BuildTemplates(CustomGestureBatchDataset dataset, CustomGestureBatchOptions options)
        {
            var samplesByLabel = new Dictionary<string, List<CustomGestureSample>>(StringComparer.OrdinalIgnoreCase);
            for (var clipIndex = 0; clipIndex < dataset.Clips.Count; clipIndex++)
            {
                var clip = dataset.Clips[clipIndex];
                if (clip == null || !clip.IsTrain || string.IsNullOrWhiteSpace(clip.Label))
                {
                    continue;
                }

                var sample = clip.ToSample(dataset.DefaultFps);
                if (sample == null)
                {
                    continue;
                }

                if (!samplesByLabel.TryGetValue(clip.Label, out var samples))
                {
                    samples = new List<CustomGestureSample>();
                    samplesByLabel.Add(clip.Label, samples);
                }

                if (samples.Count < options.MaxTrainSamplesPerLabel)
                {
                    samples.Add(sample);
                }
            }

            var templates = new List<CustomGestureTemplate>();
            foreach (var entry in samplesByLabel)
            {
                if (entry.Value.Count < options.MinTrainSamplesPerLabel)
                {
                    continue;
                }

                var template = new CustomGestureTemplate
                {
                    GestureId = SanitizeId(entry.Key),
                    DisplayName = entry.Key,
                    Kind = CustomGestureKind.DynamicMotion,
                    RequiredHandedness = ResolveHandedness(entry.Value),
                    TargetIntent = GestureIntent.CustomGesture,
                    MatchThreshold = options.MatchThreshold,
                    Samples = new List<CustomGestureSample>(entry.Value),
                    TrajectoryTemplates = CustomGestureTrajectoryTemplateBuilder.Build(entry.Value),
                    DynamicRule = CustomGestureDynamicRuleEvaluator.InferRule(entry.Value)
                };

                if (template.TrajectoryTemplates != null && template.TrajectoryTemplates.Count > 0)
                {
                    templates.Add(template);
                }
            }

            return templates;
        }

        private static CustomGestureBatchClipResult EvaluateClip(CustomGestureBatchClip clip, float defaultFps, IReadOnlyList<CustomGestureTemplate> templates, CustomGestureRecognizer recognizer)
        {
            var frames = clip.ToRuntimeFrames(defaultFps);
            var matched = false;
            var matchedName = string.Empty;
            var bestScore = float.PositiveInfinity;
            var triggeredAt = -1f;

            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                if (recognizer.TryResolve(frame, templates, frame.Timestamp, out _))
                {
                    matched = true;
                    matchedName = recognizer.LastMatchedName;
                    triggeredAt = frame.Timestamp;
                    bestScore = recognizer.LastScore;
                    break;
                }

                if (recognizer.LastScore < bestScore)
                {
                    bestScore = recognizer.LastScore;
                    matchedName = recognizer.LastMatchedName;
                }
            }

            return new CustomGestureBatchClipResult
            {
                ClipId = clip.ClipId,
                Label = clip.Label,
                Split = clip.Split,
                FrameCount = frames.Count,
                Matched = matched,
                MatchedLabel = matchedName,
                IsCorrect = matched && string.Equals(clip.Label, matchedName, StringComparison.OrdinalIgnoreCase),
                BestScore = bestScore,
                TriggeredAt = triggeredAt
            };
        }

        private static bool ShouldEvaluateSplit(CustomGestureBatchClip clip, CustomGestureBatchOptions options)
        {
            if (options.IncludeValidationSplit && clip.IsValidation)
            {
                return true;
            }

            return clip.IsTest;
        }

        private static GestureHandedness ResolveHandedness(IReadOnlyList<CustomGestureSample> samples)
        {
            for (var index = 0; index < samples.Count; index++)
            {
                if (samples[index] != null && samples[index].Handedness != GestureHandedness.Unknown)
                {
                    return samples[index].Handedness;
                }
            }

            return GestureHandedness.Unknown;
        }

        private static string SanitizeId(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "custom_batch_template";
            }

            var chars = label.Trim().ToLowerInvariant().ToCharArray();
            for (var index = 0; index < chars.Length; index++)
            {
                if (!char.IsLetterOrDigit(chars[index]))
                {
                    chars[index] = '_';
                }
            }

            return "batch_" + new string(chars);
        }
    }

    [Serializable]
    public sealed class CustomGestureBatchOptions
    {
        public int MinTrainSamplesPerLabel = 1;
        public int MaxTrainSamplesPerLabel = 5;
        public float MinimumConfidence = 0.5f;
        public float WindowSeconds = 2.4f;
        public float CooldownSeconds = 0.1f;
        public float MatchThreshold = CustomGestureRecognizer.DefaultDynamicThreshold;
        public bool IncludeValidationSplit;
    }

    [Serializable]
    public sealed class CustomGestureBatchReport
    {
        public string DatasetName;
        public int TemplateCount;
        public int EvaluatedClips;
        public int CorrectClips;
        public int MissedClips;
        public int FalseMatchedClips;
        public float Accuracy;
        public List<CustomGestureBatchClipResult> Results = new List<CustomGestureBatchClipResult>();

        public void Recalculate()
        {
            EvaluatedClips = Results?.Count ?? 0;
            CorrectClips = 0;
            MissedClips = 0;
            FalseMatchedClips = 0;
            if (Results != null)
            {
                for (var index = 0; index < Results.Count; index++)
                {
                    var result = Results[index];
                    if (result.IsCorrect)
                    {
                        CorrectClips += 1;
                    }
                    else if (!result.Matched)
                    {
                        MissedClips += 1;
                    }
                    else
                    {
                        FalseMatchedClips += 1;
                    }
                }
            }

            Accuracy = EvaluatedClips > 0 ? CorrectClips / (float)EvaluatedClips : 0f;
        }

        public string ToCsv()
        {
            var writer = new System.Text.StringBuilder();
            writer.AppendLine("clip_id,label,split,frames,matched,matched_label,correct,best_score,triggered_at");
            if (Results == null)
            {
                return writer.ToString();
            }

            for (var index = 0; index < Results.Count; index++)
            {
                var result = Results[index];
                writer.Append(Escape(result.ClipId));
                writer.Append(',');
                writer.Append(Escape(result.Label));
                writer.Append(',');
                writer.Append(Escape(result.Split));
                writer.Append(',');
                writer.Append(result.FrameCount);
                writer.Append(',');
                writer.Append(result.Matched);
                writer.Append(',');
                writer.Append(Escape(result.MatchedLabel));
                writer.Append(',');
                writer.Append(result.IsCorrect);
                writer.Append(',');
                writer.Append(float.IsInfinity(result.BestScore) ? "Infinity" : result.BestScore.ToString("F6", CultureInfo.InvariantCulture));
                writer.Append(',');
                writer.Append(result.TriggeredAt.ToString("F3", CultureInfo.InvariantCulture));
                writer.AppendLine();
            }

            return writer.ToString();
        }

        private static string Escape(string value)
        {
            value ??= string.Empty;
            return value.Contains(",") || value.Contains("\"") || value.Contains("\n")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }

    [Serializable]
    public sealed class CustomGestureBatchClipResult
    {
        public string ClipId;
        public string Label;
        public string Split;
        public int FrameCount;
        public bool Matched;
        public string MatchedLabel;
        public bool IsCorrect;
        public float BestScore;
        public float TriggeredAt;
    }
}

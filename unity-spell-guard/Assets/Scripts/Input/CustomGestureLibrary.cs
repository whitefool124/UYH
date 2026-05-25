using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public sealed class CustomGestureLibrary
    {
        private const string FolderName = "CustomGestures";
        private const string ProjectLibraryFolder = "ProjectGestureLibrary";
        private readonly List<CustomGestureTemplate> templates = new List<CustomGestureTemplate>();
        private readonly List<CustomGestureTemplateValidationReport> validationReports = new List<CustomGestureTemplateValidationReport>();
        private readonly string folderPath;

        [Serializable]
        private sealed class TemplateFile
        {
            public CustomGestureTemplate Template;
        }

        public CustomGestureLibrary() : this(GetDefaultProjectLibraryPath())
        {
        }

        public CustomGestureLibrary(string folderPath)
        {
            this.folderPath = folderPath;
        }

        public IReadOnlyList<CustomGestureTemplate> Templates => templates;
        public IReadOnlyList<CustomGestureTemplateValidationReport> ValidationReports => validationReports;
        public string FolderPath => folderPath;

        public void LoadAll()
        {
            templates.Clear();
            validationReports.Clear();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            var files = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < files.Length; index++)
            {
                if (TryLoad(files[index], out var template))
                {
                    var report = ValidateTemplate(template);
                    validationReports.Add(report);
                    if (report.Active)
                    {
                        templates.Add(template);
                    }
                }
            }
        }

        public bool Save(CustomGestureTemplate template)
        {
            if (!TrySanitizeTemplate(template, out var sanitized))
            {
                return false;
            }

            Directory.CreateDirectory(folderPath);
            var path = GetTemplatePath(sanitized.GestureId);
            var wrapper = new TemplateFile { Template = sanitized };
            File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
            var report = ValidateTemplate(sanitized);
            if (report.Active)
            {
                ReplaceInMemory(sanitized);
            }
            else
            {
                templates.RemoveAll(templateInMemory => string.Equals(templateInMemory.GestureId, sanitized.GestureId, StringComparison.OrdinalIgnoreCase));
            }
            ReplaceValidationReport(report);
            return true;
        }

        public bool Delete(string gestureId)
        {
            var sanitizedId = SanitizeId(gestureId);
            if (string.IsNullOrWhiteSpace(sanitizedId))
            {
                return false;
            }

            templates.RemoveAll(template => string.Equals(template.GestureId, sanitizedId, StringComparison.OrdinalIgnoreCase));
            validationReports.RemoveAll(report => string.Equals(report.GestureId, sanitizedId, StringComparison.OrdinalIgnoreCase));
            var path = GetTemplatePath(sanitizedId);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        public static CustomGestureTemplateValidationReport ValidateTemplate(CustomGestureTemplate template)
        {
            var report = new CustomGestureTemplateValidationReport
            {
                GestureId = template?.GestureId ?? string.Empty,
                DisplayName = template?.DisplayName ?? string.Empty,
                Pattern = template?.DynamicRule != null
                    ? CustomGestureDynamicPatternUtility.Normalize(template.DynamicRule.Pattern)
                    : CustomGestureDynamicPattern.PalmTrajectory,
                Active = false,
                FailureReason = string.Empty,
                Samples = new List<CustomGestureTemplateValidationSampleResult>()
            };

            if (template == null)
            {
                report.FailureReason = "template is null";
                return report;
            }

            if (!IsAllowedTargetIntent(template.TargetIntent))
            {
                report.FailureReason = "target intent is not allowed";
                return report;
            }

            if (template.Samples == null || template.Samples.Count == 0)
            {
                report.FailureReason = "template has no recorded samples";
                return report;
            }

            for (var sampleIndex = 0; sampleIndex < template.Samples.Count; sampleIndex++)
            {
                var sample = template.Samples[sampleIndex];
                var sampleResult = ValidateSample(template, sample);
                report.Samples.Add(sampleResult);
                report.SampleCount += 1;
                if (sampleResult.Matched)
                {
                    report.MatchedSampleCount += 1;
                }
            }

            report.Active = report.SampleCount > 0 && report.MatchedSampleCount == report.SampleCount;
            if (!report.Active)
            {
                report.FailureReason = report.SampleCount == 0
                    ? "template has no usable sample frames"
                    : $"{report.MatchedSampleCount}/{report.SampleCount} samples matched their template";
            }

            return report;
        }

        private static CustomGestureTemplateValidationSampleResult ValidateSample(CustomGestureTemplate template, CustomGestureSample sample)
        {
            var result = new CustomGestureTemplateValidationSampleResult
            {
                SampleId = sample?.SampleId ?? string.Empty,
                Threshold = template?.MatchThreshold ?? 0f,
                BestScore = float.PositiveInfinity,
                TriggeredAt = -1f,
                FailureReason = string.Empty
            };

            if (template == null || sample?.Frames == null || sample.Frames.Count == 0)
            {
                result.FailureReason = "sample has no frames";
                return result;
            }

            result.FrameCount = sample.Frames.Count;
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.2f, 2.4f, 0.01f);
            recognizer.Reset();
            var firstTime = sample.Frames[0].Time;
            for (var frameIndex = 0; frameIndex < sample.Frames.Count; frameIndex++)
            {
                var frameSample = sample.Frames[frameIndex];
                var runtimeFrame = ToGestureFrame(frameSample, sample.Handedness, frameIndex + 1, frameSample.Time - firstTime);
                if (recognizer.TryResolveSingle(runtimeFrame, template, runtimeFrame.Timestamp))
                {
                    result.Matched = true;
                    result.BestScore = recognizer.LastScore;
                    result.TriggeredAt = runtimeFrame.Timestamp;
                    return result;
                }

                result.FailureReason = recognizer.LastFailureReason;
                if (recognizer.LastScore < result.BestScore)
                {
                    result.BestScore = recognizer.LastScore;
                }
            }

            if (string.IsNullOrWhiteSpace(result.FailureReason))
            {
                result.FailureReason = "sample did not match";
            }

            return result;
        }

        public static bool IsAllowedTargetIntent(GestureIntent intent)
        {
            return intent != GestureIntent.None;
        }

        public string GetTemplatePath(string gestureId)
        {
            return Path.Combine(folderPath, SanitizeId(gestureId) + ".json");
        }

        private bool TryLoad(string path, out CustomGestureTemplate template)
        {
            template = null;
            try
            {
                var json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<TemplateFile>(json);
                if (wrapper != null && wrapper.Template != null && TrySanitizeTemplate(wrapper.Template, out template))
                {
                    return true;
                }

                var directTemplate = JsonUtility.FromJson<CustomGestureTemplate>(json);
                if (directTemplate == null || !TrySanitizeTemplate(directTemplate, out template))
                {
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[CustomGesture] Failed to load template '{path}': {exception.Message}");
                return false;
            }
        }

        private static bool TrySanitizeTemplate(CustomGestureTemplate template, out CustomGestureTemplate sanitized)
        {
            sanitized = null;
            if (template == null)
            {
                return false;
            }

            var gestureId = SanitizeId(template.GestureId);
            if (string.IsNullOrWhiteSpace(gestureId))
            {
                return false;
            }

            template.GestureId = gestureId;
            if (string.IsNullOrWhiteSpace(template.DisplayName))
            {
                template.DisplayName = gestureId;
            }

            var defaultThreshold = template.Kind == CustomGestureKind.StaticPose ? CustomGestureRecognizer.DefaultStaticThreshold : CustomGestureRecognizer.DefaultDynamicThreshold;
            if (!IsAllowedTargetIntent(template.TargetIntent))
            {
                template.TargetIntent = GestureIntent.CustomGesture;
            }
            template.MatchThreshold = Mathf.Clamp(template.MatchThreshold <= 0f ? defaultThreshold : template.MatchThreshold, 0.01f, 2f);
            template.Samples ??= new List<CustomGestureSample>();
            template.TrajectoryTemplates ??= new List<CustomGestureTrajectoryTemplate>();
            template.FeatureSequenceTemplates ??= new List<CustomGestureFeatureSequenceTemplate>();
            if (template.Kind == CustomGestureKind.DynamicMotion)
            {
                if (template.TrajectoryTemplates.Count == 0)
                {
                    template.TrajectoryTemplates = CustomGestureTrajectoryTemplateBuilder.Build(template.Samples);
                }

                template.DynamicRule ??= CustomGestureDynamicRuleEvaluator.InferRule(template.Samples);
                if (template.DynamicRule != null)
                {
                    template.DynamicRule.Pattern = CustomGestureDynamicPatternUtility.Normalize(template.DynamicRule.Pattern);
                }

                if (template.FeatureSequenceTemplates.Count == 0
                    && template.DynamicRule != null
                    && CustomGestureDynamicPatternUtility.IsFeatureSequence(template.DynamicRule.Pattern))
                {
                    template.FeatureSequenceTemplates = CustomGestureTrajectoryTemplateBuilder.BuildFeatureSequences(template.Samples);
                }

                template.SchemaVersion = Mathf.Max(template.SchemaVersion, 2);
                ApplyDynamicTemplateProfile(template);
            }
            else
            {
                template.DynamicRule = null;
                template.TrajectoryTemplates.Clear();
                template.FeatureSequenceTemplates.Clear();
            }
            template.RequiredHandedness = ResolveTemplateHandedness(template);
            sanitized = template;
            return true;
        }

        private static void ApplyDynamicTemplateProfile(CustomGestureTemplate template)
        {
            if (template?.DynamicRule == null)
            {
                return;
            }

            switch (template.DynamicRule.Pattern)
            {
                case CustomGestureDynamicPattern.FingerDistanceChange:
                case CustomGestureDynamicPattern.FingerOscillation:
                case CustomGestureDynamicPattern.FeatureSequence:
                    template.MatchThreshold = Mathf.Clamp(template.MatchThreshold, 0.01f, 0.38f);
                    break;

                default:
                    if (!CustomGestureDynamicPatternUtility.IsFeatureSequence(template.DynamicRule.Pattern))
                    {
                        template.FeatureSequenceTemplates?.Clear();
                    }
                    template.MatchThreshold = Mathf.Clamp(template.MatchThreshold, 0.01f, 0.22f);
                    template.DynamicRule.MinimumDistance = Mathf.Max(template.DynamicRule.MinimumDistance, 0.06f);
                    template.DynamicRule.MaximumDrift = Mathf.Min(Mathf.Max(template.DynamicRule.MaximumDrift, 0.14f), 0.28f);
                    break;
            }
        }

        private static string GetDefaultProjectLibraryPath()
        {
            return Path.Combine(Application.dataPath, ProjectLibraryFolder, FolderName);
        }

        private static GestureHandedness ResolveTemplateHandedness(CustomGestureTemplate template)
        {
            if (template.RequiredHandedness != GestureHandedness.Unknown)
            {
                return template.RequiredHandedness;
            }

            if (template.Kind == CustomGestureKind.DynamicMotion)
            {
                return GestureHandedness.Unknown;
            }

            if (template.Samples == null)
            {
                return GestureHandedness.Unknown;
            }

            for (var index = 0; index < template.Samples.Count; index++)
            {
                var sample = template.Samples[index];
                if (sample != null && sample.Handedness != GestureHandedness.Unknown)
                {
                    return sample.Handedness;
                }
            }

            return GestureHandedness.Unknown;
        }

        private void ReplaceInMemory(CustomGestureTemplate template)
        {
            for (var index = 0; index < templates.Count; index++)
            {
                if (string.Equals(templates[index].GestureId, template.GestureId, StringComparison.OrdinalIgnoreCase))
                {
                    templates[index] = template;
                    return;
                }
            }

            templates.Add(template);
        }

        private void ReplaceValidationReport(CustomGestureTemplateValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            for (var index = 0; index < validationReports.Count; index++)
            {
                if (string.Equals(validationReports[index].GestureId, report.GestureId, StringComparison.OrdinalIgnoreCase))
                {
                    validationReports[index] = report;
                    return;
                }
            }

            validationReports.Add(report);
        }

        private static GestureFrame ToGestureFrame(CustomGestureFrameSample sample, GestureHandedness handedness, long frameId, float timestamp)
        {
            var hand = new TrackedHandState
            {
                IsTracked = true,
                Confidence = sample?.Confidence ?? 0f,
                StaticGesture = sample?.StaticGesture ?? GestureType.None,
                Handedness = handedness,
                PalmCenter = sample?.PalmCenter ?? Vector2.zero,
                Landmarks = sample?.Landmarks ?? Array.Empty<Vector2>()
            };

            return new GestureFrame
            {
                FrameId = frameId,
                Timestamp = timestamp,
                Source = GestureSourceKind.Mock,
                Hands = new[] { hand }
            };
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (var index = 0; index < chars.Length; index++)
            {
                if (Array.IndexOf(invalid, chars[index]) >= 0 || char.IsWhiteSpace(chars[index]))
                {
                    chars[index] = '_';
                }
            }

            return new string(chars);
        }
    }
}

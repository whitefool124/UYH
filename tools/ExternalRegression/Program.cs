using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpellGuard.InputSystem;
using SpellGuard.Tools;
using UnityEngine;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new Vector2JsonConverter());
        return options;
    }

    private static int Main(string[] args)
    {
        var root = ParseArg(args, "--root") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var selfCheckLibrary = ParseArg(args, "--self-check-library");
        if (!string.IsNullOrWhiteSpace(selfCheckLibrary))
        {
            var selfCheckReportDir = ParseArg(args, "--report") ?? Path.Combine(root, "build-temp", "template-self-check");
            return RunTemplateSelfCheck(selfCheckLibrary, selfCheckReportDir);
        }

        var minedJson = ParseArg(args, "--dataset") ?? Path.Combine(root, "build-temp", "jester_mined_motion_subset.json");
        var reportDir = ParseArg(args, "--report") ?? Path.Combine(root, "build-temp", "external-regression-report");
        var explicitLibraryDir = ParseArg(args, "--library");
        Directory.CreateDirectory(reportDir);

        if (!File.Exists(minedJson))
        {
            Console.Error.WriteLine($"Missing dataset: {minedJson}");
            return 2;
        }

        var dataset = LoadDataset(minedJson);
        var saveFolder = explicitLibraryDir ?? Path.Combine(reportDir, "library");
        Directory.CreateDirectory(saveFolder);

        var trainByLabel = new Dictionary<string, List<CustomGestureSample>>(StringComparer.OrdinalIgnoreCase);
        var heldOut = new List<CustomGestureBatchClip>();
        foreach (var clip in dataset.Clips)
        {
            if (clip == null)
            {
                continue;
            }

            if (clip.IsTrain)
            {
                if (RecordClipSample(clip, dataset.DefaultFps) is { } sample)
                {
                    if (!trainByLabel.TryGetValue(clip.Label, out var list))
                    {
                        list = new List<CustomGestureSample>();
                        trainByLabel[clip.Label] = list;
                    }

                    list.Add(sample);
                }
            }
            else
            {
                heldOut.Add(clip);
            }
        }

        var savedLabels = new List<string>();
        foreach (var pair in trainByLabel.OrderBy(pair => pair.Key))
        {
            if (pair.Value.Count == 0)
            {
                continue;
            }

            var template = new CustomGestureTemplate
            {
                GestureId = $"ext_{Sanitize(pair.Key)}",
                DisplayName = pair.Key,
                Kind = CustomGestureKind.DynamicMotion,
                TargetIntent = GestureIntent.CustomGesture,
                MatchThreshold = 0.78f,
                Samples = pair.Value,
                TrajectoryTemplates = CustomGestureTrajectoryTemplateBuilder.Build(pair.Value),
                FeatureSequenceTemplates = CustomGestureTrajectoryTemplateBuilder.BuildFeatureSequences(pair.Value),
                DynamicRule = CustomGestureDynamicRuleEvaluator.InferRule(pair.Value),
                RequiredHandedness = GestureHandedness.Unknown
            };
            RelaxForLiveValidation(template);

            SaveTemplate(saveFolder, template);
            savedLabels.Add(pair.Key);
        }

        var templates = LoadTemplatesForSelfCheck(saveFolder);
        var saveResults = new List<string>();
        foreach (var label in savedLabels)
        {
            saveResults.Add($"saved,{label}");
        }

        var validationLines = new List<string>();
        foreach (var clip in heldOut)
        {
            var target = templates.FirstOrDefault(template => string.Equals(template.DisplayName, clip.Label, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                validationLines.Add($"{clip.ClipId},{clip.Label},False,,False,Infinity,no_template");
                continue;
            }

            var frames = clip.ToRuntimeFrames(dataset.DefaultFps);
            var recognizer = new CustomGestureRecognizer();
            recognizer.Configure(0.5f, 1.6f, 0.1f);
            recognizer.Reset();
            var matched = false;
            var matchedName = target.DisplayName;
            var bestScore = float.PositiveInfinity;
            for (var index = 0; index < frames.Count; index++)
            {
                if (recognizer.TryResolveSingle(frames[index], target, frames[index].Timestamp))
                {
                    matched = true;
                    matchedName = recognizer.LastMatchedName;
                    bestScore = recognizer.LastScore;
                    break;
                }

                if (recognizer.LastScore < bestScore)
                {
                    bestScore = recognizer.LastScore;
                    matchedName = recognizer.LastMatchedName;
                }
            }

            var correct = matched && string.Equals(clip.Label, matchedName, StringComparison.OrdinalIgnoreCase);
            validationLines.Add($"{clip.ClipId},{clip.Label},{matched},{matchedName},{correct},{FormatScore(bestScore)},selected_target");
        }

        File.WriteAllLines(Path.Combine(reportDir, "saved_templates.csv"), saveResults);
        File.WriteAllLines(Path.Combine(reportDir, "validation_results.csv"), validationLines.Prepend("clip_id,label,matched,matched_label,correct,best_score,mode"));
        File.WriteAllText(Path.Combine(reportDir, "import_manifest.txt"),
            $"dataset={Path.GetFullPath(minedJson)}{Environment.NewLine}" +
            $"library={Path.GetFullPath(saveFolder)}{Environment.NewLine}" +
            $"saved_templates={savedLabels.Count}{Environment.NewLine}" +
            $"validated_clips={heldOut.Count}{Environment.NewLine}");
        Console.WriteLine($"Saved templates: {savedLabels.Count}");
        Console.WriteLine($"Validated clips: {heldOut.Count}");
        Console.WriteLine($"Correct clips: {validationLines.Count(line => line.Contains(",True,"))}");
        Console.WriteLine($"Unity-compatible library folder: {saveFolder}");
        return 0;
    }

    private static int RunTemplateSelfCheck(string libraryFolder, string reportDir)
    {
        Directory.CreateDirectory(reportDir);
        var templates = LoadTemplatesForSelfCheck(libraryFolder);
        var rows = new List<string>
        {
            "gesture_id,display_name,sample_id,frames,threshold,min_score,matched,triggered_at,pattern,direction,trajectory_templates,feature_sequence_templates"
        };

        var checkedSamples = 0;
        var matchedSamples = 0;
        foreach (var template in templates)
        {
            if (template == null || template.Samples == null || template.Samples.Count == 0)
            {
                continue;
            }

            foreach (var sample in template.Samples)
            {
                if (sample == null || sample.Frames == null || sample.Frames.Count == 0)
                {
                    continue;
                }

                checkedSamples++;
                var recognizer = new CustomGestureRecognizer();
                recognizer.Configure(0.2f, 2.4f, 0.01f);
                recognizer.Reset();
                var matched = false;
                var bestScore = float.PositiveInfinity;
                var triggeredAt = -1f;
                var firstTime = sample.Frames[0].Time;
                for (var index = 0; index < sample.Frames.Count; index++)
                {
                    var runtimeFrame = ToGestureFrame(sample.Frames[index], sample.Handedness, sample.Frames[index].Time - firstTime);
                    if (recognizer.TryResolveSingle(runtimeFrame, template, runtimeFrame.Timestamp))
                    {
                        matched = true;
                        bestScore = recognizer.LastScore;
                        triggeredAt = runtimeFrame.Timestamp;
                        break;
                    }

                    if (recognizer.LastScore < bestScore)
                    {
                        bestScore = recognizer.LastScore;
                    }
                }

                if (matched)
                {
                    matchedSamples++;
                }

                rows.Add(string.Join(",",
                    Csv(template.GestureId),
                    Csv(template.DisplayName),
                    Csv(sample.SampleId),
                    sample.Frames.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    FormatScore(template.MatchThreshold),
                    FormatScore(bestScore),
                    matched.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    FormatScore(triggeredAt),
                    template.DynamicRule != null ? template.DynamicRule.Pattern.ToString() : "",
                    template.DynamicRule != null ? template.DynamicRule.Direction.ToString() : "",
                    (template.TrajectoryTemplates?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    (template.FeatureSequenceTemplates?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        var reportPath = Path.Combine(reportDir, "template_self_check.csv");
        File.WriteAllLines(reportPath, rows);
        Console.WriteLine($"Templates: {templates.Count}");
        Console.WriteLine($"Checked samples: {checkedSamples}");
        Console.WriteLine($"Matched samples: {matchedSamples}");
        Console.WriteLine($"Report: {reportPath}");
        return checkedSamples == matchedSamples ? 0 : 1;
    }

    private static List<CustomGestureTemplate> LoadTemplatesForSelfCheck(string libraryFolder)
    {
        var templates = new List<CustomGestureTemplate>();
        if (!Directory.Exists(libraryFolder))
        {
            return templates;
        }

        foreach (var path in Directory.GetFiles(libraryFolder, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var json = File.ReadAllText(path);
                var wrapped = JsonSerializer.Deserialize<TemplateFile>(json, JsonOptions);
                var template = wrapped?.Template ?? JsonSerializer.Deserialize<CustomGestureTemplate>(json, JsonOptions);
                if (template == null || string.IsNullOrWhiteSpace(template.GestureId))
                {
                    continue;
                }

                template.Samples ??= new List<CustomGestureSample>();
                template.TrajectoryTemplates ??= CustomGestureTrajectoryTemplateBuilder.Build(template.Samples);
                template.FeatureSequenceTemplates ??= CustomGestureTrajectoryTemplateBuilder.BuildFeatureSequences(template.Samples);
                if (template.TrajectoryTemplates.Count == 0 && template.Kind == CustomGestureKind.DynamicMotion)
                {
                    template.TrajectoryTemplates = CustomGestureTrajectoryTemplateBuilder.Build(template.Samples);
                }
                if (template.FeatureSequenceTemplates.Count == 0 && template.Kind == CustomGestureKind.DynamicMotion)
                {
                    template.FeatureSequenceTemplates = CustomGestureTrajectoryTemplateBuilder.BuildFeatureSequences(template.Samples);
                }

                template.DynamicRule ??= template.Kind == CustomGestureKind.DynamicMotion
                    ? CustomGestureDynamicRuleEvaluator.InferRule(template.Samples)
                    : null;
                if (template.MatchThreshold <= 0f)
                {
                    template.MatchThreshold = template.Kind == CustomGestureKind.StaticPose
                        ? CustomGestureRecognizer.DefaultStaticThreshold
                        : CustomGestureRecognizer.DefaultDynamicThreshold;
                }

                templates.Add(template);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Failed to load template {path}: {exception.Message}");
            }
        }

        return templates;
    }

    private sealed class TemplateFile
    {
        public CustomGestureTemplate? Template { get; set; }
    }

    private static void SaveTemplate(string libraryFolder, CustomGestureTemplate template)
    {
        Directory.CreateDirectory(libraryFolder);
        var path = Path.Combine(libraryFolder, $"{template.GestureId}.json");
        var json = JsonSerializer.Serialize(new TemplateFile { Template = template }, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void RelaxForLiveValidation(CustomGestureTemplate template)
    {
        if (template?.DynamicRule == null || template.Kind != CustomGestureKind.DynamicMotion)
        {
            return;
        }

        template.RequiredHandedness = GestureHandedness.Unknown;
        template.MatchThreshold = Math.Max(template.MatchThreshold, 0.78f);
        template.DynamicRule.Direction = CustomGestureMotionDirection.Any;
        template.DynamicRule.MinimumAxisRatio = 0f;
        template.DynamicRule.MinimumDistance = Math.Min(template.DynamicRule.MinimumDistance, 0.05f);
        template.DynamicRule.MaximumDrift = Math.Max(template.DynamicRule.MaximumDrift, 0.60f);
        template.DynamicRule.MinimumDuration = Math.Min(template.DynamicRule.MinimumDuration, 0.02f);
        template.DynamicRule.MaximumDuration = Math.Max(template.DynamicRule.MaximumDuration, 3.0f);
        template.DynamicRule.MinimumFeatureDelta = Math.Min(template.DynamicRule.MinimumFeatureDelta, 0.08f);
        template.DynamicRule.MinimumFeaturePath = Math.Min(template.DynamicRule.MinimumFeaturePath, 0.12f);
    }

    private static GestureFrame ToGestureFrame(CustomGestureFrameSample sample, GestureHandedness handedness, float timestamp)
    {
        var hand = new TrackedHandState
        {
            IsTracked = true,
            Confidence = sample.Confidence,
            StaticGesture = sample.StaticGesture,
            Handedness = handedness,
            PalmCenter = sample.PalmCenter,
            Landmarks = sample.Landmarks ?? Array.Empty<Vector2>()
        };

        return new GestureFrame
        {
            Timestamp = timestamp,
            Source = GestureSourceKind.Mock,
            Hands = new[] { hand }
        };
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static CustomGestureBatchDataset LoadDataset(string path)
    {
        return JsonSerializer.Deserialize<CustomGestureBatchDataset>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Could not parse dataset: {path}");
    }

    private static CustomGestureSample? RecordClipSample(CustomGestureBatchClip clip, float defaultFps)
    {
        var frames = clip.ToRuntimeFrames(defaultFps);
        if (frames.Count == 0)
        {
            return null;
        }

        var duration = Math.Max(0.24f, frames[frames.Count - 1].Timestamp - frames[0].Timestamp);
        var recorder = new CustomGestureRecorder();
        recorder.Configure(0f, duration, 0.06f, 0.5f, CustomGestureKind.DynamicMotion);
        recorder.SetTargetHandedness(frames[0].PrimaryHand.Handedness);
        var start = 10f;
        recorder.Begin(start);

        GestureFrame lastFrame = frames[frames.Count - 1];
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            frame.Timestamp = start + frame.Timestamp - frames[0].Timestamp;
            recorder.Update(frame, frame.Timestamp);
            lastFrame = frame;
        }

        if (recorder.LastSample == null)
        {
            lastFrame.Timestamp = start + duration + 0.02f;
            recorder.Update(lastFrame, lastFrame.Timestamp);
        }

        if (recorder.LastSample != null)
        {
            recorder.LastSample.SampleId = clip.ClipId;
        }

        return recorder.LastSample;
    }

    private static string? ParseArg(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static GestureHandedness ResolveHandedness(IReadOnlyList<CustomGestureSample> samples)
    {
        foreach (var sample in samples)
        {
            if (sample != null && sample.Handedness != GestureHandedness.Unknown)
            {
                return sample.Handedness;
            }
        }

        return GestureHandedness.Unknown;
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "gesture";
        }

        var chars = new char[value.Length];
        var count = 0;
        foreach (var raw in value)
        {
            var c = char.ToLowerInvariant(raw);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                chars[count++] = c;
            }
            else if (c == ' ' || c == '-' || c == '_')
            {
                chars[count++] = '_';
            }
        }

        return count > 0 ? new string(chars, 0, count) : "gesture";
    }

    private static string FormatScore(float score)
    {
        return float.IsInfinity(score) ? "Infinity" : score.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class Vector2JsonConverter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var x = 0f;
            var y = 0f;
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return Vector2.zero;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new Vector2(x, y);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var name = reader.GetString();
                reader.Read();
                if (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "X", StringComparison.OrdinalIgnoreCase))
                {
                    x = reader.GetSingle();
                }
                else if (string.Equals(name, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    y = reader.GetSingle();
                }
                else
                {
                    reader.Skip();
                }
            }

            return new Vector2(x, y);
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.x);
            writer.WriteNumber("y", value.y);
            writer.WriteEndObject();
        }
    }
}

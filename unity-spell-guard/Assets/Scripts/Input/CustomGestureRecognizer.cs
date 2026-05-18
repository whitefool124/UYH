using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public sealed class CustomGestureRecognizer
    {
        public const float DefaultDynamicThreshold = 0.18f;
        public const float DefaultStaticThreshold = 0.16f;
        private const float AmbiguousMatchMargin = 0.035f;
        private const int ResampledFrameCount = 12;

        private readonly List<CustomGestureFrameSample> window = new List<CustomGestureFrameSample>();
        private readonly List<float[]> runtimeFeatures = new List<float[]>();
        private float minimumConfidence = 0.55f;
        private float maxWindowSeconds = 1.6f;
        private float cooldownSeconds = 0.85f;
        private float lastTriggeredAt = -999f;
        private GestureAction currentAction = GestureAction.None;
        private string lastMatchedName = "无";
        private float lastScore = float.PositiveInfinity;

        public GestureAction CurrentCustomAction => currentAction;
        public string LastMatchedName => lastMatchedName;
        public float LastScore => lastScore;

        public void Configure(float minConfidence, float windowSeconds, float cooldown)
        {
            minimumConfidence = Mathf.Clamp01(minConfidence);
            maxWindowSeconds = Mathf.Clamp(windowSeconds, 0.4f, 3f);
            cooldownSeconds = Mathf.Clamp(cooldown, 0.1f, 3f);
        }

        public void Reset()
        {
            window.Clear();
            runtimeFeatures.Clear();
            currentAction = GestureAction.None;
            lastMatchedName = "无";
            lastScore = float.PositiveInfinity;
            lastTriggeredAt = -999f;
        }

        public bool TryResolve(GestureFrame frame, IReadOnlyList<CustomGestureTemplate> templates, float now, out GestureAction action)
        {
            action = GestureAction.None;
            currentAction = GestureAction.None;
            AddFrame(frame, now);
            if (templates == null || templates.Count == 0 || now - lastTriggeredAt < cooldownSeconds)
            {
                return false;
            }

            var hasDynamicFeatures = TryBuildResampledFeatures(window, minimumConfidence, runtimeFeatures);
            var hasStaticFeatures = TryGetLatestStaticFeatures(window, minimumConfidence, out var runtimeStaticFeatures);
            if (!hasDynamicFeatures && !hasStaticFeatures)
            {
                return false;
            }

            var runtimeHandedness = frame.PrimaryHand.Handedness;
            CustomGestureTemplate bestTemplate = null;
            var bestScore = float.PositiveInfinity;
            var secondBestScore = float.PositiveInfinity;
            for (var templateIndex = 0; templateIndex < templates.Count; templateIndex++)
            {
                var template = templates[templateIndex];
                if (!IsUsableTemplate(template))
                {
                    continue;
                }

                if (template.Kind == CustomGestureKind.DynamicMotion && !hasDynamicFeatures)
                {
                    continue;
                }

                if (template.Kind == CustomGestureKind.StaticPose && !hasStaticFeatures)
                {
                    continue;
                }

                if (template.RequiredHandedness != GestureHandedness.Unknown
                    && runtimeHandedness != GestureHandedness.Unknown
                    && template.RequiredHandedness != runtimeHandedness)
                {
                    continue;
                }

                for (var sampleIndex = 0; sampleIndex < template.Samples.Count; sampleIndex++)
                {
                    var sample = template.Samples[sampleIndex];
                    var score = template.Kind == CustomGestureKind.StaticPose
                        ? ScoreStaticSample(sample, runtimeStaticFeatures, runtimeHandedness)
                        : ScoreDynamicSample(sample, runtimeFeatures, runtimeHandedness);
                    if (score < bestScore)
                    {
                        secondBestScore = bestScore;
                        bestScore = score;
                        bestTemplate = template;
                    }
                    else if (score < secondBestScore)
                    {
                        secondBestScore = score;
                    }
                }
            }

            lastScore = bestScore;
            if (bestTemplate == null || bestScore > bestTemplate.MatchThreshold)
            {
                return false;
            }

            if (secondBestScore <= bestTemplate.MatchThreshold && secondBestScore - bestScore < AmbiguousMatchMargin)
            {
                lastMatchedName = "相近手势冲突";
                return false;
            }

            var primaryHand = frame.PrimaryHand;
            action = new GestureAction
            {
                Intent = GestureIntent.CustomGesture,
                Confidence = Mathf.Clamp01(1f - bestScore / Mathf.Max(0.001f, bestTemplate.MatchThreshold)),
                TriggeredTime = now,
                SourceKind = bestTemplate.Kind == CustomGestureKind.StaticPose ? GestureCommandKind.StaticPose : GestureCommandKind.Motion,
                Handedness = primaryHand.Handedness,
                TrackId = primaryHand.TrackId
            };

            currentAction = action;
            lastTriggeredAt = now;
            lastMatchedName = string.IsNullOrWhiteSpace(bestTemplate.DisplayName) ? bestTemplate.GestureId : bestTemplate.DisplayName;
            return true;
        }

        public bool TryResolve(GestureFrame frame, IReadOnlyList<CustomGestureTemplate> templates, out GestureAction action)
        {
            return TryResolve(frame, templates, Time.time, out action);
        }

        public bool TryResolveSingle(GestureFrame frame, CustomGestureTemplate template, float now)
        {
            currentAction = GestureAction.None;
            AddFrame(frame, now);
            lastScore = float.PositiveInfinity;
            if (!IsUsableTemplate(template) || now - lastTriggeredAt < cooldownSeconds)
            {
                return false;
            }

            var hasDynamicFeatures = TryBuildResampledFeatures(window, minimumConfidence, runtimeFeatures);
            var hasStaticFeatures = TryGetLatestStaticFeatures(window, minimumConfidence, out var runtimeStaticFeatures);
            if (template.Kind == CustomGestureKind.DynamicMotion && !hasDynamicFeatures)
            {
                return false;
            }

            if (template.Kind == CustomGestureKind.StaticPose && !hasStaticFeatures)
            {
                return false;
            }

            var runtimeHandedness = frame.PrimaryHand.Handedness;
            if (template.RequiredHandedness != GestureHandedness.Unknown
                && runtimeHandedness != GestureHandedness.Unknown
                && template.RequiredHandedness != runtimeHandedness)
            {
                return false;
            }

            var bestScore = float.PositiveInfinity;
            for (var sampleIndex = 0; sampleIndex < template.Samples.Count; sampleIndex++)
            {
                var sample = template.Samples[sampleIndex];
                var score = template.Kind == CustomGestureKind.StaticPose
                    ? ScoreStaticSample(sample, runtimeStaticFeatures, runtimeHandedness)
                    : ScoreDynamicSample(sample, runtimeFeatures, runtimeHandedness);
                if (score < bestScore)
                {
                    bestScore = score;
                }
            }

            lastScore = bestScore;
            if (bestScore > template.MatchThreshold)
            {
                return false;
            }

            lastTriggeredAt = now;
            lastMatchedName = string.IsNullOrWhiteSpace(template.DisplayName) ? template.GestureId : template.DisplayName;
            return true;
        }

        private void AddFrame(GestureFrame frame, float now)
        {
            if (frame.HasPrimaryHand && frame.PrimaryHand.Landmarks != null && frame.PrimaryHand.Landmarks.Length >= CustomGestureFeatureExtractor.RequiredLandmarkCount)
            {
                var hand = frame.PrimaryHand;
                var copied = new Vector2[CustomGestureFeatureExtractor.RequiredLandmarkCount];
                System.Array.Copy(hand.Landmarks, copied, copied.Length);
                window.Add(new CustomGestureFrameSample
                {
                    Time = now,
                    Confidence = Mathf.Clamp01(hand.Confidence),
                    Landmarks = copied
                });
            }

            var cutoff = now - maxWindowSeconds;
            window.RemoveAll(sample => sample.Time < cutoff);
        }

        private float ScoreDynamicSample(CustomGestureSample sample, List<float[]> currentFeatures, GestureHandedness runtimeHandedness)
        {
            if (sample == null || sample.Frames == null || sample.Frames.Count < 3)
            {
                return float.PositiveInfinity;
            }

            if (sample.Handedness != GestureHandedness.Unknown
                && runtimeHandedness != GestureHandedness.Unknown
                && sample.Handedness != runtimeHandedness)
            {
                return float.PositiveInfinity;
            }

            var templateFeatures = new List<float[]>();
            if (!TryBuildResampledFeatures(sample.Frames, minimumConfidence, templateFeatures))
            {
                return float.PositiveInfinity;
            }

            var total = 0f;
            for (var index = 0; index < ResampledFrameCount; index++)
            {
                total += CustomGestureFeatureExtractor.Distance(currentFeatures[index], templateFeatures[index]);
            }

            return total / ResampledFrameCount;
        }

        private float ScoreStaticSample(CustomGestureSample sample, float[] currentFeatures, GestureHandedness runtimeHandedness)
        {
            if (sample == null || sample.Frames == null || sample.Frames.Count == 0 || currentFeatures == null)
            {
                return float.PositiveInfinity;
            }

            if (sample.Handedness != GestureHandedness.Unknown
                && runtimeHandedness != GestureHandedness.Unknown
                && sample.Handedness != runtimeHandedness)
            {
                return float.PositiveInfinity;
            }

            var total = 0f;
            var count = 0;
            for (var index = 0; index < sample.Frames.Count; index++)
            {
                if (!CustomGestureFeatureExtractor.TryExtract(sample.Frames[index], minimumConfidence, out var features))
                {
                    continue;
                }

                total += CustomGestureFeatureExtractor.Distance(currentFeatures, features);
                count++;
            }

            return count > 0 ? total / count : float.PositiveInfinity;
        }

        private static bool IsUsableTemplate(CustomGestureTemplate template)
        {
            return template != null &&
                   CustomGestureLibrary.IsAllowedTargetIntent(template.TargetIntent) &&
                   template.Samples != null &&
                   template.Samples.Count > 0;
        }

        private static bool TryBuildResampledFeatures(IReadOnlyList<CustomGestureFrameSample> frames, float minimumConfidence, List<float[]> output)
        {
            output.Clear();
            if (frames == null || frames.Count < 3)
            {
                return false;
            }

            for (var targetIndex = 0; targetIndex < ResampledFrameCount; targetIndex++)
            {
                var sourceIndex = Mathf.RoundToInt(targetIndex * (frames.Count - 1) / (float)(ResampledFrameCount - 1));
                sourceIndex = Mathf.Clamp(sourceIndex, 0, frames.Count - 1);
                if (!CustomGestureFeatureExtractor.TryExtract(frames[sourceIndex], minimumConfidence, out var features))
                {
                    output.Clear();
                    return false;
                }

                output.Add(features);
            }

            return output.Count == ResampledFrameCount;
        }

        private static bool TryGetLatestStaticFeatures(IReadOnlyList<CustomGestureFrameSample> frames, float minimumConfidence, out float[] features)
        {
            features = null;
            if (frames == null)
            {
                return false;
            }

            for (var index = frames.Count - 1; index >= 0; index--)
            {
                if (CustomGestureFeatureExtractor.TryExtract(frames[index], minimumConfidence, out features))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

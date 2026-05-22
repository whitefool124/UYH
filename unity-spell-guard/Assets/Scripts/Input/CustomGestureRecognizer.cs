using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public sealed class CustomGestureRecognizer
    {
        public const float DefaultDynamicThreshold = 0.18f;
        public const float DefaultStaticThreshold = 0.16f;
        private const float AmbiguousMatchMargin = 0.035f;

        private readonly List<CustomGestureFrameSample> window = new List<CustomGestureFrameSample>();
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

            var hasDynamicFrames = HasEnoughDynamicFrames();
            var hasStaticFeatures = TryGetLatestStaticFeatures(window, minimumConfidence, out var runtimeStaticFeatures);
            if (!hasDynamicFrames && !hasStaticFeatures)
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

                if (template.RequiredHandedness != GestureHandedness.Unknown
                    && runtimeHandedness != GestureHandedness.Unknown
                    && template.RequiredHandedness != runtimeHandedness)
                {
                    continue;
                }

                var score = template.Kind == CustomGestureKind.StaticPose
                    ? hasStaticFeatures ? ScoreStaticTemplate(template, runtimeStaticFeatures, runtimeHandedness) : float.PositiveInfinity
                    : hasDynamicFrames ? ScoreDynamicTemplate(template, runtimeHandedness) : float.PositiveInfinity;

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
                Intent = CustomGestureLibrary.IsAllowedTargetIntent(bestTemplate.TargetIntent) ? bestTemplate.TargetIntent : GestureIntent.CustomGesture,
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

            var hasStaticFeatures = TryGetLatestStaticFeatures(window, minimumConfidence, out var runtimeStaticFeatures);
            if (template.Kind == CustomGestureKind.StaticPose && !hasStaticFeatures)
            {
                return false;
            }

            if (template.Kind == CustomGestureKind.DynamicMotion && !HasEnoughDynamicFrames())
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

            lastScore = template.Kind == CustomGestureKind.StaticPose
                ? ScoreStaticTemplate(template, runtimeStaticFeatures, runtimeHandedness)
                : ScoreDynamicTemplate(template, runtimeHandedness);

            if (lastScore > template.MatchThreshold)
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
                    StaticGesture = hand.StaticGesture,
                    PalmCenter = hand.PalmCenter,
                    Landmarks = copied
                });
            }

            var cutoff = now - maxWindowSeconds;
            window.RemoveAll(sample => sample.Time < cutoff);
        }

        private bool HasEnoughDynamicFrames()
        {
            return window.Count >= 4;
        }

        private float ScoreDynamicTemplate(CustomGestureTemplate template, GestureHandedness runtimeHandedness)
        {
            if (template == null)
            {
                return float.PositiveInfinity;
            }

            var best = float.PositiveInfinity;
            if (template.TrajectoryTemplates != null)
            {
                for (var index = 0; index < template.TrajectoryTemplates.Count; index++)
                {
                    var trajectoryTemplate = template.TrajectoryTemplates[index];
                    if (trajectoryTemplate == null || trajectoryTemplate.Points == null || trajectoryTemplate.Points.Length < 2)
                    {
                        continue;
                    }

                    var score = CustomGestureTrajectoryMatcher.ScoreBestWindow(window, trajectoryTemplate.Points, minimumConfidence, trajectoryTemplate.DurationSeconds);
                    if (score < best)
                    {
                        best = score;
                    }
                }
            }

            if (template.DynamicRule != null && template.Samples != null && template.Samples.Count > 0)
            {
                var activeRule = template.DynamicRule;
                for (var index = 0; index < template.Samples.Count; index++)
                {
                    var sample = template.Samples[index];
                    if (sample == null || sample.Handedness != GestureHandedness.Unknown
                        && runtimeHandedness != GestureHandedness.Unknown
                        && sample.Handedness != runtimeHandedness)
                    {
                        continue;
                    }

                    if (!CustomGestureDynamicRuleEvaluator.TryMatch(activeRule, window, minimumConfidence, out var confidence))
                    {
                        continue;
                    }

                    best = Mathf.Min(best, 1f - confidence);
                }
            }

            return best;
        }

        private float ScoreStaticTemplate(CustomGestureTemplate template, float[] currentFeatures, GestureHandedness runtimeHandedness)
        {
            if (template == null || template.Samples == null || template.Samples.Count == 0 || currentFeatures == null)
            {
                return float.PositiveInfinity;
            }

            var best = float.PositiveInfinity;
            for (var sampleIndex = 0; sampleIndex < template.Samples.Count; sampleIndex++)
            {
                var sample = template.Samples[sampleIndex];
                var score = ScoreStaticSample(sample, currentFeatures, runtimeHandedness);
                if (score < best)
                {
                    best = score;
                }
            }

            return best;
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

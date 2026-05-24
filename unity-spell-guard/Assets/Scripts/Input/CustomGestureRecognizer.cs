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
        private long lastAcceptedFrameId = long.MinValue;
        private float lastAcceptedFrameTimestamp = float.NaN;
        private GestureSourceKind lastAcceptedFrameSource = GestureSourceKind.Unknown;
        private GestureAction currentAction = GestureAction.None;
        private string lastMatchedName = "无";
        private float lastScore = float.PositiveInfinity;
        private string lastFailureReason = "无";

        public GestureAction CurrentCustomAction => currentAction;
        public string LastMatchedName => lastMatchedName;
        public float LastScore => lastScore;
        public string LastFailureReason => lastFailureReason;
        public int WindowFrameCount => window.Count;
        public float WindowDurationSeconds => window.Count >= 2 ? Mathf.Max(0f, window[window.Count - 1].Time - window[0].Time) : 0f;

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
            lastFailureReason = "无";
            lastTriggeredAt = -999f;
            lastAcceptedFrameId = long.MinValue;
            lastAcceptedFrameTimestamp = float.NaN;
            lastAcceptedFrameSource = GestureSourceKind.Unknown;
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
            if (bestTemplate.Kind == CustomGestureKind.DynamicMotion)
            {
                window.Clear();
            }

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
            if (!IsUsableTemplate(template))
            {
                lastFailureReason = "模板不可用";
                return false;
            }

            if (now - lastTriggeredAt < cooldownSeconds)
            {
                lastFailureReason = "命中冷却中";
                return false;
            }

            var hasStaticFeatures = TryGetLatestStaticFeatures(window, minimumConfidence, out var runtimeStaticFeatures);
            if (template.Kind == CustomGestureKind.StaticPose && !hasStaticFeatures)
            {
                lastFailureReason = "没有可用静态特征";
                return false;
            }

            if (template.Kind == CustomGestureKind.DynamicMotion && !HasEnoughDynamicFrames())
            {
                lastFailureReason = $"动态帧不足 {window.Count}/4，窗口 {WindowDurationSeconds:F2}s";
                return false;
            }

            var runtimeHandedness = frame.PrimaryHand.Handedness;
            if (template.RequiredHandedness != GestureHandedness.Unknown
                && runtimeHandedness != GestureHandedness.Unknown
                && template.RequiredHandedness != runtimeHandedness)
            {
                lastFailureReason = $"手别不一致：需要 {template.RequiredHandedness}，当前 {runtimeHandedness}";
                return false;
            }

            lastScore = template.Kind == CustomGestureKind.StaticPose
                ? ScoreStaticTemplate(template, runtimeStaticFeatures, runtimeHandedness)
                : ScoreDynamicTemplate(template, runtimeHandedness);

            if (lastScore > template.MatchThreshold)
            {
                lastFailureReason = $"分数过高 {lastScore:F3} > {template.MatchThreshold:F3}，窗口 {window.Count} 帧/{WindowDurationSeconds:F2}s";
                return false;
            }

            lastTriggeredAt = now;
            lastMatchedName = string.IsNullOrWhiteSpace(template.DisplayName) ? template.GestureId : template.DisplayName;
            if (template.Kind == CustomGestureKind.DynamicMotion)
            {
                window.Clear();
            }
            lastFailureReason = $"已命中，窗口 {window.Count} 帧/{WindowDurationSeconds:F2}s";
            return true;
        }

        private void AddFrame(GestureFrame frame, float now)
        {
            if (IsDuplicateFrame(frame))
            {
                return;
            }

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
                lastAcceptedFrameId = frame.FrameId;
                lastAcceptedFrameTimestamp = frame.Timestamp;
                lastAcceptedFrameSource = frame.Source;
            }

            var cutoff = now - maxWindowSeconds;
            window.RemoveAll(sample => sample.Time < cutoff);
        }

        private bool IsDuplicateFrame(GestureFrame frame)
        {
            return frame.FrameId == lastAcceptedFrameId
                   && frame.Source == lastAcceptedFrameSource
                   && Mathf.Abs(frame.Timestamp - lastAcceptedFrameTimestamp) <= 0.0001f;
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
            var hasTrajectoryTemplates = template.TrajectoryTemplates != null && template.TrajectoryTemplates.Count > 0;
            var hasFeatureSequenceTemplates = template.FeatureSequenceTemplates != null && template.FeatureSequenceTemplates.Count > 0;
            var hasTemplateSequence = hasTrajectoryTemplates || hasFeatureSequenceTemplates;
            var hasDynamicRule = template.DynamicRule != null && template.Samples != null && template.Samples.Count > 0;
            var isPalmTrajectoryTemplate = hasTrajectoryTemplates
                                           && (template.DynamicRule == null
                                               || template.DynamicRule.Pattern == CustomGestureDynamicPattern.Directional
                                               || template.DynamicRule.Pattern == CustomGestureDynamicPattern.Loop
                                               || template.DynamicRule.Pattern == CustomGestureDynamicPattern.Repeat);
            var allowFeatureSequenceScore = hasFeatureSequenceTemplates && !isPalmTrajectoryTemplate;
            var dynamicRuleMatched = !hasDynamicRule || hasTemplateSequence;
            if (isPalmTrajectoryTemplate && HasTooMuchFingerPoseNoise() && !TemplateAllowsFingerPoseMotion(template))
            {
                return float.PositiveInfinity;
            }

            if (hasTemplateSequence && !HasEnoughRuntimeMotion(template))
            {
                return float.PositiveInfinity;
            }

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

            if (allowFeatureSequenceScore)
            {
                for (var index = 0; index < template.FeatureSequenceTemplates.Count; index++)
                {
                    var sequenceTemplate = template.FeatureSequenceTemplates[index];
                    if (sequenceTemplate == null || sequenceTemplate.Frames == null || sequenceTemplate.Frames.Length < 2)
                    {
                        continue;
                    }

                    var score = CustomGestureFeatureSequenceMatcher.ScoreBestWindow(window, sequenceTemplate.Frames, minimumConfidence, sequenceTemplate.DurationSeconds);
                    if (score < best)
                    {
                        best = score;
                    }
                }
            }

            if (hasDynamicRule && !isPalmTrajectoryTemplate)
            {
                var activeRule = template.DynamicRule;
                for (var index = 0; index < template.Samples.Count; index++)
                {
                    var sample = template.Samples[index];
                    if (sample == null || template.RequiredHandedness != GestureHandedness.Unknown
                        && sample.Handedness != GestureHandedness.Unknown
                        && runtimeHandedness != GestureHandedness.Unknown
                        && sample.Handedness != runtimeHandedness)
                    {
                        continue;
                    }

                    if (!CustomGestureDynamicRuleEvaluator.TryMatch(activeRule, window, minimumConfidence, out var confidence))
                    {
                        continue;
                    }

                    dynamicRuleMatched = true;
                    best = Mathf.Min(best, 1f - confidence);
                }
            }

            return dynamicRuleMatched ? best : float.PositiveInfinity;
        }

        private bool HasTooMuchFingerPoseNoise()
        {
            var noisy = 0;
            var usable = 0;
            for (var index = 0; index < window.Count; index++)
            {
                var frame = window[index];
                if (frame.Confidence < minimumConfidence)
                {
                    continue;
                }

                usable++;
                if (frame.StaticGesture == GestureType.Fist || frame.StaticGesture == GestureType.Point)
                {
                    noisy++;
                }
            }

            return usable >= 4 && noisy >= Mathf.CeilToInt(usable * 0.60f);
        }

        private static bool TemplateAllowsFingerPoseMotion(CustomGestureTemplate template)
        {
            if (template?.Samples == null)
            {
                return false;
            }

            var fingerPoseFrames = 0;
            var totalFrames = 0;
            for (var sampleIndex = 0; sampleIndex < template.Samples.Count; sampleIndex++)
            {
                var frames = template.Samples[sampleIndex]?.Frames;
                if (frames == null)
                {
                    continue;
                }

                for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    var gesture = frames[frameIndex].StaticGesture;
                    if (gesture == GestureType.None || gesture == GestureType.Unknown)
                    {
                        continue;
                    }

                    totalFrames++;
                    if (gesture == GestureType.Fist || gesture == GestureType.Point || gesture == GestureType.VSign)
                    {
                        fingerPoseFrames++;
                    }
                }
            }

            return totalFrames >= 4 && fingerPoseFrames >= Mathf.CeilToInt(totalFrames * 0.35f);
        }

        private static GestureType ResolveDominantStaticGesture(CustomGestureTemplate template)
        {
            if (template?.Samples == null)
            {
                return GestureType.None;
            }

            var counts = new Dictionary<GestureType, int>();
            for (var sampleIndex = 0; sampleIndex < template.Samples.Count; sampleIndex++)
            {
                var frames = template.Samples[sampleIndex]?.Frames;
                if (frames == null)
                {
                    continue;
                }

                for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    var gesture = frames[frameIndex].StaticGesture;
                    if (gesture == GestureType.None)
                    {
                        continue;
                    }

                    counts.TryGetValue(gesture, out var count);
                    counts[gesture] = count + 1;
                }
            }

            var bestGesture = GestureType.None;
            var bestCount = 0;
            var total = 0;
            foreach (var pair in counts)
            {
                total += pair.Value;
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    bestGesture = pair.Key;
                }
            }

            return total > 0 && bestCount >= Mathf.CeilToInt(total * 0.6f)
                ? bestGesture
                : GestureType.None;
        }

        private bool HasEnoughRuntimeMotion(CustomGestureTemplate template)
        {
            if (window.Count < 4)
            {
                return false;
            }

            var rule = template?.DynamicRule;
            var minimumPalmMotion = Mathf.Clamp((rule?.MinimumDistance ?? 0.05f) * 0.55f, 0.018f, 0.035f);
            var minimumDirectionalPath = minimumPalmMotion * 1.35f;
            var firstPalm = Vector2.zero;
            var lastPalm = Vector2.zero;
            var previousPalm = Vector2.zero;
            var hasFirstPalm = false;
            var palmPath = 0f;

            for (var index = 0; index < window.Count; index++)
            {
                var frame = window[index];
                if (frame.Confidence < minimumConfidence)
                {
                    continue;
                }

                if (TryResolvePalm(frame, out var palm))
                {
                    if (!hasFirstPalm)
                    {
                        firstPalm = palm;
                        hasFirstPalm = true;
                    }
                    else
                    {
                        palmPath += Vector2.Distance(previousPalm, palm);
                    }

                    previousPalm = palm;
                    lastPalm = palm;
                }
            }

            var palmDelta = hasFirstPalm ? Vector2.Distance(firstPalm, lastPalm) : 0f;
            if ((rule == null || rule.Direction != CustomGestureMotionDirection.Any)
                && HasDirectionalTrajectoryTemplate(template)
                && !HasCompatibleRuntimeDirection(template, lastPalm - firstPalm, palmDelta, minimumPalmMotion))
            {
                return false;
            }

            if (palmDelta >= minimumPalmMotion || palmPath >= minimumDirectionalPath && palmDelta >= minimumPalmMotion * 0.5f)
            {
                return true;
            }

            return false;
        }

        private static bool HasDirectionalTrajectoryTemplate(CustomGestureTemplate template)
        {
            if (template?.TrajectoryTemplates == null)
            {
                return false;
            }

            for (var index = 0; index < template.TrajectoryTemplates.Count; index++)
            {
                var points = template.TrajectoryTemplates[index]?.Points;
                if (points != null && points.Length >= 2 && (points[points.Length - 1] - points[0]).sqrMagnitude > 0.0025f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCompatibleRuntimeDirection(CustomGestureTemplate template, Vector2 runtimeDelta, float runtimeDistance, float minimumDistance)
        {
            if (runtimeDistance < minimumDistance)
            {
                return false;
            }

            for (var index = 0; index < template.TrajectoryTemplates.Count; index++)
            {
                var points = template.TrajectoryTemplates[index]?.Points;
                if (points == null || points.Length < 2)
                {
                    continue;
                }

                var templateDelta = points[points.Length - 1] - points[0];
                var templateDistance = templateDelta.magnitude;
                if (templateDistance <= 0.05f)
                {
                    continue;
                }

                if (HasCompatibleDominantAxis(runtimeDelta, templateDelta))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCompatibleDominantAxis(Vector2 runtimeDelta, Vector2 templateDelta)
        {
            var templateHorizontal = Mathf.Abs(templateDelta.x) > Mathf.Abs(templateDelta.y) * 1.15f;
            if (templateHorizontal)
            {
                return Mathf.Sign(runtimeDelta.x) == Mathf.Sign(templateDelta.x)
                       && Mathf.Abs(runtimeDelta.x) >= Mathf.Abs(runtimeDelta.y) * 0.75f;
            }

            return Mathf.Sign(runtimeDelta.y) == Mathf.Sign(templateDelta.y)
                   && Mathf.Abs(runtimeDelta.y) >= Mathf.Abs(runtimeDelta.x) * 0.75f;
        }

        private static bool TryResolvePalm(CustomGestureFrameSample frame, out Vector2 palm)
        {
            if (frame.Landmarks == null || frame.Landmarks.Length <= 17)
            {
                palm = frame.PalmCenter;
                return palm != Vector2.zero;
            }

            palm = (frame.Landmarks[0] + frame.Landmarks[5] + frame.Landmarks[17]) / 3f;
            if (palm != Vector2.zero)
            {
                return true;
            }

            palm = frame.PalmCenter;
            if (palm == Vector2.zero)
            {
                return false;
            }

            return true;
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

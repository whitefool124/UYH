using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public static class CustomGestureDynamicRuleEvaluator
    {
        private const float PalmEpsilon = 0.0001f;
        private const float MotionEpsilon = 0.0001f;

        private struct MotionSample
        {
            public float Time;
            public Vector2 Palm;
            public GestureType StaticGesture;
            public Vector2[] Landmarks;
        }

        public static CustomGestureDynamicRule CreateDefaultRule()
        {
            return new CustomGestureDynamicRule
            {
                Pattern = CustomGestureDynamicPattern.Directional,
                Direction = CustomGestureMotionDirection.Any,
                RequireOpenPalm = true,
                MinimumOpenPalmRatio = 0.8f,
                MinimumDistance = 0.12f,
                MaximumDrift = 0.22f,
                MinimumDuration = 0.12f,
                MaximumDuration = 2f,
                MinimumAxisRatio = 0f,
                RepeatCount = 2,
                MinimumPathRatio = 1.6f,
                MaximumClosureDistance = 0.12f,
                FingerAIndex = 4,
                FingerBIndex = 8,
                FingerCIndex = 12,
                MinimumFingerDistanceDelta = 0.22f,
                MinimumFingerDistancePath = 0.18f,
                MinimumFingerVelocity = 0.35f,
                MinimumOscillationCount = 2,
                MaximumPalmMotion = 0.18f,
                MinimumFeatureDelta = 0.16f,
                MinimumFeaturePath = 0.22f,
                StartPose = GestureType.Unknown,
                EndPose = GestureType.Unknown,
                PoseTransitionMaxPalmMotion = 0.12f
            };
        }

        public static CustomGestureDynamicRule InferRule(IReadOnlyList<CustomGestureSample> samples)
        {
            var rule = CreateDefaultRule();
            if (samples == null || samples.Count == 0)
            {
                return rule;
            }

            var analyses = new List<MotionAnalysis>();
            var openPalmCount = 0;
            var totalFrames = 0;
            for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                var sample = samples[sampleIndex];
                if (sample?.Frames == null)
                {
                    continue;
                }

                var motions = new List<MotionSample>();
                for (var frameIndex = 0; frameIndex < sample.Frames.Count; frameIndex++)
                {
                    var frame = sample.Frames[frameIndex];
                    if (!TryResolvePalm(frame, out var palm))
                    {
                        continue;
                    }

                    motions.Add(new MotionSample
                    {
                        Time = frame.Time,
                        Palm = palm,
                        StaticGesture = frame.StaticGesture,
                        Landmarks = frame.Landmarks
                    });
                    totalFrames += 1;
                    if (frame.StaticGesture == GestureType.OpenPalm)
                    {
                        openPalmCount += 1;
                    }
                }

                if (motions.Count >= 2)
                {
                    analyses.Add(Analyze(motions));
                }
            }

            if (analyses.Count == 0)
            {
                return rule;
            }

            var averageDuration = 0f;
            var averagePathLength = 0f;
            var averageNetDistance = 0f;
            var averageClosureDistance = 0f;
            var averagePathRatio = 0f;
            var averagePerpendicularDrift = 0f;
            var averageHorizontalDelta = 0f;
            var averageVerticalDelta = 0f;
            var averageDirectionFlipCount = 0f;
            var averageRepeatScore = 0f;
            var averageClosedLoopScore = 0f;
            var averageFeatureDelta = 0f;
            var averageFeaturePath = 0f;
            var repeatLikeAnalysisCount = 0;
            var horizontalAnalysisCount = 0;
            for (var analysisIndex = 0; analysisIndex < analyses.Count; analysisIndex++)
            {
                var analysis = analyses[analysisIndex];
                averageDuration += analysis.Duration;
                averagePathLength += analysis.PathLength;
                averageNetDistance += analysis.NetDistance;
                averageClosureDistance += analysis.ClosureDistance;
                averagePathRatio += analysis.PathRatio;
                averagePerpendicularDrift += analysis.PerpendicularDrift;
                averageHorizontalDelta += analysis.HorizontalDelta;
                averageVerticalDelta += analysis.VerticalDelta;
                averageDirectionFlipCount += analysis.DirectionFlipCount;
                averageRepeatScore += analysis.RepeatScore;
                averageClosedLoopScore += analysis.ClosedLoopScore;
                averageFeatureDelta += analysis.FeatureDelta;
                averageFeaturePath += analysis.FeaturePath;
                if (analysis.RepeatScore >= 0.7f && analysis.DirectionFlipCount >= 2)
                {
                    repeatLikeAnalysisCount += 1;
                }

                if (analysis.IsHorizontal)
                {
                    horizontalAnalysisCount += 1;
                }
            }

            var analysisCount = analyses.Count;
            averageDuration /= analysisCount;
            averagePathLength /= analysisCount;
            averageNetDistance /= analysisCount;
            averageClosureDistance /= analysisCount;
            averagePathRatio /= analysisCount;
            averagePerpendicularDrift /= analysisCount;
            averageHorizontalDelta /= analysisCount;
            averageVerticalDelta /= analysisCount;
            averageDirectionFlipCount /= analysisCount;
            averageRepeatScore /= analysisCount;
            averageClosedLoopScore /= analysisCount;
            averageFeatureDelta /= analysisCount;
            averageFeaturePath /= analysisCount;

            rule.RequireOpenPalm = totalFrames > 0 && openPalmCount >= Mathf.CeilToInt(totalFrames * 0.5f);
            rule.MinimumOpenPalmRatio = rule.RequireOpenPalm ? 0.55f : 0f;

            var palmMotionDominates = averageNetDistance >= 0.10f || averagePathLength >= 0.16f;

            if (palmMotionDominates && averageClosedLoopScore >= 0.7f)
            {
                rule.Pattern = CustomGestureDynamicPattern.Loop;
                rule.Direction = CustomGestureMotionDirection.Any;
                rule.MinimumDistance = Mathf.Max(0.06f, averagePathLength * 0.28f);
                rule.MaximumClosureDistance = Mathf.Max(0.08f, averageClosureDistance * 1.2f);
                rule.MinimumPathRatio = Mathf.Max(1.2f, averagePathRatio * 0.85f);
                return rule;
            }

            var spreadAnalysisCount = 0;
            var averageFingerDistanceDelta = 0f;
            var averagePalmMotion = 0f;
            for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                var sample = samples[sampleIndex];
                if (sample?.Frames == null || sample.Frames.Count < 2)
                {
                    continue;
                }

                var firstFrame = sample.Frames[0];
                var lastFrame = sample.Frames[sample.Frames.Count - 1];
                if (!TryResolveFingerDistance(firstFrame, 4, 8, out var firstDistance) ||
                    !TryResolveFingerDistance(lastFrame, 4, 8, out var lastDistance) ||
                    !TryResolvePalm(firstFrame, out var firstPalm) ||
                    !TryResolvePalm(lastFrame, out var lastPalm))
                {
                    continue;
                }

                averageFingerDistanceDelta += lastDistance - firstDistance;
                averagePalmMotion += Vector2.Distance(firstPalm, lastPalm);
                spreadAnalysisCount += 1;
            }

            if (spreadAnalysisCount > 0)
            {
                averageFingerDistanceDelta /= spreadAnalysisCount;
                averagePalmMotion /= spreadAnalysisCount;
                if (averageFingerDistanceDelta > 0.18f && averagePalmMotion <= 0.2f)
                {
                    rule.Pattern = CustomGestureDynamicPattern.FingerDistanceChange;
                    rule.Direction = CustomGestureMotionDirection.Any;
                    rule.RequireOpenPalm = false;
                    rule.MinimumOpenPalmRatio = 0f;
                    rule.MinimumFingerDistanceDelta = Mathf.Max(0.12f, averageFingerDistanceDelta * 0.8f);
                    rule.MinimumFingerDistancePath = Mathf.Max(0.10f, averageFingerDistanceDelta * 0.9f);
                    rule.MaximumPalmMotion = Mathf.Max(0.12f, averagePalmMotion * 1.5f);
                    return rule;
                }
            }

            if (analysisCount >= 3
                && palmMotionDominates
                && repeatLikeAnalysisCount >= Mathf.CeilToInt(analysisCount * 0.7f)
                && averageRepeatScore >= 0.7f
                && averageDirectionFlipCount >= 2f)
            {
                rule.Pattern = CustomGestureDynamicPattern.Repeat;
                rule.Direction = horizontalAnalysisCount >= Mathf.CeilToInt(analysisCount * 0.5f)
                    ? (averageHorizontalDelta >= 0f ? CustomGestureMotionDirection.LeftToRight : CustomGestureMotionDirection.RightToLeft)
                    : (averageVerticalDelta >= 0f ? CustomGestureMotionDirection.BottomToTop : CustomGestureMotionDirection.TopToBottom);
                rule.RepeatCount = Mathf.Clamp(Mathf.RoundToInt(averageDirectionFlipCount + 1f), 2, 4);
                rule.MinimumDistance = Mathf.Max(0.06f, averagePathLength * 0.18f);
                rule.MaximumDrift = Mathf.Max(0.12f, averagePerpendicularDrift * 1.25f);
                return rule;
            }

            if (!palmMotionDominates && averageFeaturePath >= 0.18f && averageFeatureDelta >= 0.10f)
            {
                rule.Pattern = CustomGestureDynamicPattern.FeatureSequence;
                rule.Direction = CustomGestureMotionDirection.Any;
                rule.RequireOpenPalm = false;
                rule.MinimumOpenPalmRatio = 0f;
                rule.MinimumFeatureDelta = Mathf.Max(0.08f, averageFeatureDelta * 0.65f);
                rule.MinimumFeaturePath = Mathf.Max(0.12f, averageFeaturePath * 0.6f);
                rule.MinimumDuration = Mathf.Clamp(averageDuration * 0.25f, 0.05f, 0.4f);
                rule.MaximumDuration = Mathf.Clamp(averageDuration * 2.2f, 0.3f, 3f);
                return rule;
            }

            rule.Pattern = CustomGestureDynamicPattern.PalmTrajectory;
            rule.Direction = horizontalAnalysisCount >= Mathf.CeilToInt(analysisCount * 0.5f)
                ? (averageHorizontalDelta >= 0f ? CustomGestureMotionDirection.LeftToRight : CustomGestureMotionDirection.RightToLeft)
                : (averageVerticalDelta >= 0f ? CustomGestureMotionDirection.BottomToTop : CustomGestureMotionDirection.TopToBottom);
            rule.MinimumDistance = Mathf.Max(0.06f, averageNetDistance * 0.75f);
            rule.MaximumDrift = Mathf.Max(0.16f, averagePerpendicularDrift * 1.35f);
            rule.MinimumDuration = Mathf.Clamp(averageDuration * 0.3f, 0.05f, 0.4f);
            rule.MaximumDuration = Mathf.Clamp(averageDuration * 2f, 0.3f, 3f);
            return rule;
        }

        public static bool TryMatch(CustomGestureDynamicRule rule, IReadOnlyList<CustomGestureFrameSample> frames, float minimumConfidence, out float confidence)
        {
            confidence = 0f;
            if (rule == null || frames == null || frames.Count < 2)
            {
                return false;
            }

            var motionSamples = new List<MotionSample>(frames.Count);
            var openPalmCount = 0;
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                if (frame.Confidence < minimumConfidence)
                {
                    continue;
                }

                if (!TryResolvePalm(frame, out var palm))
                {
                    continue;
                }

                motionSamples.Add(new MotionSample
                {
                    Time = frame.Time,
                    Palm = palm,
                    StaticGesture = frame.StaticGesture,
                    Landmarks = frame.Landmarks
                });
                if (frame.StaticGesture == GestureType.OpenPalm)
                {
                    openPalmCount += 1;
                }
            }

            if (motionSamples.Count < 2)
            {
                return false;
            }

            var analysis = Analyze(motionSamples, rule.FingerAIndex, rule.FingerBIndex);
            if (rule.RequireOpenPalm && analysis.TotalFrames > 0)
            {
                var openPalmRatio = openPalmCount / (float)analysis.TotalFrames;
                if (openPalmRatio < rule.MinimumOpenPalmRatio)
                {
                    return false;
                }
            }

            if (analysis.Duration < rule.MinimumDuration || analysis.Duration > rule.MaximumDuration)
            {
                return false;
            }

            switch (rule.Pattern)
            {
                case CustomGestureDynamicPattern.PalmTrajectory:
                case CustomGestureDynamicPattern.Loop:
                    if (!analysis.IsClosedLoop(rule.MaximumClosureDistance, rule.MinimumPathRatio))
                    {
                        if (rule.Pattern == CustomGestureDynamicPattern.Loop)
                        {
                            return false;
                        }
                    }

                    if (rule.Pattern == CustomGestureDynamicPattern.Loop)
                    {
                        confidence = analysis.ClosedLoopScore;
                        return true;
                    }

                    if (!analysis.IsDirectional(rule.Direction, rule.MinimumDistance, rule.MaximumDrift, rule.MinimumAxisRatio))
                    {
                        return false;
                    }

                    confidence = analysis.DirectionScore;
                    return true;

                case CustomGestureDynamicPattern.FingerDistanceChange:
                case CustomGestureDynamicPattern.FingerSpread:
                    if (!analysis.IsFingerSpread(rule.FingerAIndex, rule.FingerBIndex, rule.MinimumFingerDistanceDelta, rule.MaximumPalmMotion))
                    {
                        return false;
                    }

                    confidence = analysis.FingerSpreadScore;
                    return true;

                case CustomGestureDynamicPattern.FingerOscillation:
                    if (!analysis.IsFingerOscillation(rule.MinimumOscillationCount, rule.MinimumFingerDistancePath, rule.MaximumPalmMotion))
                    {
                        return false;
                    }

                    confidence = analysis.FingerOscillationScore;
                    return true;

                case CustomGestureDynamicPattern.PoseTransition:
                    if (!analysis.IsPoseTransition(rule.StartPose, rule.EndPose, rule.PoseTransitionMaxPalmMotion))
                    {
                        return false;
                    }

                    confidence = analysis.PoseTransitionScore;
                    return true;

                case CustomGestureDynamicPattern.FeatureSequence:
                    if (!analysis.HasEnoughFeatureMotion(rule.MinimumFeatureDelta, rule.MinimumFeaturePath))
                    {
                        return false;
                    }

                    confidence = analysis.FeatureMotionScore;
                    return true;

                case CustomGestureDynamicPattern.Repeat:
                    if (!analysis.IsRepeat(rule.RepeatCount, rule.MinimumDistance, rule.MaximumDrift))
                    {
                        return false;
                    }

                    confidence = analysis.RepeatScore;
                    return true;

                default:
                    if (!analysis.IsDirectional(rule.Direction, rule.MinimumDistance, rule.MaximumDrift, rule.MinimumAxisRatio))
                    {
                        return false;
                    }

                    confidence = analysis.DirectionScore;
                    return true;
            }
        }

        private static bool TryResolvePalm(CustomGestureFrameSample frame, out Vector2 palm)
        {
            palm = frame.PalmCenter;
            if (palm != Vector2.zero)
            {
                return true;
            }

            if (frame.Landmarks == null || frame.Landmarks.Length <= 17)
            {
                return false;
            }

            palm = (frame.Landmarks[0] + frame.Landmarks[5] + frame.Landmarks[17]) / 3f;
            return true;
        }

        private static bool TryResolveFingerDistance(CustomGestureFrameSample frame, int fingerAIndex, int fingerBIndex, out float distance)
        {
            distance = 0f;
            if (frame?.Landmarks == null || frame.Landmarks.Length <= Mathf.Max(fingerAIndex, fingerBIndex))
            {
                return false;
            }

            distance = Vector2.Distance(frame.Landmarks[fingerAIndex], frame.Landmarks[fingerBIndex]);
            return true;
        }

        private static bool TryResolveFingerDistance(MotionSample sample, int fingerAIndex, int fingerBIndex, out float distance)
        {
            distance = 0f;
            if (sample.Landmarks == null || sample.Landmarks.Length <= Mathf.Max(fingerAIndex, fingerBIndex))
            {
                return false;
            }

            distance = Vector2.Distance(sample.Landmarks[fingerAIndex], sample.Landmarks[fingerBIndex]);
            return true;
        }

        private static MotionAnalysis Analyze(IReadOnlyList<MotionSample> samples, int fingerAIndex = 4, int fingerBIndex = 8)
        {
            var first = samples[0];
            var last = samples[samples.Count - 1];
            var pathLength = 0f;
            var directionFlipCount = 0;
            var fingerDirectionFlipCount = 0;
            var previousDelta = Vector2.zero;
            var hasPreviousDelta = false;
            var previousFingerDistance = 0f;
            var previousFingerDelta = 0f;
            var fingerPath = 0f;
            var peakFingerVelocity = 0f;
            var hasPreviousFingerDistance = false;
            var poseCounts = new Dictionary<GestureType, int>();

            for (var index = 1; index < samples.Count; index++)
            {
                pathLength += Vector2.Distance(samples[index - 1].Palm, samples[index].Palm);
                var delta = samples[index].Palm - samples[index - 1].Palm;
                if (delta.sqrMagnitude <= MotionEpsilon)
                {
                    continue;
                }

                if (hasPreviousDelta && Vector2.Dot(previousDelta.normalized, delta.normalized) < -0.25f)
                {
                    directionFlipCount += 1;
                }

                previousDelta = delta;
                hasPreviousDelta = true;
            }

            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                if (sample.StaticGesture != GestureType.None && sample.StaticGesture != GestureType.Unknown)
                {
                    poseCounts.TryGetValue(sample.StaticGesture, out var count);
                    poseCounts[sample.StaticGesture] = count + 1;
                }

                if (!TryResolveFingerDistance(sample, fingerAIndex, fingerBIndex, out var fingerDistance))
                {
                    continue;
                }

                if (hasPreviousFingerDistance)
                {
                    var fingerDelta = fingerDistance - previousFingerDistance;
                    fingerPath += Mathf.Abs(fingerDelta);
                    var dt = Mathf.Max(0.0001f, sample.Time - samples[index - 1].Time);
                    peakFingerVelocity = Mathf.Max(peakFingerVelocity, Mathf.Abs(fingerDelta) / dt);
                    if (Mathf.Abs(previousFingerDelta) > 0.0001f
                        && Mathf.Abs(fingerDelta) > 0.0001f
                        && Mathf.Sign(previousFingerDelta) != Mathf.Sign(fingerDelta))
                    {
                        fingerDirectionFlipCount += 1;
                    }

                    if (Mathf.Abs(fingerDelta) > 0.0001f)
                    {
                        previousFingerDelta = fingerDelta;
                    }
                }

                previousFingerDistance = fingerDistance;
                hasPreviousFingerDistance = true;
            }

            var horizontalDelta = last.Palm.x - first.Palm.x;
            var verticalDelta = last.Palm.y - first.Palm.y;
            var netDelta = last.Palm - first.Palm;
            var netDistance = netDelta.magnitude;
            var closureDistance = Vector2.Distance(first.Palm, last.Palm);
            var perpendicularDrift = Mathf.Abs(horizontalDelta) > Mathf.Abs(verticalDelta) ? Mathf.Abs(verticalDelta) : Mathf.Abs(horizontalDelta);
            var isHorizontal = Mathf.Abs(horizontalDelta) >= Mathf.Abs(verticalDelta);
            var pathRatio = closureDistance <= PalmEpsilon ? pathLength : pathLength / Mathf.Max(PalmEpsilon, closureDistance);
            var firstFingerDistance = 0f;
            var lastFingerDistance = 0f;
            if (TryResolveFingerDistance(first, fingerAIndex, fingerBIndex, out var firstSpread) && TryResolveFingerDistance(last, fingerAIndex, fingerBIndex, out var lastSpread))
            {
                firstFingerDistance = firstSpread;
                lastFingerDistance = lastSpread;
            }

            var featureDelta = 0f;
            var featurePath = 0f;
            if (TryResolveFeatureMotion(samples, out var resolvedFeatureDelta, out var resolvedFeaturePath))
            {
                featureDelta = resolvedFeatureDelta;
                featurePath = resolvedFeaturePath;
            }

            return new MotionAnalysis
            {
                TotalFrames = samples.Count,
                Duration = Mathf.Max(0.0001f, last.Time - first.Time),
                Start = first.Palm,
                End = last.Palm,
                PathLength = pathLength,
                NetDistance = netDistance,
                ClosureDistance = closureDistance,
                PathRatio = pathRatio,
                HorizontalDelta = horizontalDelta,
                VerticalDelta = verticalDelta,
                PerpendicularDrift = perpendicularDrift,
                DirectionFlipCount = directionFlipCount,
                IsHorizontal = isHorizontal,
                DirectionScore = ComputeDirectionalScore(netDistance, perpendicularDrift, pathLength),
                RepeatScore = ComputeRepeatScore(directionFlipCount, pathLength, netDistance),
                ClosedLoopScore = ComputeLoopScore(closureDistance, pathLength)
                ,
                FirstFingerDistance = firstFingerDistance,
                LastFingerDistance = lastFingerDistance,
                FingerSpreadScore = ComputeFingerSpreadScore(firstFingerDistance, lastFingerDistance, pathLength),
                FingerDistancePath = fingerPath,
                FingerPeakVelocity = peakFingerVelocity,
                FingerDirectionFlipCount = fingerDirectionFlipCount,
                FingerOscillationScore = ComputeFingerOscillationScore(fingerDirectionFlipCount, fingerPath, pathLength),
                FeatureDelta = featureDelta,
                FeaturePath = featurePath,
                FeatureMotionScore = ComputeFeatureMotionScore(featureDelta, featurePath),
                StartPose = first.StaticGesture,
                EndPose = last.StaticGesture,
                DominantPose = ResolveDominantPose(poseCounts),
                PoseTransitionScore = ComputePoseTransitionScore(first.StaticGesture, last.StaticGesture, pathLength)
            };
        }

        private static bool TryResolveFeatureMotion(IReadOnlyList<MotionSample> samples, out float featureDelta, out float featurePath)
        {
            featureDelta = 0f;
            featurePath = 0f;
            float[] firstFeatures = null;
            float[] lastFeatures = null;
            float[] previousFeatures = null;
            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                if (!CustomGestureFeatureExtractor.TryExtract(sample.Landmarks, 1f, 0f, out var features))
                {
                    continue;
                }

                firstFeatures ??= features;
                if (previousFeatures != null)
                {
                    var step = CustomGestureFeatureExtractor.Distance(previousFeatures, features);
                    if (!float.IsPositiveInfinity(step))
                    {
                        featurePath += step;
                    }
                }

                previousFeatures = features;
                lastFeatures = features;
            }

            if (firstFeatures == null || lastFeatures == null || ReferenceEquals(firstFeatures, lastFeatures))
            {
                return false;
            }

            featureDelta = CustomGestureFeatureExtractor.Distance(firstFeatures, lastFeatures);
            return !float.IsPositiveInfinity(featureDelta);
        }

        private static float ComputeDirectionalScore(float netDistance, float drift, float pathLength)
        {
            var distanceScore = Mathf.Clamp01(netDistance / 0.2f);
            var driftScore = 1f - Mathf.Clamp01(drift / 0.22f);
            var pathScore = Mathf.Clamp01(pathLength / 0.2f);
            return Mathf.Clamp01((distanceScore * 0.5f) + (driftScore * 0.2f) + (pathScore * 0.3f));
        }

        private static float ComputeRepeatScore(int flipCount, float pathLength, float netDistance)
        {
            var flipScore = Mathf.Clamp01(flipCount / 2f);
            var lengthScore = Mathf.Clamp01(pathLength / 0.25f);
            var settleScore = 1f - Mathf.Clamp01(netDistance / Mathf.Max(0.0001f, pathLength));
            return Mathf.Clamp01((flipScore * 0.45f) + (lengthScore * 0.35f) + (settleScore * 0.2f));
        }

        private static float ComputeLoopScore(float closureDistance, float pathLength)
        {
            var closureScore = 1f - Mathf.Clamp01(closureDistance / 0.15f);
            var lengthScore = Mathf.Clamp01(pathLength / 0.25f);
            return Mathf.Clamp01((closureScore * 0.6f) + (lengthScore * 0.4f));
        }

        private static float ComputeFingerSpreadScore(float firstFingerDistance, float lastFingerDistance, float pathLength)
        {
            var spreadDelta = Mathf.Clamp01((lastFingerDistance - firstFingerDistance) / 0.25f);
            var palmStabilityScore = 1f - Mathf.Clamp01(pathLength / 0.12f);
            return Mathf.Clamp01((spreadDelta * 0.85f) + (palmStabilityScore * 0.15f));
        }

        private static float ComputeFeatureMotionScore(float featureDelta, float featurePath)
        {
            var deltaScore = Mathf.Clamp01(featureDelta / 0.25f);
            var pathScore = Mathf.Clamp01(featurePath / 0.45f);
            return Mathf.Clamp01((deltaScore * 0.55f) + (pathScore * 0.45f));
        }

        private static float ComputeFingerOscillationScore(int flipCount, float fingerPath, float palmPath)
        {
            var flipScore = Mathf.Clamp01(flipCount / 2f);
            var pathScore = Mathf.Clamp01(fingerPath / 0.22f);
            var palmStabilityScore = 1f - Mathf.Clamp01(palmPath / 0.18f);
            return Mathf.Clamp01((flipScore * 0.45f) + (pathScore * 0.4f) + (palmStabilityScore * 0.15f));
        }

        private static float ComputePoseTransitionScore(GestureType startPose, GestureType endPose, float palmPath)
        {
            if (startPose == GestureType.None || startPose == GestureType.Unknown || endPose == GestureType.None || endPose == GestureType.Unknown || startPose == endPose)
            {
                return 0f;
            }

            var palmStabilityScore = 1f - Mathf.Clamp01(palmPath / 0.16f);
            return Mathf.Clamp01(0.75f + palmStabilityScore * 0.25f);
        }

        private static GestureType ResolveDominantPose(Dictionary<GestureType, int> counts)
        {
            var best = GestureType.Unknown;
            var bestCount = 0;
            foreach (var pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }

            return best;
        }

        private struct MotionAnalysis
        {
            public int TotalFrames;
            public float Duration;
            public Vector2 Start;
            public Vector2 End;
            public float PathLength;
            public float NetDistance;
            public float ClosureDistance;
            public float PathRatio;
            public float HorizontalDelta;
            public float VerticalDelta;
            public float PerpendicularDrift;
            public int DirectionFlipCount;
            public bool IsHorizontal;
            public float DirectionScore;
            public float RepeatScore;
            public float ClosedLoopScore;
            public float FirstFingerDistance;
            public float LastFingerDistance;
            public float FingerSpreadScore;
            public float FingerDistancePath;
            public float FingerPeakVelocity;
            public int FingerDirectionFlipCount;
            public float FingerOscillationScore;
            public float FeatureDelta;
            public float FeaturePath;
            public float FeatureMotionScore;
            public GestureType StartPose;
            public GestureType EndPose;
            public GestureType DominantPose;
            public float PoseTransitionScore;

            public bool IsDirectional(CustomGestureMotionDirection direction, float minimumDistance, float maximumDrift, float minimumAxisRatio)
            {
                if (NetDistance < minimumDistance || PerpendicularDrift > maximumDrift)
                {
                    return false;
                }

                if (minimumAxisRatio > 0f)
                {
                    var horizontalDominance = Mathf.Abs(HorizontalDelta) / Mathf.Max(MotionEpsilon, Mathf.Abs(VerticalDelta));
                    var verticalDominance = Mathf.Abs(VerticalDelta) / Mathf.Max(MotionEpsilon, Mathf.Abs(HorizontalDelta));
                    var passesAxisRatio = direction switch
                    {
                        CustomGestureMotionDirection.LeftToRight => horizontalDominance >= minimumAxisRatio,
                        CustomGestureMotionDirection.RightToLeft => horizontalDominance >= minimumAxisRatio,
                        CustomGestureMotionDirection.BottomToTop => verticalDominance >= minimumAxisRatio,
                        CustomGestureMotionDirection.TopToBottom => verticalDominance >= minimumAxisRatio,
                        _ => horizontalDominance >= minimumAxisRatio || verticalDominance >= minimumAxisRatio
                    };

                    if (!passesAxisRatio)
                    {
                        return false;
                    }
                }

                return direction switch
                {
                    CustomGestureMotionDirection.Any => true,
                    CustomGestureMotionDirection.LeftToRight => HorizontalDelta > 0f,
                    CustomGestureMotionDirection.RightToLeft => HorizontalDelta < 0f,
                    CustomGestureMotionDirection.BottomToTop => VerticalDelta > 0f,
                    CustomGestureMotionDirection.TopToBottom => VerticalDelta < 0f,
                    _ => true
                };
            }

            public bool IsRepeat(int repeatCount, float minimumDistance, float maximumDrift)
            {
                if (repeatCount < 2 || PathLength < minimumDistance || PerpendicularDrift > maximumDrift)
                {
                    return false;
                }

                return DirectionFlipCount >= repeatCount - 1;
            }

            public bool IsClosedLoop(float maximumClosureDistance, float minimumPathRatio)
            {
                return ClosureDistance <= maximumClosureDistance && PathRatio >= minimumPathRatio;
            }

            public bool IsFingerSpread(int fingerAIndex, int fingerBIndex, float minimumFingerDistanceDelta, float maximumPalmMotion)
            {
                if (FingerSpreadScore <= 0f)
                {
                    return false;
                }

                var distanceDelta = LastFingerDistance - FirstFingerDistance;
                if (distanceDelta < minimumFingerDistanceDelta)
                {
                    return false;
                }

                return PathLength <= Mathf.Max(0.0001f, maximumPalmMotion);
            }

            public bool IsFingerOscillation(int minimumOscillationCount, float minimumFingerDistancePath, float maximumPalmMotion)
            {
                return FingerDirectionFlipCount >= minimumOscillationCount
                       && FingerDistancePath >= minimumFingerDistancePath
                       && PathLength <= Mathf.Max(0.0001f, maximumPalmMotion);
            }

            public bool IsPoseTransition(GestureType startPose, GestureType endPose, float maximumPalmMotion)
            {
                if (startPose != GestureType.Unknown && startPose != GestureType.None && StartPose != startPose)
                {
                    return false;
                }

                if (endPose != GestureType.Unknown && endPose != GestureType.None && EndPose != endPose)
                {
                    return false;
                }

                if (StartPose == GestureType.None || StartPose == GestureType.Unknown || EndPose == GestureType.None || EndPose == GestureType.Unknown || StartPose == EndPose)
                {
                    return false;
                }

                return PathLength <= Mathf.Max(0.0001f, maximumPalmMotion);
            }

            public bool HasEnoughFeatureMotion(float minimumFeatureDelta, float minimumFeaturePath)
            {
                return FeatureDelta >= minimumFeatureDelta && FeaturePath >= minimumFeaturePath;
            }
        }
    }
}

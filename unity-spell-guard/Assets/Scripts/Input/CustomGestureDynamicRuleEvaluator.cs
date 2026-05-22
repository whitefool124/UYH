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
                RepeatCount = 2,
                MinimumPathRatio = 1.6f,
                MaximumClosureDistance = 0.12f
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
                        StaticGesture = frame.StaticGesture
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

            rule.RequireOpenPalm = totalFrames > 0 && openPalmCount >= Mathf.CeilToInt(totalFrames * 0.5f);
            rule.MinimumOpenPalmRatio = rule.RequireOpenPalm ? 0.55f : 0f;

            if (averageClosedLoopScore >= 0.7f)
            {
                rule.Pattern = CustomGestureDynamicPattern.Loop;
                rule.Direction = CustomGestureMotionDirection.Any;
                rule.MinimumDistance = Mathf.Max(0.06f, averagePathLength * 0.28f);
                rule.MaximumClosureDistance = Mathf.Max(0.08f, averageClosureDistance * 1.2f);
                rule.MinimumPathRatio = Mathf.Max(1.2f, averagePathRatio * 0.85f);
                return rule;
            }

            if (averageRepeatScore >= 0.7f && averageDirectionFlipCount >= 2f)
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

            rule.Pattern = CustomGestureDynamicPattern.Directional;
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
                    StaticGesture = frame.StaticGesture
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

            var analysis = Analyze(motionSamples);
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
                case CustomGestureDynamicPattern.Loop:
                    if (!analysis.IsClosedLoop(rule.MaximumClosureDistance, rule.MinimumPathRatio))
                    {
                        return false;
                    }

                    confidence = analysis.ClosedLoopScore;
                    return true;

                case CustomGestureDynamicPattern.Repeat:
                    if (!analysis.IsRepeat(rule.RepeatCount, rule.MinimumDistance, rule.MaximumDrift))
                    {
                        return false;
                    }

                    confidence = analysis.RepeatScore;
                    return true;

                default:
                    if (!analysis.IsDirectional(rule.Direction, rule.MinimumDistance, rule.MaximumDrift))
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

        private static MotionAnalysis Analyze(IReadOnlyList<MotionSample> samples)
        {
            var first = samples[0];
            var last = samples[samples.Count - 1];
            var pathLength = 0f;
            var directionFlipCount = 0;
            var previousDelta = Vector2.zero;
            var hasPreviousDelta = false;

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

            var horizontalDelta = last.Palm.x - first.Palm.x;
            var verticalDelta = last.Palm.y - first.Palm.y;
            var netDelta = last.Palm - first.Palm;
            var netDistance = netDelta.magnitude;
            var closureDistance = Vector2.Distance(first.Palm, last.Palm);
            var perpendicularDrift = Mathf.Abs(horizontalDelta) > Mathf.Abs(verticalDelta) ? Mathf.Abs(verticalDelta) : Mathf.Abs(horizontalDelta);
            var isHorizontal = Mathf.Abs(horizontalDelta) >= Mathf.Abs(verticalDelta);
            var pathRatio = closureDistance <= PalmEpsilon ? pathLength : pathLength / Mathf.Max(PalmEpsilon, closureDistance);
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
            };
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

            public bool IsDirectional(CustomGestureMotionDirection direction, float minimumDistance, float maximumDrift)
            {
                if (NetDistance < minimumDistance || PerpendicularDrift > maximumDrift)
                {
                    return false;
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
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public static class CustomGestureTrajectoryMatcher
    {
        private const int DefaultResampleCount = 32;
        private const float MinimumTrajectoryDistance = 0.0001f;

        public static bool TryBuildTrajectory(CustomGestureSample sample, float minimumConfidence, out Vector2[] trajectory)
        {
            trajectory = null;
            if (sample?.Frames == null || sample.Frames.Count < 2)
            {
                return false;
            }

            var points = new List<Vector2>(sample.Frames.Count);
            for (var index = 0; index < sample.Frames.Count; index++)
            {
                var frame = sample.Frames[index];
                if (frame.Confidence < minimumConfidence)
                {
                    continue;
                }

                if (!TryResolvePalm(frame, out var palm))
                {
                    continue;
                }

                points.Add(palm);
            }

            return TryNormalizeAndResample(points, out trajectory);
        }

        public static bool TryMeasureRawMotion(CustomGestureSample sample, float minimumConfidence, out float netDistance, out float pathLength)
        {
            netDistance = 0f;
            pathLength = 0f;
            if (sample?.Frames == null || sample.Frames.Count < 2)
            {
                return false;
            }

            return TryMeasureRawMotion(sample.Frames, minimumConfidence, out netDistance, out pathLength);
        }

        public static bool TryMeasureRawMotion(IReadOnlyList<CustomGestureFrameSample> frames, float minimumConfidence, out float netDistance, out float pathLength)
        {
            netDistance = 0f;
            pathLength = 0f;
            if (frames == null || frames.Count < 2)
            {
                return false;
            }

            var hasFirst = false;
            var first = Vector2.zero;
            var last = Vector2.zero;
            var previous = Vector2.zero;
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                if (frame.Confidence < minimumConfidence || !TryResolvePalm(frame, out var palm))
                {
                    continue;
                }

                if (!hasFirst)
                {
                    first = palm;
                    hasFirst = true;
                }
                else
                {
                    pathLength += Vector2.Distance(previous, palm);
                }

                previous = palm;
                last = palm;
            }

            if (!hasFirst)
            {
                return false;
            }

            netDistance = Vector2.Distance(first, last);
            return true;
        }

        public static bool TryBuildTrajectory(IReadOnlyList<CustomGestureFrameSample> frames, float minimumConfidence, out Vector2[] trajectory)
        {
            trajectory = null;
            if (frames == null || frames.Count < 2)
            {
                return false;
            }

            var points = new List<Vector2>(frames.Count);
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

                points.Add(palm);
            }

            return TryNormalizeAndResample(points, out trajectory);
        }

        public static float Score(Vector2[] runtimeTrajectory, Vector2[] templateTrajectory)
        {
            if (runtimeTrajectory == null || templateTrajectory == null || runtimeTrajectory.Length == 0 || templateTrajectory.Length == 0)
            {
                return float.PositiveInfinity;
            }

            var rows = runtimeTrajectory.Length;
            var columns = templateTrajectory.Length;
            var costs = new float[rows + 1, columns + 1];
            for (var row = 0; row <= rows; row++)
            {
                costs[row, 0] = float.PositiveInfinity;
            }

            for (var column = 0; column <= columns; column++)
            {
                costs[0, column] = float.PositiveInfinity;
            }

            costs[0, 0] = 0f;
            for (var row = 1; row <= rows; row++)
            {
                for (var column = 1; column <= columns; column++)
                {
                    var localCost = Vector2.Distance(runtimeTrajectory[row - 1], templateTrajectory[column - 1]);
                    var previous = Mathf.Min(costs[row - 1, column], costs[row, column - 1], costs[row - 1, column - 1]);
                    costs[row, column] = localCost + previous;
                }
            }

            return costs[rows, columns] / (rows + columns);
        }

        public static float ScoreAgainstSample(IReadOnlyList<CustomGestureFrameSample> runtimeFrames, CustomGestureSample templateSample, float minimumConfidence)
        {
            return TryBuildTrajectory(runtimeFrames, minimumConfidence, out var runtimeTrajectory) &&
                   TryBuildTrajectory(templateSample, minimumConfidence, out var templateTrajectory)
                ? Score(runtimeTrajectory, templateTrajectory)
                : float.PositiveInfinity;
        }

        public static float ScoreBestWindow(IReadOnlyList<CustomGestureFrameSample> runtimeFrames, Vector2[] templateTrajectory, float minimumConfidence, float templateDurationSeconds)
        {
            if (runtimeFrames == null || runtimeFrames.Count < 2 || templateTrajectory == null || templateTrajectory.Length < 2)
            {
                return float.PositiveInfinity;
            }

            var best = float.PositiveInfinity;
            var targetDuration = Mathf.Clamp(templateDurationSeconds, 0.25f, 2.5f);
            var minimumDuration = Mathf.Max(0.24f, targetDuration * 0.55f);
            var maximumDuration = Mathf.Max(3.0f, targetDuration * 4.0f);
            var templateDelta = templateTrajectory[templateTrajectory.Length - 1] - templateTrajectory[0];
            for (var start = 0; start < runtimeFrames.Count - 1; start++)
            {
                for (var end = start + 2; end <= runtimeFrames.Count; end++)
                {
                    var duration = runtimeFrames[end - 1].Time - runtimeFrames[start].Time;
                    if (duration < minimumDuration)
                    {
                        continue;
                    }

                    if (duration > maximumDuration)
                    {
                        break;
                    }

                    if (!PassesRuntimeMotionGate(runtimeFrames, start, end - start, minimumConfidence, templateDelta))
                    {
                        continue;
                    }

                    if (!TryBuildTrajectory(new WindowSlice(runtimeFrames, start, end - start), minimumConfidence, out var runtimeTrajectory))
                    {
                        continue;
                    }

                    best = Mathf.Min(best, Score(runtimeTrajectory, templateTrajectory));
                }
            }

            if (best < float.PositiveInfinity)
            {
                return best;
            }

            return PassesRuntimeMotionGate(runtimeFrames, 0, runtimeFrames.Count, minimumConfidence, templateDelta)
                   && TryBuildTrajectory(runtimeFrames, minimumConfidence, out var fullTrajectory)
                ? Score(fullTrajectory, templateTrajectory)
                : float.PositiveInfinity;
        }

        private static bool PassesRuntimeMotionGate(
            IReadOnlyList<CustomGestureFrameSample> frames,
            int start,
            int count,
            float minimumConfidence,
            Vector2 templateDelta)
        {
            if (frames == null || count < 4)
            {
                return false;
            }

            var hasFirst = false;
            var first = Vector2.zero;
            var last = Vector2.zero;
            var previous = Vector2.zero;
            var pathLength = 0f;
            var progress = 0f;
            var reverseProgress = 0f;
            var perpendicularPath = 0f;
            var axis = ResolveDominantAxis(templateDelta);
            for (var offset = 0; offset < count; offset++)
            {
                var frame = frames[start + offset];
                if (frame.Confidence < minimumConfidence || !TryResolvePalm(frame, out var palm))
                {
                    continue;
                }

                if (!hasFirst)
                {
                    first = palm;
                    hasFirst = true;
                }
                else
                {
                    var step = palm - previous;
                    pathLength += step.magnitude;
                    var axisStep = Vector2.Dot(step, axis);
                    if (axisStep >= 0f)
                    {
                        progress += axisStep;
                    }
                    else
                    {
                        reverseProgress += -axisStep;
                    }

                    perpendicularPath += Mathf.Abs(Cross(axis, step));
                }

                previous = palm;
                last = palm;
            }

            if (!hasFirst)
            {
                return false;
            }

            var delta = last - first;
            var netDistance = delta.magnitude;
            var templateDistance = templateDelta.magnitude;
            var minimumNetDistance = Mathf.Clamp(templateDistance * 0.35f, 0.018f, 0.055f);
            var minimumPathLength = Mathf.Max(0.025f, minimumNetDistance * 1.2f);
            if (netDistance < minimumNetDistance || pathLength < minimumPathLength)
            {
                return false;
            }

            if (pathLength / Mathf.Max(0.0001f, netDistance) > 5.5f)
            {
                return false;
            }

            var netAxisProgress = Vector2.Dot(delta, axis);
            var minimumAxisProgress = Mathf.Clamp(templateDistance * 0.25f, 0.012f, 0.038f);
            if (netAxisProgress < minimumAxisProgress || progress < minimumAxisProgress * 1.2f)
            {
                return false;
            }

            if (reverseProgress > Mathf.Max(0.035f, progress * 0.65f))
            {
                return false;
            }

            if (perpendicularPath > Mathf.Max(0.080f, progress * 1.65f))
            {
                return false;
            }

            if (templateDelta.sqrMagnitude <= 0.0025f)
            {
                return true;
            }

            return HasCompatibleDominantAxis(delta, templateDelta);
        }

        private static Vector2 ResolveDominantAxis(Vector2 templateDelta)
        {
            if (templateDelta.sqrMagnitude <= 0.0025f)
            {
                return Vector2.down;
            }

            return Mathf.Abs(templateDelta.x) > Mathf.Abs(templateDelta.y) * 1.15f
                ? new Vector2(Mathf.Sign(templateDelta.x), 0f)
                : new Vector2(0f, Mathf.Sign(templateDelta.y));
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
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

        private static bool TryNormalizeAndResample(IReadOnlyList<Vector2> points, out Vector2[] trajectory)
        {
            trajectory = null;
            if (points == null || points.Count < 2)
            {
                return false;
            }

            var sampled = new List<Vector2>(points.Count);
            sampled.Add(points[0]);
            for (var index = 1; index < points.Count; index++)
            {
                var point = points[index];
                if (Vector2.Distance(sampled[sampled.Count - 1], point) <= MinimumTrajectoryDistance)
                {
                    continue;
                }

                sampled.Add(point);
            }

            if (sampled.Count < 2)
            {
                return false;
            }

            var origin = sampled[0];
            var translated = new Vector2[sampled.Count];
            var maxDistance = 0f;
            for (var index = 0; index < sampled.Count; index++)
            {
                translated[index] = sampled[index] - origin;
                maxDistance = Mathf.Max(maxDistance, translated[index].magnitude);
            }

            if (maxDistance <= MinimumTrajectoryDistance)
            {
                return false;
            }

            for (var index = 0; index < translated.Length; index++)
            {
                translated[index] /= maxDistance;
            }

            trajectory = Resample(translated, DefaultResampleCount);
            return trajectory != null && trajectory.Length >= 2;
        }

        private static Vector2[] Resample(IReadOnlyList<Vector2> points, int targetCount)
        {
            if (points == null || points.Count == 0 || targetCount < 2)
            {
                return null;
            }

            if (points.Count == 1)
            {
                return new[] { points[0], points[0] };
            }

            var cumulative = new float[points.Count];
            cumulative[0] = 0f;
            var totalLength = 0f;
            for (var index = 1; index < points.Count; index++)
            {
                totalLength += Vector2.Distance(points[index - 1], points[index]);
                cumulative[index] = totalLength;
            }

            if (totalLength <= MinimumTrajectoryDistance)
            {
                return null;
            }

            var result = new Vector2[targetCount];
            result[0] = points[0];
            result[targetCount - 1] = points[points.Count - 1];

            var segment = 1;
            for (var index = 1; index < targetCount - 1; index++)
            {
                var targetDistance = totalLength * index / (targetCount - 1f);
                while (segment < cumulative.Length - 1 && cumulative[segment] < targetDistance)
                {
                    segment++;
                }

                var previousIndex = Mathf.Max(0, segment - 1);
                var nextIndex = Mathf.Min(points.Count - 1, segment);
                var segmentStart = cumulative[previousIndex];
                var segmentEnd = cumulative[nextIndex];
                var t = segmentEnd <= segmentStart + MinimumTrajectoryDistance
                    ? 0f
                    : Mathf.Clamp01((targetDistance - segmentStart) / (segmentEnd - segmentStart));
                result[index] = Vector2.Lerp(points[previousIndex], points[nextIndex], t);
            }

            return result;
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

        private readonly struct WindowSlice : IReadOnlyList<CustomGestureFrameSample>
        {
            private readonly IReadOnlyList<CustomGestureFrameSample> source;
            private readonly int start;

            public WindowSlice(IReadOnlyList<CustomGestureFrameSample> source, int start, int count)
            {
                this.source = source;
                this.start = start;
                Count = count;
            }

            public int Count { get; }

            public CustomGestureFrameSample this[int index] => source[start + index];

            public IEnumerator<CustomGestureFrameSample> GetEnumerator()
            {
                for (var index = 0; index < Count; index++)
                {
                    yield return this[index];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}

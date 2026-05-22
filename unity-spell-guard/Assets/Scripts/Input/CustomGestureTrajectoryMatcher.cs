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
            var minimumDuration = targetDuration * 0.55f;
            var maximumDuration = targetDuration * 1.65f;
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

            return TryBuildTrajectory(runtimeFrames, minimumConfidence, out var fullTrajectory)
                ? Score(fullTrajectory, templateTrajectory)
                : float.PositiveInfinity;
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

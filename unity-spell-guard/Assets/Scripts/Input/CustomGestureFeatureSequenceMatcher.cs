using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public static class CustomGestureFeatureSequenceMatcher
    {
        private const int DefaultResampleCount = 24;
        private const float MinimumStepDistance = 0.0001f;

        public static bool TryBuildSequence(CustomGestureSample sample, float minimumConfidence, out CustomGestureFeatureFrameTemplate[] sequence)
        {
            sequence = null;
            if (sample?.Frames == null)
            {
                return false;
            }

            return TryBuildSequence(sample.Frames, minimumConfidence, out sequence);
        }

        public static bool TryBuildSequence(IReadOnlyList<CustomGestureFrameSample> frames, float minimumConfidence, out CustomGestureFeatureFrameTemplate[] sequence)
        {
            sequence = null;
            if (frames == null || frames.Count < 2)
            {
                return false;
            }

            var featureFrames = new List<float[]>(frames.Count);
            for (var index = 0; index < frames.Count; index++)
            {
                if (CustomGestureFeatureExtractor.TryExtract(frames[index], minimumConfidence, out var features))
                {
                    featureFrames.Add(features);
                }
            }

            if (featureFrames.Count < 2)
            {
                return false;
            }

            var resampled = Resample(featureFrames, DefaultResampleCount);
            if (resampled == null || resampled.Length < 2)
            {
                return false;
            }

            sequence = new CustomGestureFeatureFrameTemplate[resampled.Length];
            for (var index = 0; index < resampled.Length; index++)
            {
                sequence[index] = new CustomGestureFeatureFrameTemplate
                {
                    Features = resampled[index]
                };
            }

            return true;
        }

        public static float ScoreBestWindow(IReadOnlyList<CustomGestureFrameSample> runtimeFrames, CustomGestureFeatureFrameTemplate[] templateSequence, float minimumConfidence, float templateDurationSeconds)
        {
            if (runtimeFrames == null || runtimeFrames.Count < 2 || templateSequence == null || templateSequence.Length < 2)
            {
                return float.PositiveInfinity;
            }

            var best = float.PositiveInfinity;
            var targetDuration = Mathf.Clamp(templateDurationSeconds, 0.2f, 2.5f);
            var minimumDuration = Mathf.Min(0.18f, targetDuration * 0.4f);
            var maximumDuration = Mathf.Max(3.0f, targetDuration * 4.0f);
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

                    if (!TryBuildSequence(new WindowSlice(runtimeFrames, start, end - start), minimumConfidence, out var runtimeSequence))
                    {
                        continue;
                    }

                    best = Mathf.Min(best, Score(runtimeSequence, templateSequence));
                }
            }

            if (best < float.PositiveInfinity)
            {
                return best;
            }

            return TryBuildSequence(runtimeFrames, minimumConfidence, out var fullSequence)
                ? Score(fullSequence, templateSequence)
                : float.PositiveInfinity;
        }

        private static float Score(CustomGestureFeatureFrameTemplate[] runtimeSequence, CustomGestureFeatureFrameTemplate[] templateSequence)
        {
            if (runtimeSequence == null || templateSequence == null || runtimeSequence.Length == 0 || templateSequence.Length == 0)
            {
                return float.PositiveInfinity;
            }

            var rows = runtimeSequence.Length;
            var columns = templateSequence.Length;
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
                    var localCost = CustomGestureFeatureExtractor.Distance(runtimeSequence[row - 1].Features, templateSequence[column - 1].Features);
                    var previous = Mathf.Min(costs[row - 1, column], costs[row, column - 1], costs[row - 1, column - 1]);
                    costs[row, column] = localCost + previous;
                }
            }

            return costs[rows, columns] / (rows + columns);
        }

        private static float[][] Resample(IReadOnlyList<float[]> featureFrames, int targetCount)
        {
            if (featureFrames == null || featureFrames.Count < 2 || targetCount < 2)
            {
                return null;
            }

            var cumulative = new float[featureFrames.Count];
            cumulative[0] = 0f;
            var totalLength = 0f;
            for (var index = 1; index < featureFrames.Count; index++)
            {
                var step = CustomGestureFeatureExtractor.Distance(featureFrames[index - 1], featureFrames[index]);
                if (float.IsPositiveInfinity(step))
                {
                    return null;
                }

                totalLength += step;
                cumulative[index] = totalLength;
            }

            if (totalLength <= MinimumStepDistance)
            {
                return null;
            }

            var result = new float[targetCount][];
            result[0] = Copy(featureFrames[0]);
            result[targetCount - 1] = Copy(featureFrames[featureFrames.Count - 1]);
            var segment = 1;
            for (var index = 1; index < targetCount - 1; index++)
            {
                var targetDistance = totalLength * index / (targetCount - 1f);
                while (segment < cumulative.Length - 1 && cumulative[segment] < targetDistance)
                {
                    segment++;
                }

                var previousIndex = Mathf.Max(0, segment - 1);
                var nextIndex = Mathf.Min(featureFrames.Count - 1, segment);
                var segmentStart = cumulative[previousIndex];
                var segmentEnd = cumulative[nextIndex];
                var t = segmentEnd <= segmentStart + MinimumStepDistance
                    ? 0f
                    : Mathf.Clamp01((targetDistance - segmentStart) / (segmentEnd - segmentStart));
                result[index] = Lerp(featureFrames[previousIndex], featureFrames[nextIndex], t);
            }

            return result;
        }

        private static float[] Copy(float[] source)
        {
            if (source == null)
            {
                return System.Array.Empty<float>();
            }

            var result = new float[source.Length];
            System.Array.Copy(source, result, source.Length);
            return result;
        }

        private static float[] Lerp(float[] first, float[] second, float t)
        {
            if (first == null || second == null || first.Length != second.Length)
            {
                return System.Array.Empty<float>();
            }

            var result = new float[first.Length];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = Mathf.Lerp(first[index], second[index], t);
            }

            return result;
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

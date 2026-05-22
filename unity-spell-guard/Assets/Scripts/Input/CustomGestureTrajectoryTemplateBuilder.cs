using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public static class CustomGestureTrajectoryTemplateBuilder
    {
        public static List<CustomGestureTrajectoryTemplate> Build(IReadOnlyList<CustomGestureSample> samples)
        {
            var templates = new List<CustomGestureTrajectoryTemplate>();
            if (samples == null)
            {
                return templates;
            }

            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                if (!CustomGestureTrajectoryMatcher.TryBuildTrajectory(sample, 0.5f, out var trajectory))
                {
                    continue;
                }

                templates.Add(new CustomGestureTrajectoryTemplate
                {
                    SampleId = sample.SampleId,
                    DurationSeconds = sample.DurationSeconds,
                    Points = trajectory
                });
            }

            return templates;
        }
    }
}

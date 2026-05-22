using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public enum CustomGestureKind
    {
        StaticPose,
        DynamicMotion
    }

    public enum CustomGestureDynamicPattern
    {
        Directional,
        Repeat,
        Loop
    }

    public enum CustomGestureMotionDirection
    {
        Any,
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [Serializable]
    public sealed class CustomGestureDynamicRule
    {
        public CustomGestureDynamicPattern Pattern = CustomGestureDynamicPattern.Directional;
        public CustomGestureMotionDirection Direction = CustomGestureMotionDirection.Any;
        public bool RequireOpenPalm = true;
        public float MinimumOpenPalmRatio = 0.8f;
        public float MinimumDistance = 0.12f;
        public float MaximumDrift = 0.22f;
        public float MinimumDuration = 0.12f;
        public float MaximumDuration = 2f;
        public int RepeatCount = 2;
        public float MinimumPathRatio = 1.6f;
        public float MaximumClosureDistance = 0.12f;
    }

    [Serializable]
    public sealed class CustomGestureTrajectoryTemplate
    {
        public string SampleId;
        public float DurationSeconds;
        public Vector2[] Points = System.Array.Empty<Vector2>();
    }

    [Serializable]
    public sealed class CustomGestureTemplate
    {
        public string GestureId;
        public string DisplayName;
        public CustomGestureKind Kind = CustomGestureKind.DynamicMotion;
        public GestureHandedness RequiredHandedness = GestureHandedness.Unknown;
        public GestureIntent TargetIntent = GestureIntent.CustomGesture;
        public float MatchThreshold = CustomGestureRecognizer.DefaultDynamicThreshold;
        public CustomGestureDynamicRule DynamicRule;
        public List<CustomGestureSample> Samples = new List<CustomGestureSample>();
        public List<CustomGestureTrajectoryTemplate> TrajectoryTemplates = new List<CustomGestureTrajectoryTemplate>();

        public bool HasSamples => Samples != null && Samples.Count > 0;
    }

    [Serializable]
    public sealed class CustomGestureSample
    {
        public string SampleId;
        public GestureHandedness Handedness = GestureHandedness.Unknown;
        public float DurationSeconds;
        public List<CustomGestureFrameSample> Frames = new List<CustomGestureFrameSample>();

        public bool HasFrames => Frames != null && Frames.Count > 0;
    }

    [Serializable]
    public sealed class CustomGestureFrameSample
    {
        public float Time;
        public float Confidence;
        public GestureType StaticGesture = GestureType.None;
        public Vector2 PalmCenter;
        public Vector2[] Landmarks = Array.Empty<Vector2>();
    }
}

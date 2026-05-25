using System;
using System.Collections.Generic;

namespace SpellGuard.InputSystem
{
    [Serializable]
    public sealed class CustomGestureTemplateValidationReport
    {
        public string GestureId;
        public string DisplayName;
        public CustomGestureDynamicPattern Pattern;
        public bool Active;
        public string FailureReason;
        public int SampleCount;
        public int MatchedSampleCount;
        public List<CustomGestureTemplateValidationSampleResult> Samples = new List<CustomGestureTemplateValidationSampleResult>();
    }

    [Serializable]
    public sealed class CustomGestureTemplateValidationSampleResult
    {
        public string SampleId;
        public int FrameCount;
        public float Threshold;
        public float BestScore;
        public bool Matched;
        public float TriggeredAt;
        public string FailureReason;
    }
}

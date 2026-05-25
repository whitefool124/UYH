namespace SpellGuard.InputSystem
{
    public struct CustomGestureFrameFeatures
    {
        public float[] NormalizedLandmarks;
        public float ThumbIndexDistance;
        public float ThumbMiddleDistance;
        public float IndexMiddleDistance;
        public float IndexRingDistance;
        public float MiddleRingDistance;
        public float ThumbCurl;
        public float IndexCurl;
        public float MiddleCurl;
        public float RingCurl;
        public float PinkyCurl;
    }

    public struct CustomGestureSequenceFeatures
    {
        public float Duration;
        public float PalmNetDistance;
        public float PalmPathLength;
        public float FeatureNetDistance;
        public float FeaturePathLength;
        public float SelectedFingerDistanceDelta;
        public float SelectedFingerDistancePath;
        public float SelectedFingerPeakVelocity;
        public int OscillationCount;
        public GestureType DominantStaticPose;
        public GestureType StartPose;
        public GestureType EndPose;
    }
}

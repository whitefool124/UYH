using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class NativeMediapipeGestureProvider : GestureInputProviderBase
    {
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private bool mirrorX = false;
        [SerializeField] private bool mirrorY = true;
        [SerializeField] private float motionEventTimeout = 1.2f;
        private GestureSnapshot snapshot = GestureSnapshot.Missing;
        private string statusText = "原生识别未启动";
        private Vector2[] handLandmarks = System.Array.Empty<Vector2>();
        private MotionGestureEvent latestMotionGesture = MotionGestureEvent.None;
        private int frameVersion;
        private float lastSampleTime = -999f;

        public string StatusText => statusText;

        public override GestureSnapshot CurrentSnapshot => snapshot;
        public override MotionGestureEvent CurrentMotionGesture
        {
            get
            {
                if (latestMotionGesture.IsValid && Time.time - latestMotionGesture.TriggeredTime > motionEventTimeout)
                {
                    latestMotionGesture = MotionGestureEvent.None;
                }

                return latestMotionGesture;
            }
        }

        public WebcamFeedController WebcamFeed => webcamFeed;
        public System.Collections.Generic.IReadOnlyList<Vector2> HandLandmarks => handLandmarks;
        public bool HasHandLandmarks => handLandmarks != null && handLandmarks.Length > 0;
        public int FrameVersion => frameVersion;
        public float LastSampleTime => lastSampleTime;

        public void Configure(WebcamFeedController feed)
        {
            webcamFeed = feed;
        }

        public void SetStatusText(string value)
        {
            statusText = value;
        }

        public void SetSnapshot(bool handPresent, GestureType gesture, Vector2 viewportPosition, float confidence)
        {
            if (mirrorX)
            {
                viewportPosition.x = 1f - viewportPosition.x;
            }
            if (mirrorY)
            {
                viewportPosition.y = 1f - viewportPosition.y;
            }

            snapshot = new GestureSnapshot
            {
                HandPresent = handPresent,
                Gesture = handPresent ? gesture : GestureType.None,
                ViewportPosition = new Vector2(Mathf.Clamp01(viewportPosition.x), Mathf.Clamp01(viewportPosition.y)),
                Confidence = Mathf.Clamp01(confidence)
            };

            if (!handPresent)
            {
                ClearHandLandmarks();
                latestMotionGesture = MotionGestureEvent.None;
            }

            frameVersion += 1;
            lastSampleTime = Time.time;
        }

        public void SetHandLandmarks(Vector2[] normalizedLandmarks)
        {
            if (normalizedLandmarks == null || normalizedLandmarks.Length == 0)
            {
                ClearHandLandmarks();
                return;
            }

            handLandmarks = new Vector2[normalizedLandmarks.Length];
            for (var index = 0; index < normalizedLandmarks.Length; index++)
            {
                var point = normalizedLandmarks[index];
                if (mirrorX)
                {
                    point.x = 1f - point.x;
                }
                if (mirrorY)
                {
                    point.y = 1f - point.y;
                }

                handLandmarks[index] = new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
            }
        }

        public void ClearHandLandmarks()
        {
            handLandmarks = System.Array.Empty<Vector2>();
        }

        public void PushMotionGesture(MotionGestureType gesture, Vector2 viewportPosition, float confidence)
        {
            latestMotionGesture = new MotionGestureEvent
            {
                Gesture = gesture,
                ViewportPosition = new Vector2(Mathf.Clamp01(viewportPosition.x), Mathf.Clamp01(viewportPosition.y)),
                Confidence = Mathf.Clamp01(confidence),
                TriggeredTime = Time.time
            };
        }

        public void ClearMotionGesture()
        {
            latestMotionGesture = MotionGestureEvent.None;
        }

        private void Update() { }
    }
}

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
        private GestureFrame currentGestureFrame = GestureFrame.Empty(GestureSourceKind.NativeMediapipe);
        private string statusText = "原生识别未启动";
        private Vector2[] handLandmarks = System.Array.Empty<Vector2>();
        private GestureHandedness primaryHandedness = GestureHandedness.Unknown;
        private int primaryTrackId;
        private MotionGestureEvent latestMotionGesture = MotionGestureEvent.None;
        private readonly GestureCommandHistory commandHistory = new GestureCommandHistory();
        private int frameVersion;
        private float lastSampleTime = -999f;

        public string StatusText => statusText;

        public override GestureSnapshot CurrentSnapshot => snapshot;
        public override GestureFrame CurrentGestureFrame => currentGestureFrame;
        public override GestureCommand CurrentGestureCommand
        {
            get
            {
                var command = ChooseGestureCommand(CurrentSnapshot, CurrentMotionGesture);
                commandHistory.Record(command);
                return command;
            }
        }

        public override GestureCommand[] RecentGestureCommands => commandHistory.Snapshot();
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

        public void SetPrimaryHandMetadata(int trackId, GestureHandedness handedness)
        {
            primaryTrackId = Mathf.Max(0, trackId);
            primaryHandedness = handedness;
            RefreshGestureFrame();
        }

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
                commandHistory.Clear();
            }

            frameVersion += 1;
            lastSampleTime = Time.time;
            RefreshGestureFrame();
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

            RefreshGestureFrame();
        }

        public void ClearHandLandmarks()
        {
            handLandmarks = System.Array.Empty<Vector2>();
            RefreshGestureFrame();
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
            RefreshGestureFrame();
        }

        public void ClearMotionGesture()
        {
            latestMotionGesture = MotionGestureEvent.None;
            RefreshGestureFrame();
        }

        private void RefreshGestureFrame()
        {
            currentGestureFrame = LegacyGestureRuntimeAdapter.BuildSingleHandFrame(
                snapshot,
                handLandmarks,
                frameVersion,
                lastSampleTime > 0f ? lastSampleTime : Time.time,
                GestureSourceKind.NativeMediapipe,
                latestMotionGesture,
                primaryHandedness,
                primaryTrackId);
        }

        private void Update() { }
    }
}

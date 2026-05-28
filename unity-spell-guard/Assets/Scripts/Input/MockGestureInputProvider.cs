using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class MockGestureInputProvider : GestureInputProviderBase
    {
        [SerializeField] private bool handPresent = true;
        [SerializeField] private GestureType gesture = GestureType.Point;
        [SerializeField] private Vector2 viewportPosition = new Vector2(0.5f, 0.7f);
        [SerializeField] private float moveSpeed = 0.65f;
        [SerializeField] private float motionEventTimeout = 0.7f;
        private readonly GestureCommandHistory commandHistory = new GestureCommandHistory();
        private MotionGestureEvent latestMotionGesture = MotionGestureEvent.None;
        private GestureCommand currentGestureCommand = GestureCommand.None;

        public override GestureSnapshot CurrentSnapshot => new GestureSnapshot
        {
            HandPresent = handPresent,
            Gesture = handPresent ? gesture : GestureType.None,
            ViewportPosition = viewportPosition,
            Confidence = handPresent ? 1f : 0f
        };

        public override MotionGestureEvent CurrentMotionGesture
        {
            get
            {
                RefreshMotionTimeout();
                return latestMotionGesture;
            }
        }

        public override GestureFrame CurrentGestureFrame => GestureFrameAdapter.BuildSingleHandFrame(
            CurrentSnapshot,
            null,
            0,
            Time.time,
            GestureSourceKind.Mock,
            CurrentMotionGesture);

        public override GestureCommand CurrentGestureCommand
        {
            get
            {
                RefreshMotionTimeout();
                return currentGestureCommand;
            }
        }

        public override GestureCommand[] RecentGestureCommands => commandHistory.Snapshot();

        private void Awake()
        {
            RefreshCurrentCommand(true);
        }

        private void Update()
        {
            var previousHandPresent = handPresent;
            var previousGesture = gesture;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                handPresent = !handPresent;
            }

            if (Input.GetKeyDown(KeyCode.Alpha0)) gesture = GestureType.None;
            if (Input.GetKeyDown(KeyCode.Alpha1)) gesture = GestureType.Point;
            if (Input.GetKeyDown(KeyCode.Alpha2)) gesture = GestureType.Fist;
            if (Input.GetKeyDown(KeyCode.Alpha3)) gesture = GestureType.VSign;
            if (Input.GetKeyDown(KeyCode.Alpha4)) gesture = GestureType.OpenPalm;

            if (Input.GetKeyDown(KeyCode.LeftArrow)) PushMotionGesture(MotionGestureType.SwipeRightToLeft);
            if (Input.GetKeyDown(KeyCode.RightArrow)) PushMotionGesture(MotionGestureType.SwipeLeftToRight);
            if (Input.GetKeyDown(KeyCode.UpArrow)) PushMotionGesture(MotionGestureType.SwipeBottomToTop);
            if (Input.GetKeyDown(KeyCode.DownArrow)) PushMotionGesture(MotionGestureType.SwipeTopToBottom);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PushMotionGesture(MotionGestureType.PointToFist);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) PushMotionGesture(MotionGestureType.OpenPalmSlapRightToLeft);
            if (Input.GetKeyDown(KeyCode.RightBracket)) PushMotionGesture(MotionGestureType.OpenPalmSlapLeftToRight);

            var speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? 2f : 1f);
            var delta = Vector2.zero;

            if (Input.GetKey(KeyCode.J)) delta.x -= speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.L)) delta.x += speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.I)) delta.y += speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.K)) delta.y -= speed * Time.deltaTime;

            viewportPosition += delta;
            viewportPosition.x = Mathf.Clamp01(viewportPosition.x);
            viewportPosition.y = Mathf.Clamp01(viewportPosition.y);

            if (previousHandPresent != handPresent || previousGesture != gesture)
            {
                RefreshCurrentCommand(true);
            }
            else
            {
                RefreshMotionTimeout();
            }
        }

        public override void ClearTransientInputs()
        {
            latestMotionGesture = MotionGestureEvent.None;
            RefreshCurrentCommand(false);
        }

        private void PushMotionGesture(MotionGestureType motionGesture)
        {
            latestMotionGesture = new MotionGestureEvent
            {
                Gesture = motionGesture,
                ViewportPosition = viewportPosition,
                Confidence = handPresent ? 1f : 0f,
                TriggeredTime = Time.time
            };

            RefreshCurrentCommand(true);
        }

        private void RefreshMotionTimeout()
        {
            if (latestMotionGesture.IsValid && Time.time - latestMotionGesture.TriggeredTime > motionEventTimeout)
            {
                latestMotionGesture = MotionGestureEvent.None;
                RefreshCurrentCommand(false);
            }
        }

        private void RefreshCurrentCommand(bool record)
        {
            currentGestureCommand = ChooseGestureCommand(CurrentSnapshot, latestMotionGesture);
            if (record)
            {
                commandHistory.Record(currentGestureCommand);
            }
        }
    }
}

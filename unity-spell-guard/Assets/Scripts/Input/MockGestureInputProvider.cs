using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class MockGestureInputProvider : GestureInputProviderBase
    {
        [SerializeField] private bool handPresent = true;
        [SerializeField] private GestureType gesture = GestureType.Point;
        [SerializeField] private Vector2 viewportPosition = new Vector2(0.5f, 0.7f);
        [SerializeField] private float moveSpeed = 0.65f;
        private readonly GestureCommandHistory commandHistory = new GestureCommandHistory();
        private GestureCommand currentGestureCommand = GestureCommand.None;

        public override GestureSnapshot CurrentSnapshot => new GestureSnapshot
        {
            HandPresent = handPresent,
            Gesture = handPresent ? gesture : GestureType.None,
            ViewportPosition = viewportPosition,
            Confidence = handPresent ? 1f : 0f
        };

        public override GestureFrame CurrentGestureFrame => LegacyGestureRuntimeAdapter.BuildSingleHandFrame(
            CurrentSnapshot,
            null,
            0,
            Time.time,
            GestureSourceKind.Mock,
            MotionGestureEvent.None);

        public override GestureCommand CurrentGestureCommand
        {
            get
            {
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
        }

        private void RefreshCurrentCommand(bool record)
        {
            currentGestureCommand = ChooseGestureCommand(CurrentSnapshot, MotionGestureEvent.None);
            if (record)
            {
                commandHistory.Record(currentGestureCommand);
            }
        }
    }
}

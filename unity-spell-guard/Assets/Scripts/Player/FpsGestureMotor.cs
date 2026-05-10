using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FpsGestureMotor : MonoBehaviour
    {
        public enum DiscreteMoveDirection
        {
            None,
            Forward,
            Backward,
            Left,
            Right
        }

        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float maxYawSpeed = 140f;
        [SerializeField] private float maxPitchSpeed = 90f;
        [SerializeField] private float turnDeadZone = 0.08f;
        [SerializeField] private float moveStepDistance = 1.5f;
        [SerializeField] private float moveStepDuration = 0.18f;
        [SerializeField] private float moveInputCooldown = 0.18f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float minPitch = -45f;
        [SerializeField] private float maxPitch = 55f;
        private CharacterController characterController;
        private float verticalVelocity;
        private float pitch;
        private bool inputEnabled = true;
        private GestureFrame currentGestureFrame;
        private float lastMoveTriggerTime = -999f;
        private Vector3 stepStartPosition;
        private Vector3 stepTargetPosition;
        private float stepStartedAt;
        private bool stepInProgress;
        private float lastHandledMotionTime = -999f;
        private DiscreteMoveDirection currentStepDirection = DiscreteMoveDirection.None;

        public GestureSnapshot Snapshot { get; private set; }
        public bool IsMovingForward { get; private set; }
        public bool IsStepInProgress => stepInProgress;
        public DiscreteMoveDirection CurrentStepDirection => currentStepDirection;
        public GestureFrame CurrentGestureFrame => currentGestureFrame;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            if (!value)
            {
                IsMovingForward = false;
                stepInProgress = false;
                currentStepDirection = DiscreteMoveDirection.None;
            }
        }

        private void Update()
        {
            currentGestureFrame = inputProvider != null ? inputProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            var activeHand = currentGestureFrame.PrimaryHand;
            Snapshot = activeHand.IsTracked
                ? new GestureSnapshot
                {
                    HandPresent = true,
                    Gesture = activeHand.StaticGesture,
                    ViewportPosition = activeHand.ViewportPosition,
                    Confidence = activeHand.Confidence
                }
                : GestureSnapshot.Missing;

            var moveVector = Vector3.zero;
            IsMovingForward = stepInProgress && currentStepDirection == DiscreteMoveDirection.Forward;

            if (inputEnabled && activeHand.IsTracked && activeHand.StaticGesture == GestureType.Point)
            {
                var offset = activeHand.ViewportPosition - new Vector2(0.5f, 0.5f);
                var yawInput = ApplyDeadZone(offset.x, turnDeadZone);
                var pitchInput = ApplyDeadZone(offset.y, turnDeadZone);

                transform.Rotate(0f, yawInput * maxYawSpeed * Time.deltaTime, 0f);

                pitch = Mathf.Clamp(pitch - pitchInput * maxPitchSpeed * Time.deltaTime, minPitch, maxPitch);
                if (cameraPivot != null)
                {
                    cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }
            }

            if (inputEnabled)
            {
                HandleDiscreteMovement(activeHand, currentGestureFrame.LatestMotion);
            }

            if (stepInProgress)
            {
                var duration = Mathf.Max(0.01f, moveStepDuration);
                var progress = Mathf.Clamp01((Time.time - stepStartedAt) / duration);
                var nextPosition = Vector3.Lerp(stepStartPosition, stepTargetPosition, progress);
                var delta = nextPosition - transform.position;
                moveVector += new Vector3(delta.x, 0f, delta.z) / Mathf.Max(Time.deltaTime, 0.0001f);
                IsMovingForward = true;
                if (progress >= 1f)
                {
                    stepInProgress = false;
                    currentStepDirection = DiscreteMoveDirection.None;
                }
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            moveVector.y = verticalVelocity;

            characterController.Move(moveVector * Time.deltaTime);

        }

        private void HandleDiscreteMovement(TrackedHandState activeHand, MotionGestureEvent motion)
        {
            if (stepInProgress || Time.time - lastMoveTriggerTime < moveInputCooldown)
            {
                return;
            }

            if (motion.IsValid && motion.TriggeredTime > lastHandledMotionTime)
            {
                switch (motion.Gesture)
                {
                    case MotionGestureType.SwipeRightToLeft:
                    case MotionGestureType.OpenPalmSlapRightToLeft:
                    case MotionGestureType.BodyShiftLeft:
                        lastHandledMotionTime = motion.TriggeredTime;
                        BeginStep(-transform.right);
                        return;

                    case MotionGestureType.SwipeLeftToRight:
                    case MotionGestureType.OpenPalmSlapLeftToRight:
                    case MotionGestureType.BodyShiftRight:
                        lastHandledMotionTime = motion.TriggeredTime;
                        BeginStep(transform.right);
                        return;

                    case MotionGestureType.SwipeBottomToTop:
                        lastHandledMotionTime = motion.TriggeredTime;
                        BeginStep(transform.forward);
                        return;

                    case MotionGestureType.SwipeTopToBottom:
                        lastHandledMotionTime = motion.TriggeredTime;
                        BeginStep(-transform.forward);
                        return;
                }
            }
        }

        private void BeginStep(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            direction.Normalize();
            stepStartPosition = transform.position;
            stepTargetPosition = transform.position + direction * moveStepDistance;
            stepStartedAt = Time.time;
            lastMoveTriggerTime = Time.time;
            stepInProgress = true;
            currentStepDirection = ResolveDirection(direction);
        }

        private DiscreteMoveDirection ResolveDirection(Vector3 direction)
        {
            if (Vector3.Dot(direction, transform.forward) > 0.9f)
            {
                return DiscreteMoveDirection.Forward;
            }

            if (Vector3.Dot(direction, -transform.forward) > 0.9f)
            {
                return DiscreteMoveDirection.Backward;
            }

            if (Vector3.Dot(direction, transform.right) > 0.9f)
            {
                return DiscreteMoveDirection.Right;
            }

            if (Vector3.Dot(direction, -transform.right) > 0.9f)
            {
                return DiscreteMoveDirection.Left;
            }

            return DiscreteMoveDirection.None;
        }

        private static float ApplyDeadZone(float value, float deadZone)
        {
            if (Mathf.Abs(value) <= deadZone)
            {
                return 0f;
            }

            var sign = Mathf.Sign(value);
            return sign * Mathf.InverseLerp(deadZone, 0.5f, Mathf.Abs(value));
        }
    }
}

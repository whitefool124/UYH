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
        [SerializeField] private float moveStepDistance = 1.5f;
        [SerializeField] private float moveStepDuration = 0.18f;
        [SerializeField] private float moveInputCooldown = 0.18f;
        [SerializeField] private float staticMoveHoldSeconds = 0.25f;
        [SerializeField] private float gravity = -18f;
        private CharacterController characterController;
        private float verticalVelocity;
        private bool inputEnabled = true;
        private GestureFrame currentGestureFrame;
        private float lastMoveTriggerTime = -999f;
        private Vector3 stepStartPosition;
        private Vector3 stepTargetPosition;
        private float stepStartedAt;
        private bool stepInProgress;
        private float lastHandledMotionTime = -999f;
        private DiscreteMoveDirection currentStepDirection = DiscreteMoveDirection.None;
        private GestureType heldMoveGesture = GestureType.None;
        private float heldMoveStartedAt = -999f;
        private bool heldMoveConsumed;

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
                ResetStaticMoveHold();
            }
        }

        private void Update()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

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

            if (inputEnabled)
            {
                HandleDiscreteMovement(currentGestureFrame);
            }

            if (stepInProgress)
            {
                var duration = Mathf.Max(0.01f, moveStepDuration);
                var progress = Mathf.Clamp01((Time.time - stepStartedAt) / duration);
                var nextPosition = Vector3.Lerp(stepStartPosition, stepTargetPosition, progress);
                var delta = nextPosition - transform.position;
                moveVector += new Vector3(delta.x, 0f, delta.z) / Mathf.Max(Time.deltaTime, 0.0001f);
                IsMovingForward = currentStepDirection == DiscreteMoveDirection.Forward;
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

        private void HandleDiscreteMovement(GestureFrame frame)
        {
            if (stepInProgress || Time.time - lastMoveTriggerTime < moveInputCooldown)
            {
                return;
            }

            if (TryHandleStaticMoveGesture(frame.PrimaryHand))
            {
                return;
            }

            var motion = frame.LatestMotion;
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

        private bool TryHandleStaticMoveGesture(TrackedHandState primaryHand)
        {
            if (!primaryHand.IsTracked ||
                (primaryHand.StaticGesture != GestureType.Point && primaryHand.StaticGesture != GestureType.OpenPalm))
            {
                ResetStaticMoveHold();
                return false;
            }

            if (heldMoveGesture != primaryHand.StaticGesture)
            {
                heldMoveGesture = primaryHand.StaticGesture;
                heldMoveStartedAt = Time.time;
                heldMoveConsumed = false;
            }

            if (heldMoveConsumed || Time.time - heldMoveStartedAt < Mathf.Max(0f, staticMoveHoldSeconds))
            {
                return false;
            }

            heldMoveConsumed = true;
            BeginStep(primaryHand.StaticGesture == GestureType.Point ? transform.forward : -transform.forward);
            return true;
        }

        private void ResetStaticMoveHold()
        {
            heldMoveGesture = GestureType.None;
            heldMoveStartedAt = -999f;
            heldMoveConsumed = false;
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
    }
}

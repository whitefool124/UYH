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
        [SerializeField] private float horizontalMoveCooldown = 2f;
        [SerializeField] private float verticalMoveCooldown = 2f;
        [SerializeField] private float staticMoveHoldSeconds = 0.25f;
        [SerializeField] private bool keyboardFallbackEnabled = true;
        [SerializeField] private float gravity = -18f;
        private CharacterController characterController;
        private float verticalVelocity;
        private bool inputEnabled = true;
        private GestureFrame currentGestureFrame;
        private float lastMoveTriggerTime = -999f;
        private float lastHorizontalMoveTriggerTime = -999f;
        private float lastVerticalMoveTriggerTime = -999f;
        private Vector3 stepStartPosition;
        private Vector3 stepTargetPosition;
        private float stepStartedAt;
        private bool stepInProgress;
        private float lastHandledMotionTime = -999f;
        private DiscreteMoveDirection currentStepDirection = DiscreteMoveDirection.None;
        private GestureIntent heldMoveIntent = GestureIntent.None;
        private float heldMoveStartedAt = -999f;
        private bool heldMoveConsumed;

        public GestureSnapshot Snapshot { get; private set; }
        public bool IsMovingForward { get; private set; }
        public bool IsStepInProgress => stepInProgress;
        public DiscreteMoveDirection CurrentStepDirection => currentStepDirection;
        public GestureFrame CurrentGestureFrame => currentGestureFrame;
        public float HorizontalMoveCooldownProgress => GetCooldownProgress(lastHorizontalMoveTriggerTime, horizontalMoveCooldown);
        public float VerticalMoveCooldownProgress => GetCooldownProgress(lastVerticalMoveTriggerTime, verticalMoveCooldown);
        public float MoveInputCooldownProgress => GetCooldownProgress(lastMoveTriggerTime, moveInputCooldown);
        public float HorizontalMoveCooldownRemaining => GetCooldownRemaining(lastHorizontalMoveTriggerTime, horizontalMoveCooldown);
        public float VerticalMoveCooldownRemaining => GetCooldownRemaining(lastVerticalMoveTriggerTime, verticalMoveCooldown);

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

            if (keyboardFallbackEnabled && TryHandleKeyboardMovement())
            {
                return;
            }

            var action = GestureIntentMapper.ToMovementAction(frame);
            if (TryHandleStaticMoveAction(action))
            {
                return;
            }

            if (action.IsValid && action.IsTransient && action.TriggeredTime > lastHandledMotionTime)
            {
                lastHandledMotionTime = action.TriggeredTime;
                switch (action.Intent)
                {
                    case GestureIntent.MoveLeft:
                        if (!CanTriggerHorizontalMove())
                        {
                            return;
                        }
                        BeginStep(-transform.right);
                        return;

                    case GestureIntent.MoveRight:
                        if (!CanTriggerHorizontalMove())
                        {
                            return;
                        }
                        BeginStep(transform.right);
                        return;

                    case GestureIntent.MoveForward:
                        if (!CanTriggerVerticalMove())
                        {
                            return;
                        }
                        BeginStep(transform.forward);
                        return;

                    case GestureIntent.MoveBackward:
                        if (!CanTriggerVerticalMove())
                        {
                            return;
                        }
                        BeginStep(-transform.forward);
                        return;
                }
            }
        }

        private bool TryHandleKeyboardMovement()
        {
            if ((Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) && CanTriggerHorizontalMove())
            {
                BeginStep(-transform.right);
                return true;
            }

            if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) && CanTriggerHorizontalMove())
            {
                BeginStep(transform.right);
                return true;
            }

            if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && CanTriggerVerticalMove())
            {
                BeginStep(transform.forward);
                return true;
            }

            if ((Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) && CanTriggerVerticalMove())
            {
                BeginStep(-transform.forward);
                return true;
            }

            return false;
        }

        private bool TryHandleStaticMoveAction(GestureAction action)
        {
            if (!action.IsValid || action.IsTransient || action.Intent != GestureIntent.MoveBackward)
            {
                ResetStaticMoveHold();
                return false;
            }

            if (heldMoveIntent != action.Intent)
            {
                heldMoveIntent = action.Intent;
                heldMoveStartedAt = Time.time;
                heldMoveConsumed = false;
            }

            if (heldMoveConsumed || Time.time - heldMoveStartedAt < Mathf.Max(0f, staticMoveHoldSeconds))
            {
                return false;
            }

            if (!CanTriggerVerticalMove())
            {
                heldMoveConsumed = true;
                return false;
            }

            heldMoveConsumed = true;
            BeginStep(-transform.forward);
            return true;
        }

        private void ResetStaticMoveHold()
        {
            heldMoveIntent = GestureIntent.None;
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
            if (IsHorizontalDirection(currentStepDirection))
            {
                lastHorizontalMoveTriggerTime = Time.time;
            }
            else if (IsVerticalDirection(currentStepDirection))
            {
                lastVerticalMoveTriggerTime = Time.time;
            }
        }

        private bool CanTriggerHorizontalMove()
        {
            return Time.time - lastHorizontalMoveTriggerTime >= Mathf.Max(0f, horizontalMoveCooldown);
        }

        private bool CanTriggerVerticalMove()
        {
            return Time.time - lastVerticalMoveTriggerTime >= Mathf.Max(0f, verticalMoveCooldown);
        }

        private static float GetCooldownProgress(float startedAt, float duration)
        {
            duration = Mathf.Max(0f, duration);
            if (duration <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01((Time.time - startedAt) / duration);
        }

        private static float GetCooldownRemaining(float startedAt, float duration)
        {
            duration = Mathf.Max(0f, duration);
            if (duration <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, duration - (Time.time - startedAt));
        }

        private static bool IsHorizontalDirection(DiscreteMoveDirection direction)
        {
            return direction == DiscreteMoveDirection.Left || direction == DiscreteMoveDirection.Right;
        }

        private static bool IsVerticalDirection(DiscreteMoveDirection direction)
        {
            return direction == DiscreteMoveDirection.Forward || direction == DiscreteMoveDirection.Backward;
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

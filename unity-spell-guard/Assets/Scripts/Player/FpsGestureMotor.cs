using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FpsGestureMotor : MonoBehaviour
    {
        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float maxYawSpeed = 140f;
        [SerializeField] private float maxPitchSpeed = 90f;
        [SerializeField] private float turnDeadZone = 0.08f;
        [SerializeField] private float forwardThreshold = 0.72f;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float minPitch = -45f;
        [SerializeField] private float maxPitch = 55f;
        private CharacterController characterController;
        private float verticalVelocity;
        private float pitch;
        private bool inputEnabled = true;

        public GestureSnapshot Snapshot { get; private set; }
        public bool IsMovingForward { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public void Configure(GestureInputProviderBase provider, Transform pivot)
        {
            inputProvider = provider;
            cameraPivot = pivot;
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            if (!value)
            {
                IsMovingForward = false;
            }
        }

        private void Update()
        {
            Snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;

            var moveVector = Vector3.zero;
            IsMovingForward = false;

            if (inputEnabled && Snapshot.HandPresent && Snapshot.Gesture == GestureType.Point)
            {
                var offset = Snapshot.ViewportPosition - new Vector2(0.5f, 0.5f);
                var yawInput = ApplyDeadZone(offset.x, turnDeadZone);
                var pitchInput = ApplyDeadZone(offset.y, turnDeadZone);

                transform.Rotate(0f, yawInput * maxYawSpeed * Time.deltaTime, 0f);

                pitch = Mathf.Clamp(pitch - pitchInput * maxPitchSpeed * Time.deltaTime, minPitch, maxPitch);
                if (cameraPivot != null)
                {
                    cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }

                if (Snapshot.ViewportPosition.y >= forwardThreshold)
                {
                    moveVector += transform.forward * moveSpeed;
                    IsMovingForward = true;
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

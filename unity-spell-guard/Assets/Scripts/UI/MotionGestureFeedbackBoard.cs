using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.UI
{
    public class MotionGestureFeedbackBoard : MonoBehaviour
    {
        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private Camera faceCamera;
        [SerializeField] private Renderer boardRenderer;
        [SerializeField] private TextMesh labelText;
        [SerializeField] private Color idleBoardColor = new Color(0.16f, 0.18f, 0.22f);
        [SerializeField] private Color snapBoardColor = new Color(0.16f, 0.42f, 0.58f);
        [SerializeField] private Color idleTextColor = Color.white;
        [SerializeField] private Color snapTextColor = new Color(0.82f, 0.97f, 1f);
        [SerializeField] private string idleMessage = "动态手势：等待中";
        [SerializeField] private string snapMessage = "Snap 已捕获";

        private Material runtimeMaterial;
        private string currentMessage;
        private bool currentIsSnap;

        public void Configure(GestureInputProviderBase provider, Camera camera)
        {
            inputProvider = provider;
            faceCamera = camera;
            EnsureReferences();
            RefreshFeedback(true);
        }

        private void Awake()
        {
            EnsureReferences();
            RefreshFeedback(true);
        }

        private void LateUpdate()
        {
            FaceCamera();
            RefreshFeedback(false);
        }

        private void EnsureReferences()
        {
            boardRenderer ??= GetComponent<Renderer>();
            labelText ??= GetComponentInChildren<TextMesh>(true);

            if (labelText != null && labelText.font == null)
            {
                labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (boardRenderer != null && runtimeMaterial == null)
            {
                runtimeMaterial = boardRenderer.material;
            }
        }

        private void FaceCamera()
        {
            if (faceCamera == null)
            {
                return;
            }

            var direction = faceCamera.transform.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        private void RefreshFeedback(bool force)
        {
            var motionGesture = inputProvider != null ? inputProvider.CurrentMotionGesture : MotionGestureEvent.None;
            var isSnap = motionGesture.IsValid && motionGesture.Gesture == MotionGestureType.Snap;
            var message = motionGesture.IsValid
                ? $"动态手势：{motionGesture.Gesture.ToChinese()}" + (isSnap ? $"\n{snapMessage}" : string.Empty)
                : idleMessage;

            if (!force && message == currentMessage && isSnap == currentIsSnap)
            {
                return;
            }

            currentMessage = message;
            currentIsSnap = isSnap;

            if (labelText != null)
            {
                labelText.text = message;
                labelText.color = isSnap ? snapTextColor : idleTextColor;
            }

            if (runtimeMaterial != null)
            {
                runtimeMaterial.color = isSnap ? snapBoardColor : idleBoardColor;
            }
        }
    }
}

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
        [SerializeField] private Color activeBoardColor = new Color(0.24f, 0.24f, 0.34f);
        [SerializeField] private Color idleTextColor = Color.white;
        [SerializeField] private Color snapTextColor = new Color(0.82f, 0.97f, 1f);
        [SerializeField] private Color activeTextColor = new Color(1f, 0.9f, 0.62f);
        [SerializeField] private string idleMessage = "SPELL GUARD\n动态手势待命";
        [SerializeField] private string snapMessage = "Snap 已捕获 · 立即施放";

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
                labelText.anchor = TextAnchor.MiddleCenter;
                labelText.alignment = TextAlignment.Center;
                labelText.characterSize = 0.048f;
                labelText.fontSize = 62;
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
            var isActive = motionGesture.IsValid;
            var message = motionGesture.IsValid
                ? $"SPELL GUARD\n{motionGesture.Gesture.ToChinese()}" + (isSnap ? $"\n{snapMessage}" : "\n动态施法已确认")
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
                labelText.color = isSnap ? snapTextColor : (isActive ? activeTextColor : idleTextColor);
                labelText.fontSize = isSnap ? 66 : (isActive ? 62 : 58);
                labelText.characterSize = isSnap ? 0.05f : 0.048f;
            }

            if (runtimeMaterial != null)
            {
                runtimeMaterial.color = isSnap ? snapBoardColor : (isActive ? activeBoardColor : idleBoardColor);
            }

            if (boardRenderer != null)
            {
                boardRenderer.transform.localScale = isSnap ? new Vector3(2.98f, 1.16f, 1f) : (isActive ? new Vector3(2.94f, 1.13f, 1f) : new Vector3(2.9f, 1.1f, 1f));
            }
        }
    }
}

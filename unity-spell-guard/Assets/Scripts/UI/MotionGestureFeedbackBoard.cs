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
        [SerializeField] private bool visible = false;

        public void Configure(GestureInputProviderBase provider, Camera camera)
        {
            inputProvider = provider;
            faceCamera = camera;
            SetVisible(false);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (boardRenderer != null)
            {
                boardRenderer.enabled = value;
            }

            if (labelText != null)
            {
                labelText.gameObject.SetActive(value);
            }
        }

        private void Awake()
        {
            EnsureReferences();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!visible)
            {
                return;
            }

            FaceCamera();
        }

        private void EnsureReferences()
        {
            boardRenderer ??= GetComponent<Renderer>();
            labelText ??= GetComponentInChildren<TextMesh>(true);
            SetVisible(false);
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
    }
}

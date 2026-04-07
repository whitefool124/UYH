using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class MockGestureInputProvider : GestureInputProviderBase
    {
        [SerializeField] private bool handPresent = true;
        [SerializeField] private GestureType gesture = GestureType.Point;
        [SerializeField] private Vector2 viewportPosition = new Vector2(0.5f, 0.7f);
        [SerializeField] private float moveSpeed = 0.65f;

        public override GestureSnapshot CurrentSnapshot => new GestureSnapshot
        {
            HandPresent = handPresent,
            Gesture = handPresent ? gesture : GestureType.None,
            ViewportPosition = viewportPosition,
            Confidence = handPresent ? 1f : 0f
        };

        private void Update()
        {
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
        }
    }
}

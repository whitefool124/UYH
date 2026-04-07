using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class ExternalGestureBridgeProvider : GestureInputProviderBase
    {
        [SerializeField] private float snapshotTimeout = 0.25f;
        [SerializeField] private bool clearWhenTimedOut = true;

        private GestureSnapshot snapshot = GestureSnapshot.Missing;
        private float lastPushTime = -999f;

        public override GestureSnapshot CurrentSnapshot
        {
            get
            {
                if (clearWhenTimedOut && Time.time - lastPushTime > snapshotTimeout)
                {
                    snapshot = GestureSnapshot.Missing;
                }

                return snapshot;
            }
        }

        public string BridgeStatus => snapshot.HandPresent ? $"外部识别：{snapshot.Gesture.ToChinese()}" : "等待外部识别数据";

        public void PushSnapshot(bool handPresent, GestureType gesture, Vector2 viewportPosition, float confidence)
        {
            snapshot = new GestureSnapshot
            {
                HandPresent = handPresent,
                Gesture = handPresent ? gesture : GestureType.None,
                ViewportPosition = new Vector2(Mathf.Clamp01(viewportPosition.x), Mathf.Clamp01(viewportPosition.y)),
                Confidence = Mathf.Clamp01(confidence)
            };

            lastPushTime = Time.time;
        }

        public void PushGesture(string gestureName, float x, float y, float confidence = 1f, bool handPresent = true)
        {
            PushSnapshot(handPresent, ParseGesture(gestureName), new Vector2(x, y), confidence);
        }

        public void ClearSnapshot()
        {
            snapshot = GestureSnapshot.Missing;
            lastPushTime = -999f;
        }

        private static GestureType ParseGesture(string gestureName)
        {
            if (string.IsNullOrWhiteSpace(gestureName))
            {
                return GestureType.None;
            }

            switch (gestureName.Trim().ToLowerInvariant())
            {
                case "point":
                case "pointer":
                    return GestureType.Point;
                case "fist":
                case "fire":
                    return GestureType.Fist;
                case "v":
                case "vsign":
                case "peace":
                case "ice":
                    return GestureType.VSign;
                case "openpalm":
                case "palm":
                case "shield":
                    return GestureType.OpenPalm;
                case "none":
                    return GestureType.None;
                default:
                    return GestureType.Unknown;
            }
        }
    }
}

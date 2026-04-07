using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class ExternalGestureBridgeProvider : GestureInputProviderBase
    {
        [SerializeField] private float snapshotTimeout = 0.25f;
        [SerializeField] private bool clearWhenTimedOut = true;
        [SerializeField] private float motionEventTimeout = 0.4f;

        private GestureSnapshot snapshot = GestureSnapshot.Missing;
        private ExternalVisionFrame currentFrame;
        private Vector2[] handLandmarks = Array.Empty<Vector2>();
        private Vector2[] poseLandmarks = Array.Empty<Vector2>();
        private MotionGestureEvent latestMotionGesture = MotionGestureEvent.None;
        private float lastPushTime = -999f;
        private int frameVersion;

        public override GestureSnapshot CurrentSnapshot
        {
            get
            {
                RefreshTimeoutState();

                return snapshot;
            }
        }

        public string BridgeStatus
        {
            get
            {
                RefreshTimeoutState();
                if (!snapshot.HandPresent)
                {
                    return "等待外部识别数据";
                }

                return latestMotionGesture.IsValid
                    ? $"外部识别：{snapshot.Gesture.ToChinese()} / 动态：{latestMotionGesture.Gesture.ToChinese()}"
                    : $"外部识别：{snapshot.Gesture.ToChinese()}";
            }
        }

        public ExternalVisionFrame CurrentFrame
        {
            get
            {
                RefreshTimeoutState();
                return currentFrame;
            }
        }

        public IReadOnlyList<Vector2> HandLandmarks => handLandmarks;
        public IReadOnlyList<Vector2> PoseLandmarks => poseLandmarks;
        public bool HasHandLandmarks => handLandmarks != null && handLandmarks.Length > 0;
        public MotionGestureEvent LatestMotionGesture
        {
            get
            {
                RefreshTimeoutState();
                return latestMotionGesture;
            }
        }

        public int FrameVersion => frameVersion;

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

        public void PushFrame(ExternalVisionFrame frame)
        {
            if (frame == null)
            {
                ClearSnapshot();
                return;
            }

            currentFrame = frame;
            frameVersion++;

            SetLandmarks(frame.handLandmarks, ref handLandmarks);
            SetLandmarks(frame.poseLandmarks, ref poseLandmarks);

            var viewportPosition = frame.ResolveViewportPosition();
            var confidence = frame.trackingConfidence > 0f ? frame.trackingConfidence : frame.confidence;
            PushSnapshot(frame.handPresent, ParseGesture(frame.gesture), viewportPosition, confidence);

            if (!frame.handPresent)
            {
                latestMotionGesture = MotionGestureEvent.None;
            }
        }

        public void PushGesture(string gestureName, float x, float y, float confidence = 1f, bool handPresent = true)
        {
            PushSnapshot(handPresent, ParseGesture(gestureName), new Vector2(x, y), confidence);
        }

        public void PushMotionGesture(MotionGestureType gesture, Vector2 viewportPosition, float confidence)
        {
            latestMotionGesture = new MotionGestureEvent
            {
                Gesture = gesture,
                ViewportPosition = new Vector2(Mathf.Clamp01(viewportPosition.x), Mathf.Clamp01(viewportPosition.y)),
                Confidence = Mathf.Clamp01(confidence),
                TriggeredTime = Time.time
            };
        }

        public void ClearSnapshot()
        {
            snapshot = GestureSnapshot.Missing;
            currentFrame = null;
            handLandmarks = Array.Empty<Vector2>();
            poseLandmarks = Array.Empty<Vector2>();
            latestMotionGesture = MotionGestureEvent.None;
            lastPushTime = -999f;
        }

        private void RefreshTimeoutState()
        {
            if (clearWhenTimedOut && Time.time - lastPushTime > snapshotTimeout)
            {
                snapshot = GestureSnapshot.Missing;
                currentFrame = null;
                handLandmarks = Array.Empty<Vector2>();
                poseLandmarks = Array.Empty<Vector2>();
            }

            if (latestMotionGesture.IsValid && Time.time - latestMotionGesture.TriggeredTime > motionEventTimeout)
            {
                latestMotionGesture = MotionGestureEvent.None;
            }
        }

        private static void SetLandmarks(ExternalVisionPoint[] source, ref Vector2[] destination)
        {
            if (source == null || source.Length == 0)
            {
                destination = Array.Empty<Vector2>();
                return;
            }

            destination = new Vector2[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                destination[index] = source[index].ToViewportPosition();
            }
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

    internal static class MotionGestureTypeExtensions
    {
        public static string ToChinese(this MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.SwipeLeftToRight:
                    return "左到右挥动";
                case MotionGestureType.SwipeRightToLeft:
                    return "右到左挥动";
                case MotionGestureType.Snap:
                    return "打响指";
                case MotionGestureType.BodyShiftLeft:
                    return "身体左移";
                case MotionGestureType.BodyShiftRight:
                    return "身体右移";
                default:
                    return "无";
            }
        }
    }
}

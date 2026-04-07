using System;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    [Serializable]
    public class ExternalVisionFrame
    {
        public bool handPresent;
        public string gesture;
        public float x = 0.5f;
        public float y = 0.5f;
        public float confidence;
        public float trackingConfidence;
        public float timestamp;
        public string source;
        public ExternalVisionPoint pointer;
        public ExternalVisionPoint[] handLandmarks;
        public ExternalVisionPoint[] poseLandmarks;

        public Vector2 ResolveViewportPosition()
        {
            if (pointer.visibility > 0f || pointer.x != 0f || pointer.y != 0f)
            {
                return pointer.ToViewportPosition();
            }

            if (handLandmarks != null && handLandmarks.Length > 8)
            {
                return handLandmarks[8].ToViewportPosition();
            }

            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }
    }
}

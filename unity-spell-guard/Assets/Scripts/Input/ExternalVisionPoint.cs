using System;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    [Serializable]
    public struct ExternalVisionPoint
    {
        public float x;
        public float y;
        public float z;
        public float visibility;

        public Vector2 ToViewportPosition()
        {
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }
    }
}

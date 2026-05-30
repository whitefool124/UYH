using System;
using System.Collections;
using UnityEngine;

namespace SpellGuard.UI.Canvas
{
    public static class UITransitions
    {
        public static IEnumerator FadeIn(CanvasGroup group, float duration)
        {
            if (duration <= 0f)
            {
                group.alpha = 1f;
                yield break;
            }

            var startAlpha = group.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(startAlpha, 1f, EaseOutCubic(elapsed / duration));
                yield return null;
            }
            group.alpha = 1f;
        }

        public static IEnumerator FadeOut(CanvasGroup group, float duration)
        {
            if (duration <= 0f)
            {
                group.alpha = 0f;
                yield break;
            }

            var startAlpha = group.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(startAlpha, 0f, EaseOutCubic(elapsed / duration));
                yield return null;
            }
            group.alpha = 0f;
        }

        public static IEnumerator SlideIn(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            if (duration <= 0f)
            {
                rect.anchoredPosition = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, EaseOutBack(elapsed / duration));
                yield return null;
            }
            rect.anchoredPosition = to;
        }

        public static IEnumerator SlideOut(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            if (duration <= 0f)
            {
                rect.anchoredPosition = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, EaseInCubic(elapsed / duration));
                yield return null;
            }
            rect.anchoredPosition = to;
        }

        public static IEnumerator ScalePulse(RectTransform rect, float targetScale, float duration)
        {
            var startScale = rect.localScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = elapsed / duration;
                var s = t < 0.5f
                    ? Mathf.Lerp(1f, targetScale, EaseOutCubic(t * 2f))
                    : Mathf.Lerp(targetScale, 1f, EaseInCubic((t - 0.5f) * 2f));
                rect.localScale = startScale * s;
                yield return null;
            }
            rect.localScale = startScale;
        }

        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInCubic(float t) => t * t * t;
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static IEnumerator ScaleIn(RectTransform rect, float duration)
        {
            if (duration <= 0f) { rect.localScale = Vector3.one; yield break; }
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var s = Mathf.Lerp(0.92f, 1f, EaseOutBack(elapsed / duration));
                rect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }
    }
}

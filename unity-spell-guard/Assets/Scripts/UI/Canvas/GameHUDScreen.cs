using System.Collections;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SpellGuard.UI.Canvas
{
    public class GameHUDScreen : UIScreen
    {
        [Header("HUD Elements")]
        [SerializeField] private Text screenLabelText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text gestureText;
        [SerializeField] private Text hintText;
        [SerializeField] private Image healthBarFill;
        [SerializeField] private RectTransform cooldownGroup;

        [Header("Style")]
        [SerializeField] private Color healthColor = new Color(0.35f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color healthLowColor = new Color(0.9f, 0.3f, 0.2f, 1f);
        [SerializeField] private Color hintColor = new Color(1f, 0.82f, 0.42f, 0.95f);
        [SerializeField] private RectTransform topBar;

        public override IEnumerator Open()
        {
            IsTransitioning = true;
            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Slide top bar down
            Vector2 topPos = topBar != null ? topBar.anchoredPosition : Vector2.zero;
            Vector2 topFrom = topPos + new Vector2(0f, 60f);
            if (topBar != null) topBar.anchoredPosition = topFrom;

            StartCoroutine(UITransitions.FadeIn(canvasGroup, openDuration));
            if (topBar != null) StartCoroutine(UITransitions.SlideIn(topBar, topFrom, topPos, openDuration));

            yield return new WaitForSecondsRealtime(openDuration);

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            IsOpen = true;
            IsTransitioning = false;
            InvokeOpened();
        }

        public override IEnumerator Close()
        {
            IsTransitioning = true;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            yield return StartCoroutine(UITransitions.FadeOut(canvasGroup, closeDuration));
            gameObject.SetActive(false);
            IsOpen = false;
            IsTransitioning = false;
            InvokeClosed();
        }

        public void SetScreenLabel(string label) { if (screenLabelText) screenLabelText.text = label; }
        public void SetScore(int score) { if (scoreText) scoreText.text = $"Score: {score}"; }
        public void SetHealth(int current, int max)
        {
            if (healthText) healthText.text = $"HP {current}/{max}";
            if (healthBarFill)
            {
                var ratio = max > 0 ? (float)current / max : 0f;
                healthBarFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                healthBarFill.color = ratio > 0.3f ? healthColor : healthLowColor;
            }
        }
        public void SetGesture(string gesture) { if (gestureText) gestureText.text = gesture; }
        public void SetHint(string hint) { if (hintText) hintText.text = hint; }
    }
}

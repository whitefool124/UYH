using System;
using System.Collections;
using UnityEngine;

namespace SpellGuard.UI.Canvas
{
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected float openDuration = 0.35f;
        [SerializeField] protected float closeDuration = 0.2f;

        public bool IsOpen { get; private set; }
        public bool IsTransitioning { get; private set; }
        public string ScreenId => gameObject.name;

        public event Action<UIScreen> Opened;
        public event Action<UIScreen> Closed;

        protected virtual void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public virtual IEnumerator Open()
        {
            IsTransitioning = true;
            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            yield return StartCoroutine(UITransitions.FadeIn(canvasGroup, openDuration));

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            IsOpen = true;
            IsTransitioning = false;
            Opened?.Invoke(this);
        }

        public virtual IEnumerator Close()
        {
            IsTransitioning = true;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            yield return StartCoroutine(UITransitions.FadeOut(canvasGroup, closeDuration));

            gameObject.SetActive(false);
            IsOpen = false;
            IsTransitioning = false;
            Closed?.Invoke(this);
        }

        public void SetVisibleImmediate(bool visible)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            StopAllCoroutines();
            IsTransitioning = false;
            IsOpen = visible;
            gameObject.SetActive(visible);
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}

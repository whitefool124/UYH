using System;
using UnityEngine;

namespace SpellGuard.UI.Canvas
{
    public abstract class UIScreen : MonoBehaviour
    {
        protected enum AnimState { None, Opening, Closing }

        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected float openDuration = 0.35f;
        [SerializeField] protected float closeDuration = 0.2f;

        public bool IsOpen { get; protected set; }
        public bool IsTransitioning { get; protected set; }
        public string ScreenId => gameObject.name;

        public event Action<UIScreen> Opened;
        public event Action<UIScreen> Closed;

        protected void InvokeOpened() => Opened?.Invoke(this);
        protected void InvokeClosed() => Closed?.Invoke(this);

        private AnimState _animState;
        private float _animElapsed;
        private float _animDuration;
        private float _closeStartAlpha;

        protected virtual void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public virtual void Open()
        {
            _animState = AnimState.Opening;
            _animDuration = openDuration;
            _animElapsed = 0f;
            IsTransitioning = true;
            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            OnOpenStart();
        }

        public virtual void Close()
        {
            _animState = AnimState.Closing;
            _animDuration = closeDuration;
            _animElapsed = 0f;
            _closeStartAlpha = canvasGroup.alpha;
            IsTransitioning = true;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            OnCloseStart();
        }

        public void SetVisibleImmediate(bool visible)
        {
            _animState = AnimState.None;
            StopAllCoroutines();
            IsTransitioning = false;
            IsOpen = visible;
            gameObject.SetActive(visible);
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        protected virtual void OnOpenStart() { }
        protected virtual void OnOpenUpdate(float t) { }
        protected virtual void OnOpenComplete() { }
        protected virtual void OnCloseStart() { }
        protected virtual void OnCloseUpdate(float t) { }
        protected virtual void OnCloseComplete() { }

        protected virtual void Update()
        {
            if (_animState == AnimState.None) return;

            _animElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_animElapsed / _animDuration);

            if (_animState == AnimState.Opening)
            {
                float eased = UITransitions.EaseOutCubic(t);
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, eased);
                OnOpenUpdate(t);

                if (t >= 1f)
                {
                    canvasGroup.alpha = 1f;
                    OnOpenComplete();
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    IsOpen = true;
                    IsTransitioning = false;
                    _animState = AnimState.None;
                    InvokeOpened();
                }
            }
            else // Closing
            {
                float eased = UITransitions.EaseOutCubic(t);
                canvasGroup.alpha = Mathf.Lerp(_closeStartAlpha, 0f, eased);
                OnCloseUpdate(t);

                if (t >= 1f)
                {
                    canvasGroup.alpha = 0f;
                    OnCloseComplete();
                    gameObject.SetActive(false);
                    IsOpen = false;
                    IsTransitioning = false;
                    _animState = AnimState.None;
                    InvokeClosed();
                }
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpellGuard.UI.Canvas
{
    public class SceneTransitionManager : MonoBehaviour
    {
        private enum State { Idle, FadeIn, WaitLoad, SceneReady, FadeOut }

        [SerializeField] private UnityEngine.Canvas canvas;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private CanvasGroup loadingGroup;
        [SerializeField] private Image loadingFillBar;
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private float minLoadDisplaySeconds = 0.5f;

        private static SceneTransitionManager instance;
        private State state;
        private float elapsed;
        private float loadStartTime;
        private string pendingScene;
        private AsyncOperation asyncOp;

        public static SceneTransitionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("SceneTransitionManager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SceneTransitionManager>();
                    instance.BuildDefaultUI();
                }
                return instance;
            }
        }

        public bool IsLoading => state != State.Idle;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            switch (state)
            {
                case State.FadeIn:
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    fadeGroup.alpha = Mathf.Lerp(0f, 1f, UITransitions.EaseOutCubic(t));
                    if (t >= 1f)
                    {
                        fadeGroup.alpha = 1f;
                        BeginLoad();
                    }
                    break;

                case State.WaitLoad:
                    if (asyncOp != null)
                    {
                        float fill = Mathf.Lerp(0.08f, 0.92f, Mathf.Clamp01(asyncOp.progress / 0.9f));
                        loadingFillBar.rectTransform.anchorMax = new Vector2(fill, 0.32f);

                        if (asyncOp.progress >= 0.9f)
                        {
                            loadingFillBar.rectTransform.anchorMax = new Vector2(0.92f, 0.32f);
                            float loadElapsed = Time.unscaledTime - loadStartTime;
                            if (loadElapsed >= minLoadDisplaySeconds)
                            {
                                asyncOp.allowSceneActivation = true;
                                state = State.SceneReady;
                            }
                        }
                    }
                    else
                    {
                        loadingGroup.alpha = 0f;
                        loadingGroup.blocksRaycasts = false;
                        state = State.FadeOut;
                        elapsed = 0f;
                    }
                    break;

                case State.SceneReady:
                    if (asyncOp == null || asyncOp.isDone)
                    {
                        loadingGroup.alpha = 0f;
                        loadingGroup.blocksRaycasts = false;
                        state = State.FadeOut;
                        elapsed = 0f;
                    }
                    break;

                case State.FadeOut:
                    elapsed += Time.unscaledDeltaTime;
                    float ft = Mathf.Clamp01(elapsed / fadeDuration);
                    fadeGroup.alpha = Mathf.Lerp(1f, 0f, UITransitions.EaseOutCubic(ft));
                    if (ft >= 1f)
                    {
                        fadeGroup.alpha = 0f;
                        Cleanup();
                    }
                    break;
            }
        }

        public void LoadScene(string sceneName)
        {
            if (state != State.Idle) return;

            pendingScene = sceneName;
            canvas.enabled = true;
            loadingFillBar.rectTransform.anchorMax = new Vector2(0.08f, 0.32f);

            state = State.FadeIn;
            elapsed = 0f;
            fadeGroup.alpha = 0f;
            loadingGroup.alpha = 0f;
            loadingGroup.blocksRaycasts = false;
        }

        private void BeginLoad()
        {
            loadingGroup.alpha = 1f;
            loadingGroup.blocksRaycasts = true;
            loadStartTime = Time.unscaledTime;

            asyncOp = SceneManager.LoadSceneAsync(pendingScene);
            if (asyncOp == null)
            {
                loadingGroup.alpha = 0f;
                loadingGroup.blocksRaycasts = false;
                state = State.FadeOut;
                elapsed = 0f;
                return;
            }

            asyncOp.allowSceneActivation = false;
            state = State.WaitLoad;
        }

        private void Cleanup()
        {
            canvas.enabled = false;
            state = State.Idle;
            asyncOp = null;
            pendingScene = null;
        }

        private void BuildDefaultUI()
        {
            canvas = gameObject.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            var fadeGo = new GameObject("FadeOverlay");
            fadeGo.transform.SetParent(transform, false);
            var fadeImage = fadeGo.AddComponent<Image>();
            fadeImage.color = new Color(0.02f, 0.025f, 0.05f, 1f);
            fadeImage.rectTransform.anchorMin = Vector2.zero;
            fadeImage.rectTransform.anchorMax = Vector2.one;
            fadeImage.rectTransform.sizeDelta = Vector2.zero;
            fadeGroup = fadeGo.AddComponent<CanvasGroup>();

            var loadGo = new GameObject("LoadingGroup");
            loadGo.transform.SetParent(transform, false);
            var loadRect = loadGo.AddComponent<RectTransform>();
            loadRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadRect.sizeDelta = new Vector2(400f, 100f);
            loadRect.anchoredPosition = Vector2.zero;
            loadingGroup = loadGo.AddComponent<CanvasGroup>();

            var bg = new GameObject("LoadingBg");
            bg.transform.SetParent(loadGo.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.05f, 0.1f, 0.92f);
            bgImage.rectTransform.anchorMin = Vector2.zero;
            bgImage.rectTransform.anchorMax = Vector2.one;
            bgImage.rectTransform.sizeDelta = Vector2.zero;

            var accent = new GameObject("Accent");
            accent.transform.SetParent(loadGo.transform, false);
            var accentImage = accent.AddComponent<Image>();
            accentImage.color = new Color(0.96f, 0.64f, 0.22f, 1f);
            var accentRect = accentImage.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0.95f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.sizeDelta = Vector2.zero;

            var fillBg = new GameObject("FillBg");
            fillBg.transform.SetParent(loadGo.transform, false);
            var fillBgImage = fillBg.AddComponent<Image>();
            fillBgImage.color = new Color(0.08f, 0.09f, 0.18f, 1f);
            var fillBgRect = fillBgImage.rectTransform;
            fillBgRect.anchorMin = new Vector2(0.08f, 0.12f);
            fillBgRect.anchorMax = new Vector2(0.92f, 0.32f);
            fillBgRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("FillBar");
            fill.transform.SetParent(loadGo.transform, false);
            loadingFillBar = fill.AddComponent<Image>();
            loadingFillBar.color = new Color(0.96f, 0.64f, 0.22f, 1f);
            var fillRect = loadingFillBar.rectTransform;
            fillRect.anchorMin = new Vector2(0.08f, 0.12f);
            fillRect.anchorMax = new Vector2(0.08f, 0.32f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = Vector2.zero;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(loadGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "LOADING...";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.85f, 0.9f, 0.98f, 1f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.4f);
            labelRect.anchorMax = new Vector2(1f, 0.9f);
            labelRect.sizeDelta = Vector2.zero;

            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            loadingGroup.alpha = 0f;
            loadingGroup.blocksRaycasts = false;

            canvas.enabled = false;
        }
    }
}

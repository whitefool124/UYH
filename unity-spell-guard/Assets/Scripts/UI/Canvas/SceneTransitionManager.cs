using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpellGuard.UI.Canvas
{
    public class SceneTransitionManager : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Canvas canvas;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private CanvasGroup loadingGroup;
        [SerializeField] private Image loadingFillBar;
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private float minLoadDisplaySeconds = 0.5f;

        private static SceneTransitionManager instance;

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

        public bool IsLoading { get; private set; }

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

        private void BuildDefaultUI()
        {
            canvas = gameObject.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
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
            loadRect.sizeDelta = new Vector2(320f, 80f);
            loadRect.anchoredPosition = Vector2.zero;
            loadingGroup = loadGo.AddComponent<CanvasGroup>();

            var bg = new GameObject("LoadingBg");
            bg.transform.SetParent(loadGo.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.05f, 0.1f, 0.92f);
            bgImage.rectTransform.anchorMin = Vector2.zero;
            bgImage.rectTransform.anchorMax = Vector2.one;
            bgImage.rectTransform.sizeDelta = Vector2.zero;

            var fillBg = new GameObject("FillBg");
            fillBg.transform.SetParent(loadGo.transform, false);
            var fillBgImage = fillBg.AddComponent<Image>();
            fillBgImage.color = new Color(0.08f, 0.09f, 0.18f, 1f);
            var fillBgRect = fillBgImage.rectTransform;
            fillBgRect.anchorMin = new Vector2(0.05f, 0.15f);
            fillBgRect.anchorMax = new Vector2(0.95f, 0.35f);
            fillBgRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("FillBar");
            fill.transform.SetParent(loadGo.transform, false);
            loadingFillBar = fill.AddComponent<Image>();
            loadingFillBar.color = new Color(0.95f, 0.62f, 0.24f, 1f);
            var fillRect = loadingFillBar.rectTransform;
            fillRect.anchorMin = new Vector2(0.05f, 0.15f);
            fillRect.anchorMax = new Vector2(0.05f, 0.35f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = Vector2.zero;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(loadGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "LOADING";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.85f, 0.9f, 0.98f, 1f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.9f);
            labelRect.sizeDelta = Vector2.zero;

            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            loadingGroup.alpha = 0f;
            loadingGroup.blocksRaycasts = false;

            canvas.enabled = false;
        }

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            canvas.enabled = true;
            loadingFillBar.rectTransform.anchorMax = new Vector2(0.05f, 0.35f);

            yield return StartCoroutine(UITransitions.FadeIn(fadeGroup, fadeDuration));

            loadingGroup.alpha = 1f;
            loadingGroup.blocksRaycasts = true;

            var loadStart = Time.unscaledTime;
            var asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                loadingGroup.alpha = 0f;
                yield return StartCoroutine(UITransitions.FadeOut(fadeGroup, fadeDuration));
                canvas.enabled = false;
                IsLoading = false;
                yield break;
            }

            asyncOp.allowSceneActivation = false;

            while (asyncOp.progress < 0.9f)
            {
                var fill = Mathf.Lerp(0.05f, 0.95f, asyncOp.progress / 0.9f);
                loadingFillBar.rectTransform.anchorMax = new Vector2(fill, 0.35f);
                yield return null;
            }

            loadingFillBar.rectTransform.anchorMax = new Vector2(0.95f, 0.35f);

            var elapsed = Time.unscaledTime - loadStart;
            if (elapsed < minLoadDisplaySeconds)
                yield return new WaitForSecondsRealtime(minLoadDisplaySeconds - elapsed);

            asyncOp.allowSceneActivation = true;
            while (!asyncOp.isDone)
                yield return null;

            loadingGroup.alpha = 0f;
            loadingGroup.blocksRaycasts = false;

            yield return StartCoroutine(UITransitions.FadeOut(fadeGroup, fadeDuration));

            canvas.enabled = false;
            IsLoading = false;
        }
    }
}

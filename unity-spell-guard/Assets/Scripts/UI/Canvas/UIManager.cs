using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellGuard.UI.Canvas
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private UIScreen[] screens;
        [SerializeField] private UIScreen defaultScreen;
        [SerializeField] private float transitionOutDuration = 0.2f;
        [SerializeField] private float transitionInDuration = 0.35f;

        private readonly Dictionary<Type, UIScreen> screenMap = new Dictionary<Type, UIScreen>();
        private UIScreen currentScreen;
        private bool isTransitioning;

        public UIScreen CurrentScreen => currentScreen;
        public bool IsTransitioning => isTransitioning;

        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var screen in screens)
            {
                if (screen == null) continue;
                screenMap[screen.GetType()] = screen;
                screen.gameObject.SetActive(false);
            }

            if (defaultScreen != null)
            {
                defaultScreen.SetVisibleImmediate(true);
                currentScreen = defaultScreen;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public T GetScreen<T>() where T : UIScreen
        {
            screenMap.TryGetValue(typeof(T), out var screen);
            return screen as T;
        }

        public void ShowScreen<T>() where T : UIScreen
        {
            var screen = GetScreen<T>();
            if (screen != null)
                ShowScreen(screen);
        }

        public void ShowScreen(UIScreen target)
        {
            if (isTransitioning || target == null || target == currentScreen)
                return;

            StartCoroutine(TransitionRoutine(target));
        }

        private IEnumerator TransitionRoutine(UIScreen target)
        {
            isTransitioning = true;

            if (currentScreen != null && currentScreen.IsOpen)
                yield return StartCoroutine(currentScreen.Close());

            currentScreen = target;
            yield return StartCoroutine(target.Open());

            isTransitioning = false;
        }

        public Coroutine ShowScreenDirect(UIScreen target)
        {
            if (target == null) return null;
            return StartCoroutine(TransitionRoutine(target));
        }

        public void RebuildScreenMap()
        {
            screenMap.Clear();
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                screenMap[screen.GetType()] = screen;
            }
            if (defaultScreen != null && !defaultScreen.IsOpen)
            {
                defaultScreen.SetVisibleImmediate(true);
                currentScreen = defaultScreen;
            }
        }

        public void SetDefaultScreen(UIScreen screen)
        {
            defaultScreen = screen;
        }

        public void HideAll()
        {
            StopAllCoroutines();
            isTransitioning = false;
            foreach (var screen in screens)
            {
                if (screen != null)
                    screen.SetVisibleImmediate(false);
            }
            currentScreen = null;
        }
    }
}

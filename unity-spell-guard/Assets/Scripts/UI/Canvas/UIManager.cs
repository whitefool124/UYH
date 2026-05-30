using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.UI.Canvas
{
    public class UIManager : MonoBehaviour
    {
        private enum TransState { None, ClosingCurrent, OpeningTarget }

        [SerializeField] private UIScreen[] screens;
        [SerializeField] private UIScreen defaultScreen;
        [SerializeField] private float transitionOutDuration = 0.2f;
        [SerializeField] private float transitionInDuration = 0.35f;

        private readonly Dictionary<Type, UIScreen> screenMap = new Dictionary<Type, UIScreen>();
        private readonly Dictionary<string, UIScreen> screenNameMap = new Dictionary<string, UIScreen>();
        private readonly List<GameCanvasBridge> bridges = new List<GameCanvasBridge>();
        private UIScreen currentScreen;
        private TransState transState;
        private UIScreen pendingTarget;

        public UIScreen CurrentScreen => currentScreen;
        public bool IsTransitioning => transState != TransState.None;

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
                screenNameMap[screen.ScreenId] = screen;
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

        private void Update()
        {
            // Tick all registered bridges
            for (int i = 0; i < bridges.Count; i++)
                bridges[i].Tick();

            // Handle screen transitions
            if (transState == TransState.None) return;

            if (transState == TransState.ClosingCurrent)
            {
                if (!currentScreen.IsTransitioning)
                {
                    currentScreen = pendingTarget;
                    currentScreen.Open();
                    transState = TransState.OpeningTarget;
                }
            }
            else if (transState == TransState.OpeningTarget)
            {
                if (!currentScreen.IsTransitioning)
                {
                    transState = TransState.None;
                }
            }
        }

        public T GetScreen<T>() where T : UIScreen
        {
            screenMap.TryGetValue(typeof(T), out var screen);
            return screen as T;
        }

        public T GetScreen<T>(string screenId) where T : UIScreen
        {
            screenNameMap.TryGetValue(screenId, out var screen);
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
            if (transState != TransState.None || target == null || target == currentScreen)
                return;

            pendingTarget = target;
            transState = TransState.ClosingCurrent;
            currentScreen.Close();
        }

        public void RegisterBridge(GameCanvasBridge bridge)
        {
            if (bridge != null && !bridges.Contains(bridge))
                bridges.Add(bridge);
        }

        public void UnregisterBridge(GameCanvasBridge bridge)
        {
            bridges.Remove(bridge);
        }

        public void RebuildScreenMap()
        {
            screenMap.Clear();
            screenNameMap.Clear();
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                screenMap[screen.GetType()] = screen;
                screenNameMap[screen.ScreenId] = screen;
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
            transState = TransState.None;
            StopAllCoroutines();
            foreach (var screen in screens)
            {
                if (screen != null)
                    screen.SetVisibleImmediate(false);
            }
            currentScreen = null;
        }
    }
}

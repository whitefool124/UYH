using System;
using SpellGuard.InputSystem;
using SpellGuard.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpellGuard.UI.Canvas
{
    public class StartMenuScreen : UIScreen
    {
        public enum Mode { Main, Guide, Settings, Calibration }

        [Header("Layout")]
        [SerializeField] private RectTransform heroPanel;
        [SerializeField] private RectTransform navPanel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text hintText;
        [SerializeField] private RawImage cameraPreview;
        [SerializeField] private RectTransform cameraPreviewRect;

        [Header("Button Prefab")]
        [SerializeField] private GameObject navButtonPrefab;
        [SerializeField] private Color buttonNormalColor = new Color(0.08f, 0.1f, 0.18f, 0.94f);
        [SerializeField] private Color buttonSelectedColor = new Color(0.25f, 0.35f, 0.55f, 0.96f);
        [SerializeField] private Color accentColor = new Color(0.96f, 0.64f, 0.22f, 0.95f);
        [SerializeField] private Color panelBgColor = new Color(0.045f, 0.06f, 0.1f, 0.96f);

        public event Action<string> ButtonClicked;

        private Mode currentMode;
        private int selectedIndex;
        private NavButton[] buttons;
        private RawImage backgroundImage;
        private Image backgroundScrim;
        private RectTransform gesturePanel;
        private Image gesturePanelBg;
        private Text gestureTitleText;
        private Text gestureValueText;
        private Text gestureHintText;
        private RectTransform gesturePointsRoot;
        private Image[] gesturePoints;
        private Image heroBgImage;
        private Image navBgImage;
        private Vector2 heroFrom;
        private Vector2 heroTo;
        private Vector2 navFrom;
        private Vector2 navTo;
        private RectTransform[] buttonRects;

        private const float ButtonStagger = 0.12f;
        private const float ButtonAnimFraction = 0.9f;
        private const int HandLandmarkCount = 21;

        private struct NavButton
        {
            public string Key;
            public string Label;
            public GameObject Go;
            public Text LabelText;
            public Image BgImage;
        }

        public void Configure(Mode mode)
        {
            currentMode = mode;
            selectedIndex = 0;
            ApplyVisualStyle();
            cameraPreviewRect?.gameObject.SetActive(mode == Mode.Calibration);
            cameraPreview?.gameObject.SetActive(mode == Mode.Calibration);
            BuildScreen();
            UpdateSelection();
        }

        protected override void OnOpenStart()
        {
            heroFrom = new Vector2(-120f, 0f);
            navFrom = new Vector2(120f, 0f);
            heroTo = heroPanel != null ? heroPanel.anchoredPosition : Vector2.zero;
            navTo = navPanel != null ? navPanel.anchoredPosition : Vector2.zero;

            if (heroPanel != null) heroPanel.anchoredPosition = heroFrom;
            if (navPanel != null) navPanel.anchoredPosition = navFrom;

            if (buttons != null)
            {
                buttonRects = new RectTransform[buttons.Length];
                for (var i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].Go == null) continue;
                    buttonRects[i] = buttons[i].Go.GetComponent<RectTransform>();
                    if (buttonRects[i] != null)
                    {
                        buttonRects[i].localScale = Vector3.zero;
                    }
                }
            }
            else
            {
                buttonRects = null;
            }
        }

        protected override void OnOpenUpdate(float t)
        {
            var eased = UITransitions.EaseOutCubic(t);
            if (heroPanel != null)
            {
                heroPanel.anchoredPosition = Vector2.Lerp(heroFrom, heroTo, eased);
            }

            if (navPanel != null)
            {
                navPanel.anchoredPosition = Vector2.Lerp(navFrom, navTo, eased);
            }

            if (buttonRects == null || buttonRects.Length == 0) return;

            var btnWindow = ButtonAnimFraction / buttonRects.Length;
            for (var i = 0; i < buttonRects.Length; i++)
            {
                if (buttonRects[i] == null) continue;
                var btnStart = i * ButtonStagger;
                var btnT = Mathf.Clamp01((t - btnStart) / btnWindow);
                var scale = UITransitions.EaseOutBack(btnT);
                buttonRects[i].localScale = new Vector3(scale, scale, 1f);
            }
        }

        protected override void OnOpenComplete()
        {
            if (heroPanel != null) heroPanel.anchoredPosition = heroTo;
            if (navPanel != null) navPanel.anchoredPosition = navTo;
            if (buttonRects == null) return;

            for (var i = 0; i < buttonRects.Length; i++)
            {
                if (buttonRects[i] != null)
                {
                    buttonRects[i].localScale = Vector3.one;
                }
            }
        }

        public void MoveSelection(int delta)
        {
            if (buttons == null || buttons.Length == 0) return;
            selectedIndex = (selectedIndex + delta) % buttons.Length;
            if (selectedIndex < 0) selectedIndex += buttons.Length;
            UpdateSelection();
        }

        public void ActivateSelected()
        {
            if (buttons == null || selectedIndex < 0 || selectedIndex >= buttons.Length) return;
            ButtonClicked?.Invoke(buttons[selectedIndex].Key);
        }

        public string SelectedKey => buttons != null && selectedIndex >= 0 && selectedIndex < buttons.Length
            ? buttons[selectedIndex].Key
            : null;

        public void UpdateGestureCapture(GestureFrame frame, GestureSnapshot snapshot)
        {
            EnsureGestureCapturePanel();

            var hand = frame.PrimaryHand;
            var hasHand = snapshot.HandPresent || hand.IsTracked;
            var gesture = hasHand ? (snapshot.Gesture != GestureType.None ? snapshot.Gesture : hand.StaticGesture) : GestureType.None;
            var confidence = Mathf.Max(snapshot.Confidence, hand.Confidence);

            if (gestureTitleText != null)
            {
                gestureTitleText.text = "\u5f53\u524d\u624b\u52bf";
            }

            if (gestureValueText != null)
            {
                gestureValueText.text = hasHand
                    ? $"{gesture.ToChinese()}  {confidence:P0}"
                    : "\u672a\u68c0\u6d4b\u5230\u624b";
            }

            if (gestureHintText != null)
            {
                gestureHintText.text = "\u6325\u52a8\u5207\u6362 \u00b7 \u63e1\u62f3\u786e\u8ba4 \u00b7 \u5f20\u638c\u8fd4\u56de";
            }

            UpdateGesturePoints(hand, hasHand);
        }

        private void BuildScreen()
        {
            ClearButtons();

            switch (currentMode)
            {
                case Mode.Main:
                    SetHero("SPELL GUARD", "\u4f53\u611f\u65bd\u6cd5\u5b88\u536b",
                        "\u6b22\u8fce\u8fdb\u5165\u7b26\u5370\u5b88\u536b\u3002\n\n\u63a8\u8350\u6d41\u7a0b\uff1a\u73a9\u6cd5\u8bf4\u660e -> \u6444\u50cf\u5934\u6821\u51c6 -> \u5f00\u59cb\u5b88\u536b\u3002",
                        "\u6325\u52a8\u5207\u6362\uff0c\u63e1\u62f3\u786e\u8ba4\uff0c\u5f20\u638c\u8fd4\u56de\u3002");
                    AddButtons(("start", "\u5f00\u59cb\u5b88\u536b"), ("guide", "\u73a9\u6cd5\u8bf4\u660e"),
                        ("calibration", "\u6444\u50cf\u5934\u6821\u51c6"), ("settings", "\u8bbe\u7f6e"),
                        ("developer-tools", "\u5f00\u53d1\u8005\u5de5\u5177"));
                    break;

                case Mode.Guide:
                    SetHero("\u73a9\u6cd5\u8bf4\u660e", "\u5b88\u4f4f\u4eea\u5f0f\u6838\u5fc3",
                        "\u76ee\u6807\uff1a\u963b\u6b62\u654c\u4eba\u7a81\u7834\u901a\u9053\uff0c\u8fbe\u5230\u76ee\u6807\u5206\u6570\u5373\u53ef\u80dc\u5229\u3002\n\n\u6218\u6597\uff1a\u63e1\u62f3=\u706b\u7130\uff0cV \u624b\u52bf=\u51b0\u971c\uff0c\u5f20\u638c=\u62a4\u76fe\u3002\n\n\u79fb\u52a8\uff1a\u5de6\u53f3/\u4e0a\u4e0b\u6325\u52a8\u8fdb\u884c\u6362\u4f4d\u3002\n\n\u83dc\u5355\uff1a\u6325\u52a8\u5207\u6362\uff0c\u63e1\u62f3\u786e\u8ba4\uff0c\u5f20\u638c\u8fd4\u56de\u3002",
                        "\u51c6\u5907\u597d\u540e\u76f4\u63a5\u5f00\u59cb\u5b88\u536b\u3002");
                    AddButtons(("start", "\u5f00\u59cb\u5b88\u536b"), ("back", "\u8fd4\u56de\u4e3b\u83dc\u5355"));
                    break;

                case Mode.Settings:
                    SetHero("\u8bbe\u7f6e", "\u6f14\u793a\u524d\u786e\u8ba4",
                        "\u8f93\u5165\u6a21\u5f0f\u3001\u65bd\u6cd5\u786e\u8ba4\u3001\u654c\u4eba\u8282\u594f\u548c\u97f3\u91cf\u3002\n\n\u6b63\u5f0f\u6f14\u793a\u5efa\u8bae\u4f7f\u7528 Mock\uff1b\u9700\u8981\u771f\u5b9e\u6444\u50cf\u5934\u65f6\u5148\u5230\u6821\u51c6\u9875\u786e\u8ba4\u753b\u9762\u3002",
                        "\u5f20\u638c\u8fd4\u56de\u4e3b\u83dc\u5355\u3002");
                    AddButtons(("input-mode", "\u8f93\u5165\u6a21\u5f0f"), ("confirm", "\u7ed3\u5370\u786e\u8ba4"),
                        ("difficulty", "\u654c\u4eba\u8282\u594f"), ("music-volume", "\u97f3\u4e50\u97f3\u91cf"),
                        ("sfx-volume", "\u97f3\u6548\u97f3\u91cf"), ("back", "\u8fd4\u56de\u4e3b\u83dc\u5355"));
                    break;

                case Mode.Calibration:
                    SetHero("\u6444\u50cf\u5934\u6821\u51c6", "\u786e\u8ba4\u6444\u50cf\u5934\u662f\u5426\u53ef\u7528",
                        "\u6444\u50cf\u5934\uff1a\u68c0\u6d4b\u4e2d...\n\u8f93\u5165\u6a21\u5f0f\uff1a-\n\u8bc6\u522b\uff1a\u672a\u68c0\u6d4b\u5230\u624b",
                        "\u65e0\u753b\u9762\u65f6\u5148\u5207\u6362\u5230 Native MediaPipe\uff0c\u518d\u5c1d\u8bd5\u5207\u6362\u6444\u50cf\u5934\u3002");
                    AddButtons(("input-mode", "\u8f93\u5165\u6a21\u5f0f"), ("camera-device", "\u5207\u6362\u6444\u50cf\u5934"),
                        ("back", "\u8fd4\u56de\u4e3b\u83dc\u5355"));
                    break;
            }
        }

        private void SetHero(string title, string subtitle, string body, string hint)
        {
            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
            if (bodyText != null) bodyText.text = body;
            if (hintText != null) hintText.text = hint;
        }

        private void ApplyVisualStyle()
        {
            openDuration = 0.9f;
            closeDuration = 0.28f;

            EnsureBackground();
            EnsureGestureCapturePanel();
            ApplyPanel(heroPanel, ref heroBgImage, new Color(0.025f, 0.035f, 0.058f, 0.96f));
            ApplyPanel(navPanel, ref navBgImage, new Color(0f, 0f, 0f, 0f));
            ApplyText(titleText, 42, FontStyle.Bold, new Color(1f, 0.72f, 0.26f, 1f), TextAnchor.UpperLeft);
            ApplyText(subtitleText, 22, FontStyle.Bold, new Color(0.76f, 0.9f, 1f, 1f), TextAnchor.UpperLeft);
            ApplyText(bodyText, 18, FontStyle.Normal, new Color(0.88f, 0.94f, 1f, 1f), TextAnchor.UpperLeft);
            ApplyText(hintText, 15, FontStyle.Bold, new Color(0.55f, 0.72f, 1f, 1f), TextAnchor.MiddleCenter);

            if (heroPanel != null)
            {
                heroPanel.anchorMin = new Vector2(0.06f, 0.12f);
                heroPanel.anchorMax = new Vector2(0.45f, 0.9f);
                heroPanel.offsetMin = Vector2.zero;
                heroPanel.offsetMax = Vector2.zero;
            }

            if (navPanel != null)
            {
                navPanel.anchorMin = new Vector2(0.49f, 0.28f);
                navPanel.anchorMax = new Vector2(0.94f, 0.66f);
                navPanel.offsetMin = Vector2.zero;
                navPanel.offsetMax = Vector2.zero;
            }
        }

        private void EnsureBackground()
        {
            var parent = heroPanel != null ? heroPanel.parent as RectTransform : transform as RectTransform;
            if (parent == null) return;

            if (backgroundImage == null)
            {
                var existing = parent.Find("RuntimeCleanSciFiBackground");
                var bgGo = existing != null ? existing.gameObject : new GameObject("RuntimeCleanSciFiBackground");
                bgGo.transform.SetParent(parent, false);
                bgGo.transform.SetAsFirstSibling();
                backgroundImage = bgGo.GetComponent<RawImage>();
                if (backgroundImage == null) backgroundImage = bgGo.AddComponent<RawImage>();
                backgroundImage.raycastTarget = false;

                var rect = bgGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            backgroundImage.texture = SpellGuardRuntimeSkin.StartMenuBackdrop ?? SpellGuardRuntimeSkin.ScreenMenuGateway;
            backgroundImage.color = new Color(0.88f, 0.94f, 1f, 0.62f);

            if (backgroundScrim == null)
            {
                var existing = parent.Find("RuntimeBackgroundReadabilityScrim");
                var scrimGo = existing != null ? existing.gameObject : new GameObject("RuntimeBackgroundReadabilityScrim");
                scrimGo.transform.SetParent(parent, false);
                scrimGo.transform.SetSiblingIndex(Mathf.Min(1, parent.childCount - 1));
                backgroundScrim = scrimGo.GetComponent<Image>();
                if (backgroundScrim == null) backgroundScrim = scrimGo.AddComponent<Image>();
                backgroundScrim.raycastTarget = false;

                var rect = scrimGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            backgroundScrim.color = new Color(0.01f, 0.014f, 0.028f, 0.42f);
        }

        private void EnsureGestureCapturePanel()
        {
            var parent = heroPanel != null ? heroPanel.parent as RectTransform : transform as RectTransform;
            if (parent == null) return;

            if (gesturePanel == null)
            {
                var existing = parent.Find("RuntimeGestureCapturePanel");
                var go = existing != null ? existing.gameObject : new GameObject("RuntimeGestureCapturePanel");
                go.transform.SetParent(parent, false);
                go.transform.SetAsLastSibling();
                gesturePanel = EnsureRectTransform(go);
                gesturePanel.anchorMin = new Vector2(0.06f, 0.025f);
                gesturePanel.anchorMax = new Vector2(0.36f, 0.12f);
                gesturePanel.offsetMin = Vector2.zero;
                gesturePanel.offsetMax = Vector2.zero;

                gesturePanelBg = go.GetComponent<Image>();
                if (gesturePanelBg == null) gesturePanelBg = go.AddComponent<Image>();
                gesturePanelBg.raycastTarget = false;
            }

            gesturePanelBg.color = new Color(0.018f, 0.026f, 0.045f, 0.9f);
            gestureTitleText = EnsureText(gesturePanel, "Title", new Vector2(0.04f, 0.52f), new Vector2(0.36f, 0.92f), 13, FontStyle.Bold, new Color(0.55f, 0.72f, 1f, 1f), TextAnchor.MiddleLeft);
            gestureValueText = EnsureText(gesturePanel, "Value", new Vector2(0.04f, 0.08f), new Vector2(0.42f, 0.58f), 19, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            gestureHintText = EnsureText(gesturePanel, "Hint", new Vector2(0.44f, 0.08f), new Vector2(0.96f, 0.42f), 12, FontStyle.Normal, new Color(0.72f, 0.82f, 0.94f, 1f), TextAnchor.MiddleRight);

            if (gesturePointsRoot == null)
            {
                var existing = gesturePanel.Find("CapturePoints");
                var go = existing != null ? existing.gameObject : new GameObject("CapturePoints");
                go.transform.SetParent(gesturePanel, false);
                gesturePointsRoot = EnsureRectTransform(go);
                gesturePointsRoot.anchorMin = new Vector2(0.44f, 0.44f);
                gesturePointsRoot.anchorMax = new Vector2(0.96f, 0.92f);
                gesturePointsRoot.offsetMin = Vector2.zero;
                gesturePointsRoot.offsetMax = Vector2.zero;
                gesturePoints = new Image[HandLandmarkCount];
            }

            if (gesturePoints == null || gesturePoints.Length != HandLandmarkCount)
            {
                gesturePoints = new Image[HandLandmarkCount];
            }

            for (var i = 0; i < HandLandmarkCount; i++)
            {
                if (gesturePoints[i] != null) continue;
                var pointGo = new GameObject($"Point_{i:00}");
                pointGo.transform.SetParent(gesturePointsRoot, false);
                var rect = pointGo.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * (i == 0 ? 7f : 5f);
                var image = pointGo.AddComponent<Image>();
                image.raycastTarget = false;
                gesturePoints[i] = image;
            }
        }

        private void UpdateGesturePoints(TrackedHandState hand, bool hasHand)
        {
            if (gesturePointsRoot == null || gesturePoints == null) return;

            var rect = gesturePointsRoot.rect;
            var landmarks = hand.Landmarks;
            var hasLandmarks = hasHand && landmarks != null && landmarks.Length > 0;

            for (var i = 0; i < gesturePoints.Length; i++)
            {
                var point = gesturePoints[i];
                if (point == null) continue;

                var rectTransform = point.rectTransform;
                var active = hasLandmarks && i < landmarks.Length;
                point.color = active
                    ? (i == 0 ? new Color(1f, 0.72f, 0.26f, 0.95f) : new Color(0.38f, 0.9f, 1f, 0.82f))
                    : new Color(0.35f, 0.46f, 0.62f, 0.18f);

                var normalized = active
                    ? landmarks[i]
                    : new Vector2((i % 7) / 6f, 1f - (i / 7) / 2f);
                rectTransform.anchoredPosition = new Vector2(
                    (Mathf.Clamp01(normalized.x) - 0.5f) * rect.width,
                    (0.5f - Mathf.Clamp01(normalized.y)) * rect.height);
            }
        }

        private static Text EnsureText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            var existing = parent.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            if (text == null) text = go.AddComponent<Text>();
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            ApplyText(text, size, style, color, alignment);
            return text;
        }

        private static RectTransform EnsureRectTransform(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            return rect != null ? rect : go.AddComponent<RectTransform>();
        }

        private static void ApplyPanel(RectTransform panel, ref Image image, Color color)
        {
            if (panel == null) return;
            if (image == null) image = panel.GetComponent<Image>();
            if (image == null) image = panel.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void ApplyText(Text text, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            if (text == null) return;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
        }

        private void AddButtons(params (string key, string label)[] items)
        {
            buttons = new NavButton[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var go = Instantiate(navButtonPrefab, navPanel);
                go.name = $"Btn_{items[i].key}";
                go.SetActive(true);

                var labelText = go.GetComponentInChildren<Text>();
                if (labelText == null)
                {
                    var labelGo = new GameObject("Label");
                    labelGo.transform.SetParent(go.transform, false);
                    labelText = labelGo.AddComponent<Text>();
                    labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    labelText.fontSize = 20;
                    labelText.alignment = TextAnchor.MiddleLeft;
                    labelText.color = new Color(0.9f, 0.92f, 0.98f, 1f);
                    labelText.rectTransform.anchorMin = Vector2.zero;
                    labelText.rectTransform.anchorMax = Vector2.one;
                    labelText.rectTransform.sizeDelta = new Vector2(-32f, 0f);
                    labelText.rectTransform.anchoredPosition = new Vector2(16f, 0f);
                }
                else
                {
                    ApplyText(labelText, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
                }

                var bgImage = go.GetComponent<Image>();
                if (bgImage == null) bgImage = go.AddComponent<Image>();
                bgImage.color = buttonNormalColor;

                var rect = go.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 48f);
                }

                labelText.text = items[i].label;

                var btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                btn.targetGraphic = bgImage;
                var cb = btn.colors;
                cb.normalColor = buttonNormalColor;
                cb.highlightedColor = new Color(0.18f, 0.28f, 0.42f, 0.98f);
                cb.pressedColor = new Color(0.24f, 0.42f, 0.58f, 1f);
                cb.selectedColor = buttonSelectedColor;
                cb.fadeDuration = 0.12f;
                btn.colors = cb;

                var key = items[i].key;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ButtonClicked?.Invoke(key));

                buttons[i] = new NavButton
                {
                    Key = key,
                    Label = items[i].label,
                    Go = go,
                    LabelText = labelText,
                    BgImage = bgImage
                };
            }

            selectedIndex = Mathf.Min(selectedIndex, buttons.Length - 1);
        }

        private void ClearButtons()
        {
            if (buttons != null)
            {
                foreach (var b in buttons)
                {
                    if (b.Go != null) Destroy(b.Go);
                }
            }

            if (navPanel != null)
            {
                for (var i = navPanel.childCount - 1; i >= 0; i--)
                {
                    Destroy(navPanel.GetChild(i).gameObject);
                }
            }

            buttons = null;
            selectedIndex = 0;
        }

        private void UpdateSelection()
        {
            if (buttons == null) return;
            for (var i = 0; i < buttons.Length; i++)
            {
                var selected = i == selectedIndex;
                buttons[i].BgImage.color = selected ? new Color(0.22f, 0.36f, 0.54f, 0.98f) : buttonNormalColor;
                buttons[i].LabelText.color = selected ? Color.white : new Color(0.78f, 0.86f, 0.96f, 1f);
                buttons[i].LabelText.text = selected
                    ? $">  {buttons[i].Label}"
                    : $"   {buttons[i].Label}";
            }
        }
    }
}

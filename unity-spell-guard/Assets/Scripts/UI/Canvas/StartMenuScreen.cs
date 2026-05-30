using System;
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
        private Image heroBgImage;
        private Image navBgImage;
        private Vector2 heroFrom;
        private Vector2 heroTo;
        private Vector2 navFrom;
        private Vector2 navTo;
        private RectTransform[] buttonRects;

        private const float ButtonStagger = 0.12f;
        private const float ButtonAnimFraction = 0.9f;

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

        private void BuildScreen()
        {
            ClearButtons();

            switch (currentMode)
            {
                case Mode.Main:
                    SetHero("SPELL GUARD", "体感施法守卫",
                        "欢迎进入符印守卫。\n\n推荐流程：玩法说明 -> 摄像头校准 -> 开始守卫。",
                        "挥动切换，握拳确认，张掌返回。");
                    AddButtons(("start", "开始守卫"), ("guide", "玩法说明"),
                        ("calibration", "摄像头校准"), ("settings", "设置"),
                        ("developer-tools", "开发者工具"));
                    break;

                case Mode.Guide:
                    SetHero("玩法说明", "守住仪式核心",
                        "目标：阻止敌人突破通道，达到目标分数即可胜利。\n\n战斗：握拳=火焰，V 手势=冰霜，张掌=护盾。\n\n移动：左右/上下挥动进行换位。\n\n菜单：挥动切换，握拳确认，张掌返回。",
                        "准备好后直接开始守卫。");
                    AddButtons(("start", "开始守卫"), ("back", "返回主菜单"));
                    break;

                case Mode.Settings:
                    SetHero("设置", "演示前确认",
                        "输入模式、施法确认、敌人节奏和音量。\n\n正式演示建议使用 Mock；需要真实摄像头时先到校准页确认画面。",
                        "张掌返回主菜单。");
                    AddButtons(("input-mode", "输入模式"), ("confirm", "结印确认"),
                        ("difficulty", "敌人节奏"), ("music-volume", "音乐音量"),
                        ("sfx-volume", "音效音量"), ("back", "返回主菜单"));
                    break;

                case Mode.Calibration:
                    SetHero("摄像头校准", "确认摄像头是否可用",
                        "摄像头：检测中...\n输入模式：-\n识别：未检测到手",
                        "无画面时先切换到 Native MediaPipe，再尝试切换摄像头。");
                    AddButtons(("input-mode", "输入模式"), ("camera-device", "切换摄像头"),
                        ("back", "返回主菜单"));
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
                    ? $"▶  {buttons[i].Label}"
                    : $"   {buttons[i].Label}";
            }
        }
    }
}

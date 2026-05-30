using System;
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
        private Image heroBgImage;
        private Image navBgImage;

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
            cameraPreviewRect?.gameObject.SetActive(mode == Mode.Calibration);
            cameraPreview?.gameObject.SetActive(mode == Mode.Calibration);
            BuildScreen();
            UpdateSelection();
        }

        private Vector2 _heroFrom, _heroTo, _navFrom, _navTo;
        private RectTransform[] _buttonRects;
        private const float ButtonStagger = 0.06f;
        private const float ButtonAnimFraction = 0.6f;

        protected override void OnOpenStart()
        {
            _heroFrom = new Vector2(-80f, 0f);
            _navFrom = new Vector2(80f, 0f);
            _heroTo = heroPanel != null ? heroPanel.anchoredPosition : Vector2.zero;
            _navTo = navPanel != null ? navPanel.anchoredPosition : Vector2.zero;

            if (heroPanel != null) heroPanel.anchoredPosition = _heroFrom;
            if (navPanel != null) navPanel.anchoredPosition = _navFrom;

            // Collect button transforms for cascade animation
            if (buttons != null && navPanel != null)
            {
                _buttonRects = new RectTransform[buttons.Length];
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].Go != null)
                    {
                        _buttonRects[i] = buttons[i].Go.GetComponent<RectTransform>();
                        if (_buttonRects[i] != null)
                            _buttonRects[i].localScale = Vector3.zero;
                    }
                }
            }
            else _buttonRects = null;
        }

        protected override void OnOpenUpdate(float t)
        {
            if (heroPanel != null)
                heroPanel.anchoredPosition = Vector2.Lerp(_heroFrom, _heroTo, UITransitions.EaseOutBack(t));
            if (navPanel != null)
                navPanel.anchoredPosition = Vector2.Lerp(_navFrom, _navTo, UITransitions.EaseOutBack(t));

            // Button cascade: each button scales in with stagger
            if (_buttonRects != null)
            {
                float btnWindow = ButtonAnimFraction / _buttonRects.Length;
                for (int i = 0; i < _buttonRects.Length; i++)
                {
                    if (_buttonRects[i] == null) continue;
                    float btnStart = i * ButtonStagger;
                    float btnT = Mathf.Clamp01((t - btnStart) / btnWindow);
                    float s = UITransitions.EaseOutBack(btnT);
                    _buttonRects[i].localScale = new Vector3(s, s, 1f);
                }
            }
        }

        protected override void OnOpenComplete()
        {
            if (heroPanel != null) heroPanel.anchoredPosition = _heroTo;
            if (navPanel != null) navPanel.anchoredPosition = _navTo;
            if (_buttonRects != null)
            {
                for (int i = 0; i < _buttonRects.Length; i++)
                    if (_buttonRects[i] != null) _buttonRects[i].localScale = Vector3.one;
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
            ? buttons[selectedIndex].Key : null;

        private void BuildScreen()
        {
            ClearButtons();

            switch (currentMode)
            {
                case Mode.Main:
                    SetHero("SPELL GUARD", "体感施法守卫",
                        "欢迎进入符印守卫。\n\n推荐流程：玩法说明 → 摄像头校准 → 开始守卫。",
                        "挥动切换，握拳确认，张掌返回。");
                    AddButtons(("start", "开始守卫"), ("guide", "玩法说明"),
                        ("calibration", "摄像头校准"), ("settings", "设置"),
                        ("developer-tools", "开发者工具"));
                    break;

                case Mode.Guide:
                    SetHero("玩法说明", "守住仪式核心",
                        "目标：阻止敌人突破通道，达到目标分数即胜利。\n\n战斗：握拳=火焰，V手势=冰霜，张掌=护盾。\n\n移动：左右/上下挥动进行换位。\n\n菜单：挥动切换，握拳确认，张掌返回。",
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
                        "摄像头：检测中...\n输入模式：--\n识别：未检测到手",
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

        private void AddButtons(params (string key, string label)[] items)
        {
            buttons = new NavButton[items.Length];
            for (int i = 0; i < items.Length; i++)
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
                    labelText.fontSize = 18;
                    labelText.alignment = TextAnchor.MiddleLeft;
                    labelText.color = new Color(0.9f, 0.92f, 0.98f, 1f);
                    labelText.rectTransform.anchorMin = Vector2.zero;
                    labelText.rectTransform.anchorMax = Vector2.one;
                    labelText.rectTransform.sizeDelta = new Vector2(-32f, 0f);
                    labelText.rectTransform.anchoredPosition = new Vector2(16f, 0f);
                }

                var bgImage = go.GetComponent<Image>();
                if (bgImage == null) bgImage = go.AddComponent<Image>();

                labelText.text = items[i].label;

                var btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                btn.targetGraphic = bgImage;
                var cb = btn.colors;
                cb.normalColor = buttonNormalColor;
                cb.highlightedColor = new Color(0.35f, 0.42f, 0.58f, 0.96f);
                cb.pressedColor = new Color(0.18f, 0.22f, 0.38f, 1f);
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
            // Also destroy any orphaned children (e.g. from Edit Mode Configure)
            if (navPanel != null)
            {
                for (int i = navPanel.childCount - 1; i >= 0; i--)
                    Destroy(navPanel.GetChild(i).gameObject);
            }
            buttons = null;
            selectedIndex = 0;
        }

        private void UpdateSelection()
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                var selected = i == selectedIndex;
                buttons[i].BgImage.color = selected ? buttonSelectedColor : buttonNormalColor;
                buttons[i].LabelText.text = selected
                    ? $"▶ {buttons[i].Label}"
                    : $"   {buttons[i].Label}";
            }
        }
    }
}

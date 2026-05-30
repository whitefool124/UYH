using System;
using UnityEngine;
using UnityEngine.UI;

namespace SpellGuard.UI.Canvas
{
    public class GameOverlayScreen : UIScreen
    {
        public enum Mode { Menu, Settings, Tutorial, Training, Paused, Results }

        [Header("Layout")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text hintText;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private RectTransform buttonContainer;

        [Header("Style")]
        [SerializeField] private Color panelBgColor = new Color(0.04f, 0.05f, 0.11f, 0.96f);
        [SerializeField] private Color accentColor = new Color(0.96f, 0.64f, 0.22f, 0.95f);
        [SerializeField] private Color btnNormalColor = new Color(0.08f, 0.1f, 0.18f, 0.94f);
        [SerializeField] private Color btnSelectedColor = new Color(0.25f, 0.35f, 0.55f, 0.96f);

        public event Action<string> ButtonClicked;

        private Mode currentMode;
        private int selectedIndex;
        private GameButton[] buttons;
        private Image panelBg;

        private struct GameButton
        {
            public string Key;
            public string Label;
            public GameObject Go;
            public Text LabelText;
            public Image BgImage;
        }

        protected override void Awake()
        {
            base.Awake();
            panelBg = panel?.GetComponent<Image>();
            if (panelBg != null) panelBg.color = panelBgColor;
        }

        public void Configure(Mode mode, string title, string subtitle, string body, string hint,
            params (string key, string label)[] btnItems)
        {
            currentMode = mode;
            selectedIndex = 0;

            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
            if (bodyText != null) bodyText.text = body;
            if (hintText != null) hintText.text = hint;

            BuildButtons(btnItems);
        }

        public void UpdateBody(string body)
        {
            if (bodyText != null) bodyText.text = body;
        }

        public void UpdateSubtitle(string subtitle)
        {
            if (subtitleText != null) subtitleText.text = subtitle;
        }

        public void MoveSelection(int delta)
        {
            if (buttons == null || buttons.Length == 0) return;
            selectedIndex = (selectedIndex + delta) % buttons.Length;
            if (selectedIndex < 0) selectedIndex += buttons.Length;
            UpdateSelectionVisual();
        }

        public void ActivateSelected()
        {
            if (buttons == null || selectedIndex < 0 || selectedIndex >= buttons.Length) return;
            ButtonClicked?.Invoke(buttons[selectedIndex].Key);
        }

        public string SelectedKey => buttons != null && selectedIndex >= 0 && selectedIndex < buttons.Length
            ? buttons[selectedIndex].Key : null;

        private void BuildButtons((string key, string label)[] items)
        {
            ClearButtons();
            if (items == null || items.Length == 0) return;

            buttons = new GameButton[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var go = Instantiate(buttonPrefab, buttonContainer);
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
                    labelText.alignment = TextAnchor.MiddleCenter;
                    labelText.color = new Color(0.9f, 0.92f, 0.98f, 1f);
                    labelText.rectTransform.anchorMin = Vector2.zero;
                    labelText.rectTransform.anchorMax = Vector2.one;
                    labelText.rectTransform.sizeDelta = Vector2.zero;
                }

                var bgImage = go.GetComponent<Image>();
                if (bgImage == null) bgImage = go.AddComponent<Image>();

                labelText.text = items[i].label;

                var btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                btn.targetGraphic = bgImage;
                var key = items[i].key;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ButtonClicked?.Invoke(key));

                buttons[i] = new GameButton
                {
                    Key = key, Label = items[i].label,
                    Go = go, LabelText = labelText, BgImage = bgImage
                };
            }

            selectedIndex = 0;
            UpdateSelectionVisual();
        }

        private void ClearButtons()
        {
            if (buttons != null)
            {
                foreach (var b in buttons)
                    if (b.Go != null) Destroy(b.Go);
            }
            buttons = null;
            selectedIndex = 0;
        }

        private void UpdateSelectionVisual()
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                var sel = i == selectedIndex;
                buttons[i].BgImage.color = sel ? btnSelectedColor : btnNormalColor;
                buttons[i].LabelText.text = sel ? $"▶ {buttons[i].Label}" : $"   {buttons[i].Label}";
            }
        }
    }
}

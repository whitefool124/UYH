using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SpellGuard.UI
{
    public static class SpellGuardRuntimeSkin
    {
        public const string Root = "Art/UI/SpellGuard/";

        private static Texture2D panelLarge;
        private static Texture2D panelSmall;
        private static Texture2D buttonPrimary;
        private static Texture2D buttonSecondary;
        private static Texture2D progressBar;
        private static Texture2D hudFrame;
        private static Texture2D iconEnergy;
        private static Texture2D iconShield;
        private static Texture2D iconSpell;
        private static Texture2D divider;
        private static Texture2D generatedPanel;
        private static Texture2D generatedButtonNormal;
        private static Texture2D generatedButtonHover;
        private static Texture2D generatedButtonActive;
        private static Texture2D generatedProgressBg;
        private static Texture2D generatedProgressFill;
        private static Texture2D generatedIconFire;
        private static Texture2D generatedIconIce;
        private static Texture2D generatedIconShield;
        private static Texture2D generatedIconHealth;
        private static Texture2D handFist;
        private static Texture2D handOpenPalm;
        private static Texture2D handPoint;
        private static Texture2D handVSign;
        private static Texture2D screenMenuGateway;
        private static Texture2D screenResultsPanel;
        private static Texture2D screenMainMenuBg;
        private static Texture2D startMenuBackdrop;
        private static bool loaded;

        public static Texture2D PanelLarge => Load(ref panelLarge, "Sprites/ui_panel_large");
        public static Texture2D PanelSmall => Load(ref panelSmall, "Sprites/ui_panel_small");
        public static Texture2D ButtonPrimary => Load(ref buttonPrimary, "Sprites/ui_button_primary");
        public static Texture2D ButtonSecondary => Load(ref buttonSecondary, "Sprites/ui_button_secondary");
        public static Texture2D ProgressBar => Load(ref progressBar, "Sprites/ui_progress_bar");
        public static Texture2D HudFrame => Load(ref hudFrame, "Sprites/ui_hud_frame");
        public static Texture2D IconEnergy => Load(ref iconEnergy, "Sprites/ui_icon_energy");
        public static Texture2D IconShield => Load(ref iconShield, "Sprites/ui_icon_shield");
        public static Texture2D IconSpell => Load(ref iconSpell, "Sprites/ui_icon_spell");
        public static Texture2D Divider => Load(ref divider, "Sprites/ui_divider");
        public static Texture2D GeneratedPanel => Load(ref generatedPanel, "GeneratedCore/ui_panel_main");
        public static Texture2D ButtonNormal => Load(ref generatedButtonNormal, "GeneratedCore/ui_btn_primary_normal");
        public static Texture2D ButtonHover => Load(ref generatedButtonHover, "GeneratedCore/ui_btn_primary_hover");
        public static Texture2D ButtonActive => Load(ref generatedButtonActive, "GeneratedCore/ui_btn_primary_active");
        public static Texture2D ProgressBg => Load(ref generatedProgressBg, "GeneratedCore/ui_progress_bar_bg");
        public static Texture2D ProgressFill => Load(ref generatedProgressFill, "GeneratedCore/ui_progress_bar_fill");
        public static Texture2D IconFire => Load(ref generatedIconFire, "GeneratedCore/ui_icon_fire");
        public static Texture2D IconIce => Load(ref generatedIconIce, "GeneratedCore/ui_icon_ice");
        public static Texture2D IconShieldGenerated => Load(ref generatedIconShield, "GeneratedCore/ui_icon_shield");
        public static Texture2D IconHealth => Load(ref generatedIconHealth, "GeneratedCore/ui_icon_health");
        public static Texture2D HandFist => Load(ref handFist, "Hands/hand_sprite_fist");
        public static Texture2D HandOpenPalm => Load(ref handOpenPalm, "Hands/hand_sprite_openpalm");
        public static Texture2D HandPoint => Load(ref handPoint, "Hands/hand_sprite_point");
        public static Texture2D HandVSign => Load(ref handVSign, "Hands/hand_sprite_vsign");
        public static Texture2D ScreenMenuGateway => Load(ref screenMenuGateway, "Screens/screen_menu_gateway");
        public static Texture2D ScreenResultsPanel => Load(ref screenResultsPanel, "Screens/screen_results_panel");
        public static Texture2D ScreenMainMenuBg => Load(ref screenMainMenuBg, "Screens/ui_screen_bg_main_menu");
        public static Texture2D StartMenuBackdrop => Load(ref startMenuBackdrop, "Screens/start_menu_bg_clean_scifi");

        public static readonly Color SpaceInk = new Color(0.025f, 0.035f, 0.055f, 0.92f);
        public static readonly Color Glass = new Color(0.075f, 0.095f, 0.14f, 0.76f);
        public static readonly Color GlassBright = new Color(0.12f, 0.16f, 0.22f, 0.78f);
        public static readonly Color Cyan = new Color(0.35f, 0.88f, 1f, 1f);
        public static readonly Color Mint = new Color(0.42f, 1f, 0.76f, 1f);
        public static readonly Color Amber = new Color(1f, 0.74f, 0.32f, 1f);
        public static readonly Color Red = new Color(1f, 0.34f, 0.24f, 1f);
        public static readonly Color Text = new Color(0.94f, 0.96f, 1f, 1f);
        public static readonly Color MutedText = new Color(0.64f, 0.73f, 0.84f, 1f);

        public static float Breathe(float speed = 1f, float min = 0.75f, float max = 1f)
        {
            return Mathf.Lerp(min, max, (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f);
        }

        public static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        public static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            _ = PanelLarge;
            _ = PanelSmall;
            _ = ButtonPrimary;
            _ = ButtonSecondary;
            _ = ProgressBar;
            _ = HudFrame;
            loaded = true;
        }

        public static Texture2D GetHandTexture(string gestureLabel)
        {
            if (string.IsNullOrEmpty(gestureLabel))
            {
                return null;
            }

            if (gestureLabel.Contains("握") || gestureLabel.Contains("拳") || gestureLabel.Contains("火"))
            {
                return HandFist;
            }

            if (gestureLabel.Contains("V") || gestureLabel.Contains("胜") || gestureLabel.Contains("冰"))
            {
                return HandVSign;
            }

            if (gestureLabel.Contains("指") || gestureLabel.Contains("Point"))
            {
                return HandPoint;
            }

            if (gestureLabel.Contains("掌") || gestureLabel.Contains("盾"))
            {
                return HandOpenPalm;
            }

            return null;
        }

        public static void DrawImagePanel(Rect rect, Texture2D texture, Color fallbackFill, Color accent, float border = 1.5f)
        {
            var previous = GUI.color;
            GUI.color = fallbackFill;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = new Color(accent.r, accent.g, accent.b, accent.a * Breathe(1.6f, 0.58f, 0.92f));
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - border, rect.width, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, border, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - border, rect.y, border, rect.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.07f);
            GUI.DrawTexture(new Rect(rect.x + 10f, rect.y + 10f, Mathf.Max(1f, rect.width - 20f), 1f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void DrawDivider(Rect rect, Color color)
        {
            var previous = GUI.color;
            if (Divider != null)
            {
                GUI.color = new Color(1f, 1f, 1f, color.a);
                GUI.DrawTexture(rect, Divider, ScaleMode.StretchToFill, true);
            }
            else
            {
                GUI.color = color;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }

        public static void DrawProgress(Rect rect, float progress, Color color)
        {
            var previous = GUI.color;
            progress = Mathf.Clamp01(progress);
            if (ProgressBg != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.78f);
                GUI.DrawTexture(rect, ProgressBg, ScaleMode.StretchToFill, true);
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }

            var fillRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
            if (ProgressFill != null)
            {
                GUI.color = new Color(color.r, color.g, color.b, 0.95f);
                GUI.DrawTexture(fillRect, ProgressFill, ScaleMode.StretchToFill, true);
            }
            else
            {
                GUI.color = new Color(color.r, color.g, color.b, 0.92f);
                GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }

        public static void DrawScanLines(Rect rect, float scale, Color color)
        {
            var previous = GUI.color;
            var spacing = Mathf.Max(10f, 16f * scale);
            var offset = Mathf.Repeat(Time.unscaledTime * 18f, spacing);
            GUI.color = new Color(color.r, color.g, color.b, color.a * 0.16f);
            for (var y = rect.y + offset; y < rect.yMax; y += spacing)
            {
                GUI.DrawTexture(new Rect(rect.x, y, rect.width, Mathf.Max(1f, scale)), Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }

        private static Texture2D Load(ref Texture2D cache, string path)
        {
            if (cache == null)
            {
                cache = Resources.Load<Texture2D>(Root + path);
                if (cache == null)
                {
                    cache = LoadAssetDatabaseTexture(path);
                }
            }

            return cache;
        }

        private static Texture2D LoadAssetDatabaseTexture(string path)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/{Root}{path}.png");
#else
            return null;
#endif
        }
    }
}

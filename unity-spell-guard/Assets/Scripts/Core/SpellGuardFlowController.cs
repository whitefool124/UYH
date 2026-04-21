using System;
using SpellGuard.Combat;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardFlowController : MonoBehaviour
    {
        [Serializable]
        private struct Region
        {
            public string key;
            public string label;
            public Rect rect;
        }

        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private ExternalGestureBridgeProvider externalBridge;
        [SerializeField] private SpellGuardGameSettings settings;
        [SerializeField] private FpsGestureMotor motor;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private GameFlowManager gameFlow;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private float trainingMenuHoldSeconds = 1.6f;

        private SpellGuardScreen screen = SpellGuardScreen.Menu;
        private string focusedKey;
        private string dwellKey;
        private float dwellStartedAt;
        private float backStartedAt;
        private readonly Region[] regions = new Region[8];
        private int regionCount;
        private GUIStyle overlayTitleStyle;
        private GUIStyle overlayBodyStyle;
        private GUIStyle overlayHintStyle;
        private GUIStyle overlayButtonStyle;
        private GUIStyle overlayPanelStyle;
        private float cachedOverlayScale = -1f;

        private struct OverlayLayout
        {
            public Rect Panel;
            public Rect Content;
            public Rect Title;
            public Rect Body;
            public Rect Hint;
            public Rect ButtonsRow;
            public float Scale;
            public float Padding;
            public float Gap;
        }

        private int combatScore;
        private int combatHits;
        private int combatCasts;
        private int trainingCasts;
        private int trainingPointerChecks;
        private int trainingFireCasts;
        private int trainingIceCasts;
        private int trainingShieldCasts;
        private SpellType lastTrainingSpell = SpellType.None;
        private bool subscribed;
        private float lastHandledMotionTime = -999f;

        public SpellGuardScreen Screen => screen;
        public string HintText { get; private set; } = "菜单指令：食指指向移动焦点，停留确认。";

        public void Configure(
            GestureInputProviderBase provider,
            ExternalGestureBridgeProvider bridge,
            SpellGuardGameSettings gameSettings,
            FpsGestureMotor fpsMotor,
            GestureSpellCaster gestureSpellCaster,
            PlayerHealth health,
            Transform playerTransform,
            EnemySpawner spawner,
            GameFlowManager flow,
            Camera cameraRef)
        {
            inputProvider = provider;
            externalBridge = bridge;
            settings = gameSettings;
            motor = fpsMotor;
            spellCaster = gestureSpellCaster;
            playerHealth = health;
            playerRoot = playerTransform;
            enemySpawner = spawner;
            gameFlow = flow;
            mainCamera = cameraRef;

            RefreshSpellCasterSubscription();
        }

        private void OnEnable()
        {
            RefreshSpellCasterSubscription();
        }

        private void OnDisable()
        {
            if (spellCaster != null && subscribed)
            {
                spellCaster.SpellResolved -= HandleSpellResolved;
                subscribed = false;
            }
        }

        private void Start()
        {
            ReturnToMenu();
        }

        private void Update()
        {
            ApplyModeState();

            if (screen == SpellGuardScreen.Playing && gameFlow != null && gameFlow.GameOver)
            {
                screen = SpellGuardScreen.Results;
                HintText = "结果页指令：食指指向按钮并停留确认。";
            }

            if (screen == SpellGuardScreen.Menu || screen == SpellGuardScreen.Settings || screen == SpellGuardScreen.Tutorial || screen == SpellGuardScreen.Training || screen == SpellGuardScreen.Results)
            {
                UpdateMenuLikeInput();
            }
        }

        private void UpdateMenuLikeInput()
        {
            RebuildRegions();

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            if (snapshot.HandPresent && snapshot.Gesture == GestureType.OpenPalm && screen != SpellGuardScreen.Menu && screen != SpellGuardScreen.Playing && screen != SpellGuardScreen.Training)
            {
                if (backStartedAt <= 0f)
                {
                    backStartedAt = Time.time;
                }

                if (Time.time - backStartedAt >= settings.MenuBackHoldSeconds)
                {
                    ReturnToMenu();
                }

                return;
            }

            backStartedAt = 0f;

            if (!snapshot.HandPresent)
            {
                focusedKey = null;
                dwellKey = null;
                regionCount = 0;
                return;
            }

            var cursor = new Vector2(snapshot.ViewportPosition.x * UnityEngine.Screen.width, (1f - snapshot.ViewportPosition.y) * UnityEngine.Screen.height);
            var region = GetFocusedRegion(cursor);
            if (region == null)
            {
                focusedKey = null;
                dwellKey = null;
                return;
            }

            focusedKey = region.Value.key;
            if (HandleMotionGesture(region.Value.key))
            {
                return;
            }

            if (dwellKey != focusedKey)
            {
                dwellKey = focusedKey;
                dwellStartedAt = Time.time;
            }

            if (Time.time - dwellStartedAt >= GetRequiredHoldSeconds(region.Value.key))
            {
                ActivateRegion(region.Value.key);
                dwellKey = null;
                focusedKey = null;
            }
        }

        private bool HandleMotionGesture(string focusedRegionKey)
        {
            if (inputProvider == null)
            {
                return false;
            }

            var command = inputProvider.CurrentGestureCommand;
            if (!command.IsValid || command.Kind != GestureCommandKind.Motion || command.TriggeredTime <= lastHandledMotionTime)
            {
                return false;
            }

            switch (command.MotionGesture)
            {
                case MotionGestureType.SwipeRightToLeft:
                case MotionGestureType.OpenPalmSlapRightToLeft:
                    lastHandledMotionTime = command.TriggeredTime;
                    if (screen == SpellGuardScreen.Settings)
                    {
                        if (focusedRegionKey == "confirm")
                        {
                            settings.CycleConfirm();
                            HintText = $"设置已切换：施法确认 {settings.ConfirmLabel}";
                            dwellKey = null;
                            LogFlowEvent($"motion settings cycle confirm via {command.MotionGesture}");
                            return true;
                        }

                        if (focusedRegionKey == "difficulty")
                        {
                            settings.CycleDifficulty();
                            HintText = $"设置已切换：敌人节奏 {settings.DifficultyLabel}";
                            dwellKey = null;
                            LogFlowEvent($"motion settings cycle difficulty via {command.MotionGesture}");
                            return true;
                        }
                    }

                    if (screen == SpellGuardScreen.Settings || screen == SpellGuardScreen.Tutorial || screen == SpellGuardScreen.Results)
                    {
                        LogFlowEvent($"motion return to menu via {command.MotionGesture} from {screen}");
                        ReturnToMenu();
                        return true;
                    }
                    break;

                case MotionGestureType.SwipeLeftToRight:
                case MotionGestureType.OpenPalmSlapLeftToRight:
                    if (screen == SpellGuardScreen.Settings && (focusedRegionKey == "confirm" || focusedRegionKey == "difficulty"))
                    {
                        lastHandledMotionTime = command.TriggeredTime;
                        if (focusedRegionKey == "confirm")
                        {
                            settings.CycleConfirm();
                            HintText = $"设置已切换：施法确认 {settings.ConfirmLabel}";
                            LogFlowEvent($"motion settings cycle confirm via {command.MotionGesture}");
                        }
                        else
                        {
                            settings.CycleDifficulty();
                            HintText = $"设置已切换：敌人节奏 {settings.DifficultyLabel}";
                            LogFlowEvent($"motion settings cycle difficulty via {command.MotionGesture}");
                        }

                        dwellKey = null;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private Region? GetFocusedRegion(Vector2 cursor)
        {
            for (var index = 0; index < regionCount; index++)
            {
                if (regions[index].rect.Contains(cursor))
                {
                    return regions[index];
                }
            }

            return null;
        }

        private void ActivateRegion(string key)
        {
            switch (screen)
            {
                case SpellGuardScreen.Menu:
                    if (key == "start") StartRun();
                    else if (key == "training") StartTraining();
                    else if (key == "settings") { screen = SpellGuardScreen.Settings; HintText = "设置指令：指向条目并停留切换。"; }
                    else if (key == "tutorial") { screen = SpellGuardScreen.Tutorial; HintText = "教程指令：阅读后可开始守卫或进入训练场。"; }
                    break;
                case SpellGuardScreen.Settings:
                    if (key == "confirm") settings.CycleConfirm();
                    else if (key == "difficulty") settings.CycleDifficulty();
                    else if (key == "back") ReturnToMenu();
                    break;
                case SpellGuardScreen.Tutorial:
                    if (key == "play") StartRun();
                    else if (key == "training") StartTraining();
                    else if (key == "back") ReturnToMenu();
                    break;
                case SpellGuardScreen.Training:
                    if (key == "pointer-check") trainingPointerChecks++;
                    else if (key == "reset-training") ResetTrainingStats();
                    else if (key == "menu") ReturnToMenu();
                    break;
                case SpellGuardScreen.Results:
                    if (key == "restart") StartRun();
                    else if (key == "menu") ReturnToMenu();
                    break;
            }
        }

        private void StartRun()
        {
            ResetCombatStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            enemySpawner?.ApplySettings(settings.Difficulty);
            ResetPlayerPose();
            screen = SpellGuardScreen.Playing;
            HintText = "战斗指令：Point转向并抬高手位前进；握拳/V/张掌或打响指/左右扇手施法。";
        }

        private void StartTraining()
        {
            ResetTrainingStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            ResetPlayerPose();
            screen = SpellGuardScreen.Training;
            HintText = $"训练指令：可用握拳/V/张掌，也可用打响指/左右扇手直接施法；返回主菜单需停留 {trainingMenuHoldSeconds:F1} 秒。";
        }

        private void ReturnToMenu()
        {
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            screen = SpellGuardScreen.Menu;
            HintText = "菜单指令：食指指向移动焦点，停留确认。";
        }

        private void ResetPlayerPose()
        {
            if (playerRoot != null)
            {
                playerRoot.position = new Vector3(0f, 1.1f, 0f);
                playerRoot.rotation = Quaternion.identity;
            }
        }

        private void RefreshSpellCasterSubscription()
        {
            if (spellCaster == null || subscribed)
            {
                return;
            }

            spellCaster.SpellResolved += HandleSpellResolved;
            subscribed = true;
        }

        private void ApplyModeState()
        {
            var interactive3D = screen == SpellGuardScreen.Playing || screen == SpellGuardScreen.Training;
            if (motor != null)
            {
                motor.SetInputEnabled(interactive3D);
            }

            if (spellCaster != null)
            {
                spellCaster.SetCastingEnabled(interactive3D);
                spellCaster.SetConfirmSeconds(settings.ConfirmSeconds);
            }

            if (enemySpawner != null)
            {
                enemySpawner.SetSpawningEnabled(screen == SpellGuardScreen.Playing);
            }
        }

        private void HandleSpellResolved(SpellType spell, int hitCount)
        {
            if (screen == SpellGuardScreen.Playing)
            {
                combatCasts += 1;
                combatHits += hitCount;
                combatScore += hitCount;
            }
            else if (screen == SpellGuardScreen.Training)
            {
                trainingCasts += 1;
                lastTrainingSpell = spell;
                if (spell == SpellType.Fire) trainingFireCasts += 1;
                else if (spell == SpellType.Ice) trainingIceCasts += 1;
                else if (spell == SpellType.Shield) trainingShieldCasts += 1;
            }
        }

        private void ResetCombatStats()
        {
            combatScore = 0;
            combatHits = 0;
            combatCasts = 0;
        }

        private void ResetTrainingStats()
        {
            trainingCasts = 0;
            trainingPointerChecks = 0;
            trainingFireCasts = 0;
            trainingIceCasts = 0;
            trainingShieldCasts = 0;
            lastTrainingSpell = SpellType.None;
        }

        private void LogFlowEvent(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[Gesture][FlowReaction] {message}", this);
        }

        public string BuildOverlayText()
        {
            return screen switch
            {
                SpellGuardScreen.Menu => $"开始守卫 / 手势训练场 / 调整设置 / 查看教程\n{HintText}",
                SpellGuardScreen.Settings => $"结印确认：{settings.ConfirmLabel}\n敌人节奏：{settings.DifficultyLabel}\n{HintText}",
                SpellGuardScreen.Tutorial => "Point：转向与前进\nFist 或 Snap：火焰\nV 或 左右扇手：冰霜\nOpenPalm：护盾",
                SpellGuardScreen.Training => $"总训练：{trainingCasts}\n指向确认：{trainingPointerChecks}\n火焰/冰霜/护盾：{trainingFireCasts}/{trainingIceCasts}/{trainingShieldCasts}\n最近一次：{lastTrainingSpell.ToChinese()}",
                SpellGuardScreen.Results => $"得分：{combatScore}\n命中：{combatHits}\n施法：{combatCasts}\n命中率：{GetHitRate()}%",
                _ => $"得分：{combatScore}  命中：{combatHits} / 施法：{combatCasts}\n{HintText}",
            };
        }

        public void DrawOverlay()
        {
            RebuildRegions();
            switch (screen)
            {
                case SpellGuardScreen.Menu:
                    DrawMenu();
                    break;
                case SpellGuardScreen.Settings:
                    DrawSettings();
                    break;
                case SpellGuardScreen.Tutorial:
                    DrawTutorial();
                    break;
                case SpellGuardScreen.Training:
                    DrawTraining();
                    break;
                case SpellGuardScreen.Results:
                    DrawResults();
                    break;
            }

            DrawCursor();
        }

        private void RebuildRegions()
        {
            regionCount = 0;

            var layout = GetOverlayLayout();

            switch (screen)
            {
                case SpellGuardScreen.Menu:
                    AddRegion("start", "开始守卫", MakeButtonRect(layout, 0, 0, 4));
                    AddRegion("training", "手势训练场", MakeButtonRect(layout, 1, 0, 4));
                    AddRegion("settings", "调整设置", MakeButtonRect(layout, 2, 0, 4));
                    AddRegion("tutorial", "查看教程", MakeButtonRect(layout, 3, 0, 4));
                    break;
                case SpellGuardScreen.Settings:
                    AddRegion("confirm", $"结印确认时长：{settings.ConfirmLabel}", MakeButtonRect(layout, 0, 0, 3));
                    AddRegion("difficulty", $"敌人节奏：{settings.DifficultyLabel}", MakeButtonRect(layout, 1, 0, 3));
                    AddRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
                    break;
                case SpellGuardScreen.Tutorial:
                    AddRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
                    AddRegion("training", "进入训练场", MakeButtonRect(layout, 1, 0, 3));
                    AddRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
                    break;
                case SpellGuardScreen.Training:
                    AddRegion("pointer-check", "指向确认练习", MakeTrainingRect(layout, 0, 0));
                    AddRegion("reset-training", "重置训练计数", MakeTrainingRect(layout, 1, 0));
                    AddRegion("menu", $"长按返回主菜单（{trainingMenuHoldSeconds:F1}秒）", MakeTrainingRect(layout, 0, 1));
                    break;
                case SpellGuardScreen.Results:
                    AddRegion("restart", "再来一局", MakeButtonRect(layout, 0, 0, 2));
                    AddRegion("menu", "返回主菜单", MakeButtonRect(layout, 1, 0, 2));
                    break;
            }
        }

        private void DrawMenu()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.06f, 0.08f, 0.13f, 0.94f), new Color(0.95f, 0.68f, 0.25f, 0.92f));
            GUI.Label(layout.Title, "SPELL GUARD", overlayTitleStyle);
            GUI.Label(layout.Body, "Gesture-driven ritual defense\n选择模式并进入训练或战斗。", overlayBodyStyle);
            DrawRegion("start", "开始守卫", MakeButtonRect(layout, 0, 0, 4));
            DrawRegion("training", "手势训练场", MakeButtonRect(layout, 1, 0, 4));
            DrawRegion("settings", "调整设置", MakeButtonRect(layout, 2, 0, 4));
            DrawRegion("tutorial", "查看教程", MakeButtonRect(layout, 3, 0, 4));
        }

        private void DrawSettings()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.07f, 0.09f, 0.14f, 0.95f), new Color(0.34f, 0.56f, 1f, 0.9f));
            GUI.Label(layout.Title, "战斗设置", overlayTitleStyle);
            GUI.Label(layout.Body, "调整施法确认与敌人节奏。", overlayBodyStyle);
            DrawRegion("confirm", $"结印确认时长：{settings.ConfirmLabel}", MakeButtonRect(layout, 0, 0, 3));
            DrawRegion("difficulty", $"敌人节奏：{settings.DifficultyLabel}", MakeButtonRect(layout, 1, 0, 3));
            DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
        }

        private void DrawTutorial()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.06f, 0.08f, 0.13f, 0.95f), new Color(0.95f, 0.72f, 0.28f, 0.92f));
            GUI.Label(layout.Title, "手势教程", overlayTitleStyle);
            GUI.Label(layout.Body, "• Point：控制视角，抬高手位时前进\n• Fist 或 Snap：火焰术，正面打击目标\n• V 或 左右扇手：冰霜与节奏施法\n• OpenPalm：护盾术，提供一次防护\n• 动态动作已接入：打响指、扇手、手势切换", overlayBodyStyle);
            GUI.Label(layout.Hint, HintText, overlayHintStyle);
            DrawRegion("play", "开始守卫", MakeButtonRect(layout, 0, 0, 3));
            DrawRegion("training", "进入训练场", MakeButtonRect(layout, 1, 0, 3));
            DrawRegion("back", "返回主菜单", MakeButtonRect(layout, 2, 0, 3));
        }

        private void DrawTraining()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.05f, 0.08f, 0.13f, 0.94f), new Color(0.31f, 0.78f, 1f, 0.92f));
            GUI.Label(layout.Title, "训练场", overlayTitleStyle);
            GUI.Label(layout.Body, BuildOverlayText(), overlayBodyStyle);
            GUI.Label(layout.Hint, HintText, overlayHintStyle);
            DrawRegion("pointer-check", "指向确认练习", MakeTrainingRect(layout, 0, 0));
            DrawRegion("reset-training", "重置训练计数", MakeTrainingRect(layout, 1, 0));
            DrawRegion("menu", $"长按返回主菜单（{trainingMenuHoldSeconds:F1}秒）", MakeTrainingRect(layout, 0, 1));
        }

        private void DrawResults()
        {
            var layout = GetOverlayLayout();
            EnsureOverlayStyles(layout.Scale);
            DrawPanel(layout.Panel, new Color(0.08f, 0.08f, 0.12f, 0.95f), new Color(1f, 0.48f, 0.24f, 0.92f));
            GUI.Label(layout.Title, "战斗结束", overlayTitleStyle);
            GUI.Label(layout.Body, BuildOverlayText(), overlayBodyStyle);
            DrawRegion("restart", "再来一局", MakeButtonRect(layout, 0, 0, 2));
            DrawRegion("menu", "返回主菜单", MakeButtonRect(layout, 1, 0, 2));
        }

        private void DrawRegion(string key, string label, Rect rect)
        {
            var text = label;
            if (focusedKey == key && dwellKey == key)
            {
                var progress = Mathf.Clamp01((Time.time - dwellStartedAt) / GetRequiredHoldSeconds(key));
                text = $"{label}   {Mathf.RoundToInt(progress * 100f)}%";
            }

            var previousColor = GUI.color;
            var isFocused = focusedKey == key;
            var isHolding = focusedKey == key && dwellKey == key;
            GUI.color = isHolding ? new Color(1f, 0.68f, 0.28f, 0.95f) : (isFocused ? new Color(0.38f, 0.58f, 1f, 0.95f) : new Color(0.18f, 0.22f, 0.3f, 0.92f));
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 28f, rect.height - 18f), text, overlayButtonStyle);
        }

        private OverlayLayout GetOverlayLayout()
        {
            var width = Mathf.Max(1f, UnityEngine.Screen.width);
            var height = Mathf.Max(1f, UnityEngine.Screen.height);
            var scale = Mathf.Clamp(Mathf.Min(width / 1280f, height / 720f), 0.9f, 1.28f);
            var panelWidth = Mathf.Clamp(width * 0.34f, 340f, 520f);
            var panelHeight = Mathf.Clamp(height * 0.28f, 220f, 320f);
            var marginX = Mathf.Clamp(width * 0.03f, 18f, 40f);
            var marginY = Mathf.Clamp(height * 0.05f, 16f, 40f);
            var padding = Mathf.Clamp(18f * scale, 14f, 26f);
            var gap = Mathf.Clamp(10f * scale, 8f, 16f);

            var panelX = width - marginX - panelWidth;
            var panelY = height - marginY - panelHeight;
            var panel = new Rect(panelX, panelY, panelWidth, panelHeight);
            if (screen == SpellGuardScreen.Training)
            {
                panelWidth = Mathf.Clamp(width * 0.31f, 320f, 460f);
                panelHeight = Mathf.Clamp(height * 0.26f, 210f, 280f);
                panel = new Rect(width - marginX - panelWidth, height - marginY - panelHeight, panelWidth, panelHeight);
            }

            var content = Shrink(panel, padding, padding + 18f * scale, padding, padding);
            var title = new Rect(content.x, content.y, content.width, 30f * scale);
            var body = new Rect(content.x, content.y + 34f * scale, content.width, Mathf.Max(40f, content.height * 0.48f));
            var hint = new Rect(content.x, panel.yMax - padding - 22f * scale - 4f, content.width, 22f * scale);
            var buttonRow = new Rect(content.x, panel.yMax - padding - 48f * scale, content.width, 48f * scale);

            return new OverlayLayout
            {
                Panel = panel,
                Content = content,
                Title = title,
                Body = body,
                Hint = hint,
                ButtonsRow = buttonRow,
                Scale = scale,
                Padding = padding,
                Gap = gap,
            };
        }

        private void EnsureOverlayStyles(float scale)
        {
            if (overlayTitleStyle != null && Mathf.Abs(cachedOverlayScale - scale) < 0.01f)
            {
                return;
            }

            cachedOverlayScale = scale;

            overlayTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(24f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.97f, 1f, 1f) }
            };

            overlayBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.88f, 0.96f, 0.96f) }
            };

            overlayHintStyle = new GUIStyle(overlayBodyStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                normal = { textColor = new Color(1f, 0.84f, 0.46f, 0.98f) }
            };

            overlayButtonStyle = new GUIStyle(overlayBodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };

            overlayPanelStyle = new GUIStyle(GUI.skin.box);
        }

        private void DrawPanel(Rect rect, Color fillColor, Color accentColor)
        {
            var previousColor = GUI.color;
            GUI.color = fillColor;
            GUI.Box(rect, GUIContent.none, overlayPanelStyle ?? GUI.skin.box);
            GUI.color = accentColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private Rect MakeButtonRect(OverlayLayout layout, int column, int row, int columns)
        {
            var rows = screen == SpellGuardScreen.Training ? 2 : 1;
            var spacing = Mathf.Clamp(10f * layout.Scale, 8f, 14f);
            var height = Mathf.Clamp(46f * layout.Scale, 40f, 54f);
            var availableWidth = layout.Content.width;
            var width = columns > 1 ? (availableWidth - spacing * (columns - 1)) / columns : availableWidth;
            var y = screen == SpellGuardScreen.Training
                ? layout.Panel.yMax - layout.Padding - (height * rows) - spacing
                : layout.Panel.yMax - layout.Padding - height;
            return new Rect(layout.Content.x + (width + spacing) * column, y + (height + spacing) * row, width, height);
        }

        private Rect MakeTrainingRect(OverlayLayout layout, int column, int row)
        {
            var spacing = Mathf.Clamp(10f * layout.Scale, 8f, 14f);
            var width = (layout.Content.width - spacing) * 0.5f;
            var height = Mathf.Clamp(42f * layout.Scale, 38f, 48f);
            var baseY = layout.Panel.yMax - layout.Padding - height * 2f - spacing;
            return new Rect(layout.Content.x + (width + spacing) * column, baseY + (height + spacing) * row, width, height);
        }

        private static Rect Shrink(Rect rect, float left, float top, float right, float bottom)
        {
            return new Rect(rect.x + left, rect.y + top, Mathf.Max(1f, rect.width - left - right), Mathf.Max(1f, rect.height - top - bottom));
        }

        private void AddRegion(string key, string label, Rect rect)
        {
            if (regionCount >= regions.Length)
            {
                return;
            }

            regions[regionCount++] = new Region { key = key, label = label, rect = rect };
        }

        private void DrawCursor()
        {
            if (screen == SpellGuardScreen.Playing)
            {
                return;
            }

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            if (!snapshot.HandPresent)
            {
                return;
            }

            var x = snapshot.ViewportPosition.x * UnityEngine.Screen.width;
            var y = (1f - snapshot.ViewportPosition.y) * UnityEngine.Screen.height;
            var isPointing = snapshot.Gesture == GestureType.Point;

            var progress = 0f;
            if (!string.IsNullOrEmpty(dwellKey) && dwellKey == focusedKey)
            {
                progress = Mathf.Clamp01((Time.time - dwellStartedAt) / settings.MenuDwellSeconds);
            }

            var previousColor = GUI.color;
            GUI.color = isPointing ? new Color(1f, 0.84f, 0.35f, 0.95f) : new Color(0.7f, 0.75f, 0.82f, 0.9f);
            GUI.DrawTexture(new Rect(x - 16f, y - 16f, 32f, 32f), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            GUI.DrawTexture(new Rect(x - 11f, y - 11f, 22f, 22f), Texture2D.whiteTexture);
            GUI.color = isPointing ? new Color(1f, 0.84f, 0.35f, 1f) : new Color(0.85f, 0.9f, 0.96f, 0.95f);
            GUI.DrawTexture(new Rect(x - 3f, y - 3f, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (progress > 0f)
            {
                var requiredHold = GetRequiredHoldSeconds(dwellKey);
                GUI.Box(new Rect(x + 16f, y - 8f, 120f, 22f), $"{Mathf.RoundToInt(progress * 100f)}% / {requiredHold:F1}s");
            }
            else if (!isPointing)
            {
                GUI.Box(new Rect(x + 16f, y - 8f, 110f, 22f), snapshot.Gesture.ToChinese());
            }
        }

        private float GetRequiredHoldSeconds(string key)
        {
            if (screen == SpellGuardScreen.Training && key == "menu")
            {
                return trainingMenuHoldSeconds;
            }

            return settings != null ? settings.MenuDwellSeconds : 0.8f;
        }

        private int GetHitRate()
        {
            if (combatCasts <= 0)
            {
                return 0;
            }

            return Mathf.RoundToInt(combatHits / (float)combatCasts * 100f);
        }
    }
}

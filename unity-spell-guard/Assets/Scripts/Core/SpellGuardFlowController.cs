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

        private SpellGuardScreen screen = SpellGuardScreen.Menu;
        private string focusedKey;
        private string dwellKey;
        private float dwellStartedAt;
        private float backStartedAt;
        private readonly Region[] regions = new Region[8];
        private int regionCount;

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
            if (snapshot.HandPresent && snapshot.Gesture == GestureType.OpenPalm && screen != SpellGuardScreen.Menu && screen != SpellGuardScreen.Playing)
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

            if (Time.time - dwellStartedAt >= settings.MenuDwellSeconds)
            {
                ActivateRegion(region.Value.key);
                dwellKey = null;
                focusedKey = null;
            }
        }

        private bool HandleMotionGesture(string focusedRegionKey)
        {
            if (externalBridge == null)
            {
                return false;
            }

            var motion = externalBridge.LatestMotionGesture;
            if (!motion.IsValid || motion.TriggeredTime <= lastHandledMotionTime)
            {
                return false;
            }

            switch (motion.Gesture)
            {
                case MotionGestureType.SwipeRightToLeft:
                    lastHandledMotionTime = motion.TriggeredTime;
                    if (screen == SpellGuardScreen.Settings)
                    {
                        if (focusedRegionKey == "confirm")
                        {
                            settings.CycleConfirm();
                            HintText = $"设置已切换：施法确认 {settings.ConfirmLabel}";
                            dwellKey = null;
                            return true;
                        }

                        if (focusedRegionKey == "difficulty")
                        {
                            settings.CycleDifficulty();
                            HintText = $"设置已切换：敌人节奏 {settings.DifficultyLabel}";
                            dwellKey = null;
                            return true;
                        }
                    }

                    if (screen != SpellGuardScreen.Menu && screen != SpellGuardScreen.Playing)
                    {
                        ReturnToMenu();
                        return true;
                    }
                    break;

                case MotionGestureType.SwipeLeftToRight:
                    if (screen == SpellGuardScreen.Settings && (focusedRegionKey == "confirm" || focusedRegionKey == "difficulty"))
                    {
                        lastHandledMotionTime = motion.TriggeredTime;
                        if (focusedRegionKey == "confirm")
                        {
                            settings.CycleConfirm();
                            HintText = $"设置已切换：施法确认 {settings.ConfirmLabel}";
                        }
                        else
                        {
                            settings.CycleDifficulty();
                            HintText = $"设置已切换：敌人节奏 {settings.DifficultyLabel}";
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
            HintText = "战斗指令：Point转向并抬高手位前进；握拳/ V / 张掌施法。";
        }

        private void StartTraining()
        {
            ResetTrainingStats();
            playerHealth?.ResetHealth();
            enemySpawner?.ClearAll();
            gameFlow?.ResetGameOver();
            ResetPlayerPose();
            screen = SpellGuardScreen.Training;
            HintText = "训练指令：直接切换握拳 / V / 张掌，不必先收势。";
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

        public string BuildOverlayText()
        {
            return screen switch
            {
                SpellGuardScreen.Menu => $"主菜单\n开始守卫 / 手势训练场 / 调整设置 / 查看教程\n{HintText}",
                SpellGuardScreen.Settings => $"设置\n结印确认：{settings.ConfirmLabel}\n敌人节奏：{settings.DifficultyLabel}\n{HintText}",
                SpellGuardScreen.Tutorial => "教程\nPoint：转向与前进\nFist：火焰\nV：冰霜\nOpenPalm：护盾",
                SpellGuardScreen.Training => $"训练场\n总训练：{trainingCasts}\n指向确认：{trainingPointerChecks}\n火焰/冰霜/护盾：{trainingFireCasts}/{trainingIceCasts}/{trainingShieldCasts}\n最近一次：{lastTrainingSpell.ToChinese()}",
                SpellGuardScreen.Results => $"结果页\n得分：{combatScore}\n命中：{combatHits}\n施法：{combatCasts}\n命中率：{GetHitRate()}%",
                _ => $"战斗中\n得分：{combatScore}  命中：{combatHits} / 施法：{combatCasts}\n{HintText}",
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

            switch (screen)
            {
                case SpellGuardScreen.Menu:
                    AddRegion("start", "开始守卫", new Rect(150, 150, 460, 48));
                    AddRegion("training", "手势训练场", new Rect(150, 208, 460, 48));
                    AddRegion("settings", "调整设置", new Rect(150, 266, 460, 48));
                    AddRegion("tutorial", "查看教程", new Rect(150, 324, 460, 48));
                    break;
                case SpellGuardScreen.Settings:
                    AddRegion("confirm", $"结印确认时长：{settings.ConfirmLabel}", new Rect(150, 160, 460, 48));
                    AddRegion("difficulty", $"敌人节奏：{settings.DifficultyLabel}", new Rect(150, 218, 460, 48));
                    AddRegion("back", "返回主菜单", new Rect(150, 276, 460, 48));
                    break;
                case SpellGuardScreen.Tutorial:
                    AddRegion("play", "开始守卫", new Rect(110, 300, 170, 48));
                    AddRegion("training", "进入训练场", new Rect(305, 300, 170, 48));
                    AddRegion("back", "返回主菜单", new Rect(500, 300, 170, 48));
                    break;
                case SpellGuardScreen.Training:
                    AddRegion("pointer-check", "指向确认练习", new Rect(40, 170, 180, 40));
                    AddRegion("reset-training", "重置训练计数", new Rect(230, 170, 180, 40));
                    AddRegion("menu", "返回主菜单", new Rect(40, 220, 370, 40));
                    break;
                case SpellGuardScreen.Results:
                    AddRegion("restart", "再来一局", new Rect(210, 250, 160, 48));
                    AddRegion("menu", "返回主菜单", new Rect(390, 250, 160, 48));
                    break;
            }
        }

        private void DrawMenu()
        {
            GUI.Box(new Rect(120, 100, 520, 300), "符印守卫");
            DrawRegion("start", "开始守卫", new Rect(150, 150, 460, 48));
            DrawRegion("training", "手势训练场", new Rect(150, 208, 460, 48));
            DrawRegion("settings", "调整设置", new Rect(150, 266, 460, 48));
            DrawRegion("tutorial", "查看教程", new Rect(150, 324, 460, 48));
        }

        private void DrawSettings()
        {
            GUI.Box(new Rect(120, 100, 520, 260), "设置");
            DrawRegion("confirm", $"结印确认时长：{settings.ConfirmLabel}", new Rect(150, 160, 460, 48));
            DrawRegion("difficulty", $"敌人节奏：{settings.DifficultyLabel}", new Rect(150, 218, 460, 48));
            DrawRegion("back", "返回主菜单", new Rect(150, 276, 460, 48));
        }

        private void DrawTutorial()
        {
            GUI.Box(new Rect(80, 70, 620, 360), "手势教程");
            GUI.Label(new Rect(110, 120, 560, 120), "食指指向：控制视角方向；抬高手位时前进。\n握拳：火焰术，直接打击前方目标。\nV 手势：冰霜术，冻结前方威胁。\n张掌：护盾术，提供一次保命护盾。\n同一手势不会连发，切到别的手势可直接续接。");
            DrawRegion("play", "开始守卫", new Rect(110, 300, 170, 48));
            DrawRegion("training", "进入训练场", new Rect(305, 300, 170, 48));
            DrawRegion("back", "返回主菜单", new Rect(500, 300, 170, 48));
        }

        private void DrawTraining()
        {
            GUI.Box(new Rect(20, 20, 420, 220), "训练场");
            GUI.Label(new Rect(40, 60, 380, 100), BuildOverlayText());
            DrawRegion("pointer-check", "指向确认练习", new Rect(40, 170, 180, 40));
            DrawRegion("reset-training", "重置训练计数", new Rect(230, 170, 180, 40));
            DrawRegion("menu", "返回主菜单", new Rect(40, 220, 370, 40));
        }

        private void DrawResults()
        {
            GUI.Box(new Rect(180, 110, 420, 230), "战斗结束");
            GUI.Label(new Rect(210, 150, 360, 80), BuildOverlayText());
            DrawRegion("restart", "再来一局", new Rect(210, 250, 160, 48));
            DrawRegion("menu", "返回主菜单", new Rect(390, 250, 160, 48));
        }

        private void DrawRegion(string key, string label, Rect rect)
        {
            var text = label;
            if (focusedKey == key && dwellKey == key)
            {
                var progress = Mathf.Clamp01((Time.time - dwellStartedAt) / settings.MenuDwellSeconds);
                text = $"> {label} {Mathf.RoundToInt(progress * 100f)}%";
            }
            else if (focusedKey == key)
            {
                text = $"> {label}";
            }

            GUI.Box(rect, text);
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
                GUI.Box(new Rect(x + 16f, y - 8f, 80f, 22f), $"{Mathf.RoundToInt(progress * 100f)}%");
            }
            else if (!isPointing)
            {
                GUI.Box(new Rect(x + 16f, y - 8f, 110f, 22f), snapshot.Gesture.ToChinese());
            }
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

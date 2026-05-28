using SpellGuard.Audio;
using SpellGuard.Combat;
using SpellGuard.InputSystem;
using System;
using UnityEngine;

namespace SpellGuard.Player
{
    public class GestureSpellCaster : MonoBehaviour
    {
        [SerializeField] private GestureInputProviderBase inputProvider;
        [SerializeField] private Camera castCamera;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private float confirmSeconds = 0.4f;
        [SerializeField] private float castDistance = 50f;
        [SerializeField] private float castStatusHoldSeconds = 0.75f;
        [SerializeField] private float projectileSpeed = 18f;
        [SerializeField] private float projectileLifetime = 2.5f;
        [SerializeField] private float shieldCounterRadius = 4.2f;
        [SerializeField] private int shieldCounterDamage = 1;
        [SerializeField] private float shieldCounterFreezeSeconds = 0.55f;
        [SerializeField] private int selectedFireVariantIndex;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private bool debugLogs = true;

        private SpellType pendingSpell = SpellType.None;
        private float pendingStartTime;
        private SpellType lastCastSpell = SpellType.None;
        private float lastHandledMotionTime = -999f;
        private float statusHoldUntil;
        private bool castingEnabled = true;

        private string LastCastSourceLabel => lastHandledMotionTime > Time.time - 0.05f ? "dynamic-motion" : "static-snapshot";

        public SpellType PendingSpell => pendingSpell;
        public float PendingProgress { get; private set; }
        public SpellType LastCastSpell => lastCastSpell;
        public string SelectedFireName => SpellConfigLibrary.GetFireVariant(selectedFireVariantIndex).DisplayName;
        public Color SelectedFireColor => SpellConfigLibrary.GetFireVariant(selectedFireVariantIndex).Color;
        public string StatusText { get; private set; } = "\u7b49\u5f85\u624b\u52bf";
        public string SpellPromptText => BuildSpellPromptText();
        public string LastSpellFeedbackText { get; private set; } = "\u5c1a\u672a\u65bd\u6cd5";
        public event Action<SpellType, int> SpellResolved;

        public SpellConfig GetSpellConfig(SpellType spellType)
        {
            return spellType == SpellType.Fire ? SpellConfigLibrary.GetFireVariant(selectedFireVariantIndex) : SpellConfigLibrary.Get(spellType);
        }

        public void SetConfirmSeconds(float value)
        {
            confirmSeconds = Mathf.Max(0.1f, value);
        }

        public void SetCastingEnabled(bool value)
        {
            castingEnabled = value;
            if (!value)
            {
                pendingSpell = SpellType.None;
                PendingProgress = 0f;
                lastCastSpell = SpellType.None;
                statusHoldUntil = 0f;
                StatusText = "\u65bd\u6cd5\u5df2\u6682\u505c";
            }
        }

        private void Update()
        {
            if (!castingEnabled)
            {
                return;
            }

            if (TryCastFromKeyboard())
            {
                return;
            }

            var snapshot = inputProvider != null ? inputProvider.CurrentSnapshot : GestureSnapshot.Missing;
            var action = inputProvider != null ? inputProvider.CurrentCustomAction : GestureAction.None;
            if (!action.IsValid && inputProvider != null)
            {
                action = inputProvider.CurrentComboAction;
            }
            if (!action.IsValid && inputProvider != null)
            {
                action = inputProvider.CurrentSpellAction;
            }

            if (TryCastFromAction(action))
            {
                return;
            }

            var spell = MapIntentToSpell(action.Intent);

            if (!snapshot.HandPresent || !action.IsValid || action.IsTransient || spell == SpellType.None)
            {
                if (Time.time < statusHoldUntil)
                {
                    return;
                }

                pendingSpell = SpellType.None;
                PendingProgress = 0f;
                lastCastSpell = SpellType.None;
                StatusText = snapshot.HandPresent ? "\u5f53\u524d\u624b\u52bf\u65e0\u65bd\u6cd5" : "\u672a\u68c0\u6d4b\u5230\u624b";
                return;
            }

            if (lastCastSpell == spell)
            {
                pendingSpell = SpellType.None;
                PendingProgress = 0f;
                StatusText = $"{spell.ToChinese()}\u4fdd\u6301\u4e2d\uff0c\u5207\u6362\u624b\u52bf\u53ef\u7ee7\u7eed";
                return;
            }

            if (pendingSpell != spell)
            {
                pendingSpell = spell;
                pendingStartTime = Time.time;
                PendingProgress = 0f;
            }
            else
            {
                PendingProgress = Mathf.Clamp01((Time.time - pendingStartTime) / confirmSeconds);
            }

            StatusText = $"{spell.ToChinese()}\u786e\u8ba4\u4e2d {Mathf.RoundToInt(PendingProgress * 100f)}%";

            if (PendingProgress >= 1f)
            {
                Cast(spell);
                lastCastSpell = spell;
                pendingSpell = SpellType.None;
                PendingProgress = 0f;
            }
        }

        private void Cast(SpellType spell)
        {
            var hitCount = 0;
            var spellConfig = GetSpellConfig(spell);
            SpellGuardAudioController.Instance?.PlaySpellCastSfx(spell);
            switch (spell)
            {
                case SpellType.Fire:
                    LaunchFireProjectile(spellConfig);
                    StatusText = $"{spellConfig.DisplayName}\u5df2\u53d1\u5c04";
                    LastSpellFeedbackText = $"{spellConfig.DisplayName}\u706b\u7130\u5f39\u98de\u51fa";
                    break;
                case SpellType.Ice:
                    hitCount = TryHitEnemy(enemy => enemy.ApplyFreeze(spellConfig.FreezeDuration));
                    StatusText = "\u51b0\u971c\u672f\u5df2\u91ca\u653e";
                    LastSpellFeedbackText = hitCount > 0 ? "\u51b0\u971c\u547d\u4e2d\uff1a\u654c\u4eba\u8fdb\u5165\u51bb\u7ed3\u53cd\u9988" : "\u51b0\u971c\u672a\u547d\u4e2d\uff1a\u8bf7\u5bf9\u51c6\u654c\u4eba";
                    break;
                case SpellType.Shield:
                    if (playerHealth != null)
                    {
                        playerHealth.ActivateShield(spellConfig.ShieldDuration);
                    }
                    StatusText = "\u62a4\u76fe\u672f\u5df2\u91ca\u653e";
                    LastSpellFeedbackText = $"\u62a4\u76fe\u542f\u52a8\uff1a\u6301\u7eed {spellConfig.ShieldDuration:F1}s";
                    break;
            }

            SpellResolved?.Invoke(spell, hitCount);
            statusHoldUntil = Time.time + castStatusHoldSeconds;

            if (debugLogs)
            {
                Debug.Log($"[Gesture][GameplayReaction] spellCast={spell} hitCount={hitCount} source={LastCastSourceLabel}", this);
            }
        }

        private bool TryCastFromKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetMouseButtonDown(0))
            {
                Cast(SpellType.Fire);
                lastCastSpell = SpellType.Fire;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                selectedFireVariantIndex = (selectedFireVariantIndex + SpellConfigLibrary.GetFireVariantCount() - 1) % SpellConfigLibrary.GetFireVariantCount();
                StatusText = $"\u5df2\u5207\u6362\u706b\u7130\uff1a{SelectedFireName}";
                statusHoldUntil = Time.time + castStatusHoldSeconds;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                selectedFireVariantIndex = (selectedFireVariantIndex + 1) % SpellConfigLibrary.GetFireVariantCount();
                StatusText = $"\u5df2\u5207\u6362\u706b\u7130\uff1a{SelectedFireName}";
                statusHoldUntil = Time.time + castStatusHoldSeconds;
                return true;
            }

            return false;
        }

        private bool TryCastFromAction(GestureAction action)
        {
            if (!action.IsValid || !action.IsTransient || action.TriggeredTime <= lastHandledMotionTime)
            {
                return false;
            }

            var spell = MapIntentToSpell(action.Intent);
            if (spell == SpellType.None)
            {
                return false;
            }

            lastHandledMotionTime = action.TriggeredTime;
            if (debugLogs)
            {
                Debug.Log($"[Gesture][SpellInput] intent={action.Intent} mappedSpell={spell} confidence={action.Confidence:F2}", this);
            }

            pendingSpell = SpellType.None;
            PendingProgress = 0f;
            lastCastSpell = spell;
            if (IsShieldCounterMotion(action))
            {
                CastShieldCounter();
            }
            else
            {
                Cast(spell);
                StatusText = BuildDynamicCastStatus(action, spell);
            }

            statusHoldUntil = Time.time + castStatusHoldSeconds;
            return true;
        }

        private bool IsShieldCounterMotion(GestureAction action)
        {
            return action.Intent == GestureIntent.CastShield
                   && action.SourceKind == GestureCommandKind.Motion
                   && inputProvider != null
                   && IsOpenPalmSlap(inputProvider.CurrentMotionGesture.Gesture);
        }

        private static bool IsOpenPalmSlap(MotionGestureType gesture)
        {
            return gesture == MotionGestureType.OpenPalmSlapLeftToRight || gesture == MotionGestureType.OpenPalmSlapRightToLeft;
        }

        private static string BuildDynamicCastStatus(GestureAction action, SpellType spell)
        {
            if (action.Intent == GestureIntent.CastFire && spell == SpellType.Fire)
            {
                return "\u5feb\u901f\u706b\u7130\u5df2\u91ca\u653e";
            }

            return $"{spell.ToChinese()}\u5df2\u901a\u8fc7\u52a8\u6001\u624b\u52bf\u91ca\u653e";
        }

        private void CastShieldCounter()
        {
            var spellConfig = GetSpellConfig(SpellType.Shield);
            SpellGuardAudioController.Instance?.PlaySpellCastSfx(SpellType.Shield);
            if (playerHealth != null)
            {
                playerHealth.ActivateShield(Mathf.Max(spellConfig.ShieldDuration, shieldCounterFreezeSeconds));
            }

            var hitCount = ApplyShieldCounterToNearbyEnemies();
            StatusText = "\u62a4\u76fe\u53cd\u51fb\u5df2\u89e6\u53d1";
            LastSpellFeedbackText = hitCount > 0
                ? $"\u62a4\u76fe\u53cd\u51fb\u547d\u4e2d {hitCount} \u4e2a\u654c\u4eba"
                : "\u62a4\u76fe\u53cd\u51fb\u5c55\u5f00\uff1a\u8fd1\u8eab\u654c\u4eba\u4f1a\u88ab\u9707\u9000";
            SpellResolved?.Invoke(SpellType.Shield, hitCount);
            statusHoldUntil = Time.time + castStatusHoldSeconds;

            if (debugLogs)
            {
                Debug.Log($"[Gesture][GameplayReaction] shieldCounter hitCount={hitCount} radius={shieldCounterRadius:F1}", this);
            }
        }

        private int ApplyShieldCounterToNearbyEnemies()
        {
            var colliders = Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, shieldCounterRadius), hitMask, QueryTriggerInteraction.Ignore);
            var hitEnemies = new System.Collections.Generic.HashSet<SimpleEnemyController>();
            for (var index = 0; index < colliders.Length; index++)
            {
                var enemy = colliders[index] != null ? colliders[index].GetComponentInParent<SimpleEnemyController>() : null;
                if (enemy == null || !hitEnemies.Add(enemy))
                {
                    continue;
                }

                enemy.ApplyFreeze(shieldCounterFreezeSeconds);
                enemy.ApplyDamage(shieldCounterDamage);
            }

            return hitEnemies.Count;
        }

        private void LaunchFireProjectile(SpellConfig spellConfig)
        {
            if (castCamera == null)
            {
                return;
            }

            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = $"{spellConfig.DisplayName}_Projectile";
            projectileObject.transform.position = castCamera.transform.position + castCamera.transform.forward * 0.7f;
            projectileObject.transform.localScale = Vector3.one * 0.28f;

            var renderer = projectileObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = spellConfig.Color;
            }

            var light = projectileObject.AddComponent<Light>();
            light.color = spellConfig.Color;
            light.range = 2.2f;
            light.intensity = 2.5f;

            var projectile = projectileObject.AddComponent<FireSpellProjectile>();
            projectile.Initialize(castCamera.transform.forward, projectileSpeed, projectileLifetime, spellConfig.Damage, castDistance, hitMask);
        }

        private int TryHitEnemy(System.Action<SimpleEnemyController> effect)
        {
            if (castCamera == null)
            {
                return 0;
            }

            if (Physics.Raycast(castCamera.transform.position, castCamera.transform.forward, out var hit, castDistance, hitMask, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<SimpleEnemyController>();
                if (enemy != null)
                {
                    effect(enemy);
                    return 1;
                }
            }

            return 0;
        }

        private static SpellType MapIntentToSpell(GestureIntent intent)
        {
            switch (intent)
            {
                case GestureIntent.CastFire:
                    return SpellType.Fire;
                case GestureIntent.CastIce:
                    return SpellType.Ice;
                case GestureIntent.CastShield:
                    return SpellType.Shield;
                default:
                    return SpellType.None;
            }
        }

        private string BuildSpellPromptText()
        {
            if (!castingEnabled)
            {
                return "\u6cd5\u672f\u6682\u505c\uff1a\u83dc\u5355\u6216\u7ed3\u7b97\u72b6\u6001";
            }

            if (pendingSpell != SpellType.None)
            {
                return $"\u786e\u8ba4\u4e2d\uff1a{pendingSpell.ToChinese()} {Mathf.RoundToInt(PendingProgress * 100f)}%";
            }

            if (lastCastSpell != SpellType.None)
            {
                return $"\u6700\u8fd1\u91ca\u653e\uff1a{lastCastSpell.ToChinese()} - {LastSpellFeedbackText}";
            }

            return $"\u706b\u7130\u5360\u4f4d\uff1a{SelectedFireName} - \u5de6\u952e/1 \u53d1\u5c04\u706b\u7130\u5f39 - Q/R \u5207\u6362\u4e03\u8272\u706b\u7130";
        }
    }
}

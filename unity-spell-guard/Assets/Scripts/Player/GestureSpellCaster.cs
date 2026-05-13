using SpellGuard.Combat;
using SpellGuard.Audio;
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
        public string StatusText { get; private set; } = "等待手势";
        public string SpellPromptText => BuildSpellPromptText();
        public string LastSpellFeedbackText { get; private set; } = "尚未施法";
        public event Action<SpellType, int> SpellResolved;

        public SpellConfig GetSpellConfig(SpellType spellType)
        {
            return SpellConfigLibrary.Get(spellType);
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
                StatusText = "施法已暂停";
            }
        }

        private void Update()
        {
            if (!castingEnabled)
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
                StatusText = snapshot.HandPresent ? "当前手势无施法" : "未检测到手";
                return;
            }

            if (lastCastSpell == spell)
            {
                pendingSpell = SpellType.None;
                PendingProgress = 0f;
                StatusText = $"{spell.ToChinese()}保持中，切换手势可继续";
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

            StatusText = $"{spell.ToChinese()}确认中 {Mathf.RoundToInt(PendingProgress * 100f)}%";

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
                    hitCount = TryHitEnemy(enemy => enemy.ApplyDamage(spellConfig.Damage));
                    StatusText = "火焰术已释放";
                    LastSpellFeedbackText = hitCount > 0 ? "火焰命中：敌人受到伤害" : "火焰未命中：请对准敌人";
                    break;
                case SpellType.Ice:
                    hitCount = TryHitEnemy(enemy => enemy.ApplyFreeze(spellConfig.FreezeDuration));
                    StatusText = "冰霜术已释放";
                    LastSpellFeedbackText = hitCount > 0 ? "冰霜命中：敌人进入冻结反馈" : "冰霜未命中：请对准敌人";
                    break;
                case SpellType.Shield:
                    if (playerHealth != null)
                    {
                        playerHealth.ActivateShield(spellConfig.ShieldDuration);
                    }
                    StatusText = "护盾术已释放";
                    LastSpellFeedbackText = $"护盾启动：持续 {spellConfig.ShieldDuration:F1}s";
                    break;
            }

            SpellResolved?.Invoke(spell, hitCount);
            statusHoldUntil = Time.time + castStatusHoldSeconds;

            if (debugLogs)
            {
                Debug.Log($"[Gesture][GameplayReaction] spellCast={spell} hitCount={hitCount} source={LastCastSourceLabel}", this);
            }
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
            Cast(spell);
            StatusText = $"{spell.ToChinese()}已通过动态手势释放";
            statusHoldUntil = Time.time + castStatusHoldSeconds;
            return true;
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
                return "法术暂停：菜单或结算状态";
            }

            if (pendingSpell != SpellType.None)
            {
                return $"确认中：{pendingSpell.ToChinese()} {Mathf.RoundToInt(PendingProgress * 100f)}%";
            }

            if (lastCastSpell != SpellType.None)
            {
                return $"最近释放：{lastCastSpell.ToChinese()} · {LastSpellFeedbackText}";
            }

            return "火焰 / 冰霜 / 护盾：等待有效手势";
        }
    }
}

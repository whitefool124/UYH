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
        [SerializeField] private float shieldDuration = 3f;
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
        public event Action<SpellType, int> SpellResolved;

        public void Configure(GestureInputProviderBase provider, Camera cameraRef, PlayerHealth health)
        {
            inputProvider = provider;
            castCamera = cameraRef;
            playerHealth = health;
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
            var command = inputProvider != null ? inputProvider.CurrentGestureCommand : GestureCommand.None;

            if (TryCastFromCommand(command))
            {
                return;
            }

            var spell = MapGestureToSpell(snapshot.Gesture);

            if (!snapshot.HandPresent || spell == SpellType.None)
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
            switch (spell)
            {
                case SpellType.Fire:
                    hitCount = TryHitEnemy(enemy => enemy.ApplyDamage(1));
                    StatusText = "火焰术已释放";
                    break;
                case SpellType.Ice:
                    hitCount = TryHitEnemy(enemy => enemy.ApplyFreeze(2.5f));
                    StatusText = "冰霜术已释放";
                    break;
                case SpellType.Shield:
                    if (playerHealth != null)
                    {
                        playerHealth.ActivateShield(shieldDuration);
                    }
                    StatusText = "护盾术已释放";
                    break;
            }

            SpellResolved?.Invoke(spell, hitCount);
            statusHoldUntil = Time.time + castStatusHoldSeconds;

            if (debugLogs)
            {
                Debug.Log($"[Gesture][GameplayReaction] spellCast={spell} hitCount={hitCount} source={LastCastSourceLabel}", this);
            }
        }

        private bool TryCastFromCommand(GestureCommand command)
        {
            if (!command.IsValid || command.Kind != GestureCommandKind.Motion || command.TriggeredTime <= lastHandledMotionTime)
            {
                return false;
            }

            var spell = MapMotionToSpell(command.MotionGesture);
            if (spell == SpellType.None)
            {
                return false;
            }

            lastHandledMotionTime = command.TriggeredTime;
            if (debugLogs)
            {
                Debug.Log($"[Gesture][SpellInput] motionGesture={command.MotionGesture} mappedSpell={spell} confidence={command.Confidence:F2}", this);
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

        private static SpellType MapGestureToSpell(GestureType gesture)
        {
            switch (gesture)
            {
                case GestureType.Fist:
                    return SpellType.Fire;
                case GestureType.VSign:
                    return SpellType.Ice;
                case GestureType.OpenPalm:
                    return SpellType.Shield;
                default:
                    return SpellType.None;
            }
        }

        private static SpellType MapMotionToSpell(MotionGestureType gesture)
        {
            switch (gesture)
            {
                case MotionGestureType.Snap:
                case MotionGestureType.PointToFist:
                    return SpellType.Fire;
                case MotionGestureType.SwipeLeftToRight:
                case MotionGestureType.OpenPalmSlapLeftToRight:
                    return SpellType.Ice;
                case MotionGestureType.SwipeRightToLeft:
                case MotionGestureType.OpenPalmSlapRightToLeft:
                    return SpellType.Shield;
                default:
                    return SpellType.None;
            }
        }
    }
}

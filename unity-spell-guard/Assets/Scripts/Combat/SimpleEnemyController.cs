using System;
using UnityEngine;
using SpellGuard.Audio;

namespace SpellGuard.Combat
{
    public class SimpleEnemyController : MonoBehaviour
    {
        [SerializeField] private float speed = 0f;
        [SerializeField] private int hitPoints = EnemyConfig.Default.HitPoints;
        [SerializeField] private float attackDistance = 0f;
        [Header("Visual Feedback")]
        [SerializeField] private Renderer[] feedbackRenderers;
        [SerializeField] private Color normalTint = new Color(0.84f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color hitTint = new Color(1f, 0.72f, 0.22f, 1f);
        [SerializeField] private Color frozenTint = new Color(0.36f, 0.82f, 1f, 1f);
        [SerializeField] private float hitFlashSeconds = 0.18f;
        [Header("Health Bar")]
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.9f, 0f);
        [SerializeField] private Vector2 healthBarSize = new Vector2(0.8f, 0.12f);
        [SerializeField] private Color healthBarBgColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color healthBarFillColor = new Color(1f, 0.32f, 0.18f, 0.95f);

        private Transform target;
        private PlayerHealth playerHealth;
        private float frozenUntil;
        private float hitFlashUntil;
        private MaterialPropertyBlock propertyBlock;
        private bool deathNotified;

        public static event Action<SimpleEnemyController> AnyEnemyDied;
        public float Speed => speed;
        public float AttackDistance => attackDistance;
        public bool IsFrozen => Time.time < frozenUntil;
        public string FeedbackState => IsFrozen ? "Frozen" : Time.time < hitFlashUntil ? "HitFlash" : "Normal";
        public float HealthPercent => hitPoints <= 0 ? 0f : Mathf.Clamp01((float)hitPoints / Mathf.Max(1f, EnemyConfig.Default.HitPoints));

        private void Awake()
        {
            if (feedbackRenderers == null || feedbackRenderers.Length == 0)
            {
                feedbackRenderers = GetComponentsInChildren<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            ApplyTint(normalTint);
        }

        public void Initialize(Transform targetTransform, PlayerHealth player)
        {
            target = targetTransform;
            playerHealth = player;
        }

        public void ApplyConfig(EnemyConfig config)
        {
            speed = Mathf.Max(0f, config.Speed);
            hitPoints = Mathf.Max(1, config.HitPoints);
            attackDistance = Mathf.Max(0f, config.AttackDistance);
        }

        private void Update()
        {
            UpdateVisualFeedback();
            if (target == null || playerHealth == null || !playerHealth.IsAlive)
            {
                return;
            }

            if (Time.time < frozenUntil)
            {
                return;
            }

            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f && speed > 0f)
            {
                var direction = toTarget.normalized;
                transform.position += direction * speed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        private void OnGUI()
        {
            if (hitPoints <= 0)
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var screenPoint = cam.WorldToScreenPoint(transform.position + healthBarOffset);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            var width = Mathf.Max(40f, healthBarSize.x * 100f);
            var height = Mathf.Max(6f, healthBarSize.y * 100f);
            var rect = new Rect(screenPoint.x - width * 0.5f, Screen.height - screenPoint.y - height * 0.5f, width, height);
            GUI.color = healthBarBgColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = healthBarFillColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * HealthPercent, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public void ApplyDamage(int amount)
        {
            hitPoints -= Mathf.Max(1, amount);
            hitFlashUntil = Time.time + Mathf.Max(0.05f, hitFlashSeconds);
            ApplyTint(hitTint);
            SpellGuardAudioController.Instance?.PlayEnemyHitSfx();
            if (hitPoints <= 0)
            {
                NotifyDeath();
                Destroy(gameObject);
            }
        }

        public void ApplyFreeze(float duration)
        {
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
            ApplyTint(frozenTint);
            SpellGuardAudioController.Instance?.PlayFreezeSfx();
        }

        private void NotifyDeath()
        {
            if (deathNotified)
            {
                return;
            }

            deathNotified = true;
            AnyEnemyDied?.Invoke(this);
        }

        private void UpdateVisualFeedback()
        {
            if (Time.time < frozenUntil)
            {
                ApplyTint(frozenTint);
                return;
            }

            ApplyTint(Time.time < hitFlashUntil ? hitTint : normalTint);
        }

        private void ApplyTint(Color tint)
        {
            if (feedbackRenderers == null)
            {
                return;
            }

            foreach (var feedbackRenderer in feedbackRenderers)
            {
                if (feedbackRenderer == null || feedbackRenderer.sharedMaterial == null)
                {
                    continue;
                }

                feedbackRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", tint);
                propertyBlock.SetColor("_BaseColor", tint);
                feedbackRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}

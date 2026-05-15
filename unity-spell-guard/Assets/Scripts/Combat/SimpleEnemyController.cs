using UnityEngine;
using SpellGuard.Audio;

namespace SpellGuard.Combat
{
    public class SimpleEnemyController : MonoBehaviour
    {
        [SerializeField] private float speed = EnemyConfig.Default.Speed;
        [SerializeField] private int hitPoints = EnemyConfig.Default.HitPoints;
        [SerializeField] private float attackDistance = EnemyConfig.Default.AttackDistance;
        [Header("Visual Feedback")]
        [SerializeField] private Renderer[] feedbackRenderers;
        [SerializeField] private Color normalTint = new Color(0.84f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color hitTint = new Color(1f, 0.72f, 0.22f, 1f);
        [SerializeField] private Color frozenTint = new Color(0.36f, 0.82f, 1f, 1f);
        [SerializeField] private float hitFlashSeconds = 0.18f;

        private Transform target;
        private PlayerHealth playerHealth;
        private float frozenUntil;
        private float hitFlashUntil;
        private MaterialPropertyBlock propertyBlock;

        public int CurrentHitPoints => hitPoints;
        public float Speed => speed;
        public float AttackDistance => attackDistance;
        public bool IsFrozen => Time.time < frozenUntil;
        public string FeedbackState => IsFrozen ? "Frozen" : Time.time < hitFlashUntil ? "HitFlash" : "Normal";

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
            speed = config.Speed;
            hitPoints = config.HitPoints;
            attackDistance = config.AttackDistance;
        }

        private void Update()
        {
            UpdateVisualFeedback();

            if (target == null || playerHealth == null || !playerHealth.IsAlive)
            {
                return;
            }

            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;

            if (distance <= attackDistance)
            {
                playerHealth.ApplyHit(1);
                Destroy(gameObject);
                return;
            }

            if (Time.time < frozenUntil)
            {
                return;
            }

            if (distance > 0.01f)
            {
                var direction = toTarget.normalized;
                transform.position += direction * speed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        public void ApplyDamage(int amount)
        {
            hitPoints -= amount;
            hitFlashUntil = Time.time + Mathf.Max(0.05f, hitFlashSeconds);
            ApplyTint(hitTint);
            SpellGuardAudioController.Instance?.PlayEnemyHitSfx();
            if (hitPoints <= 0)
            {
                Destroy(gameObject);
            }
        }

        public void ApplyFreeze(float duration)
        {
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
            ApplyTint(frozenTint);
            SpellGuardAudioController.Instance?.PlayFreezeSfx();
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

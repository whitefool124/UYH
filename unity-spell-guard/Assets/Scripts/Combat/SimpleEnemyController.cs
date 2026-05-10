using UnityEngine;
using SpellGuard.Audio;

namespace SpellGuard.Combat
{
    public class SimpleEnemyController : MonoBehaviour
    {
        [SerializeField] private float speed = EnemyConfig.Default.Speed;
        [SerializeField] private int hitPoints = EnemyConfig.Default.HitPoints;
        [SerializeField] private float attackDistance = EnemyConfig.Default.AttackDistance;

        private Transform target;
        private PlayerHealth playerHealth;
        private float frozenUntil;

        public int CurrentHitPoints => hitPoints;
        public float Speed => speed;
        public float AttackDistance => attackDistance;

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
            SpellGuardAudioController.Instance?.PlayEnemyHitSfx();
            if (hitPoints <= 0)
            {
                Destroy(gameObject);
            }
        }

        public void ApplyFreeze(float duration)
        {
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
            SpellGuardAudioController.Instance?.PlayFreezeSfx();
        }
    }
}

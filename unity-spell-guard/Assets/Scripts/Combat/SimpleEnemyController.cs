using UnityEngine;

namespace SpellGuard.Combat
{
    public class SimpleEnemyController : MonoBehaviour
    {
        [SerializeField] private float speed = 2.2f;
        [SerializeField] private int hitPoints = 2;
        [SerializeField] private float attackDistance = 1.4f;

        private Transform target;
        private PlayerHealth playerHealth;
        private float frozenUntil;

        public void Initialize(Transform targetTransform, PlayerHealth player)
        {
            target = targetTransform;
            playerHealth = player;
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
            if (hitPoints <= 0)
            {
                Destroy(gameObject);
            }
        }

        public void ApplyFreeze(float duration)
        {
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
        }
    }
}

using SpellGuard.Combat;
using UnityEngine;

namespace SpellGuard.Player
{
    [DisallowMultipleComponent]
    public class FireSpellProjectile : MonoBehaviour
    {
        [SerializeField] private float visualScale = 0.28f;
        [SerializeField] private float trailTime = 0.18f;
        [SerializeField] private Color glowColor = new Color(1f, 0.42f, 0.08f, 1f);
        private Vector3 direction;
        private float speed;
        private float remainingLifetime;
        private int damage;
        private float remainingDistance;
        private LayerMask hitMask;
        private bool initialized;
        private TrailRenderer trail;

        public void Initialize(Vector3 castDirection, float projectileSpeed, float lifetime, int damageAmount, float maxDistance, LayerMask mask)
        {
            direction = castDirection.sqrMagnitude > 0.0001f ? castDirection.normalized : Vector3.forward;
            speed = Mathf.Max(1f, projectileSpeed);
            remainingLifetime = Mathf.Max(0.1f, lifetime);
            damage = Mathf.Max(1, damageAmount);
            remainingDistance = Mathf.Max(1f, maxDistance);
            hitMask = mask;
            initialized = true;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.localScale = Vector3.one * Mathf.Max(0.08f, visualScale);

            trail ??= gameObject.AddComponent<TrailRenderer>();
            trail.time = Mathf.Max(0.05f, trailTime);
            trail.startWidth = 0.18f;
            trail.endWidth = 0.01f;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                trail.material = new Material(shader);
            }
            trail.startColor = glowColor;
            trail.endColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

            var light = GetComponent<Light>() ?? gameObject.AddComponent<Light>();
            light.color = glowColor;
            light.range = 3f;
            light.intensity = 2.4f;
        }

        private void Update()
        {
            if (!initialized)
            {
                Destroy(gameObject);
                return;
            }

            var frameDistance = speed * Time.deltaTime;
            if (Physics.Raycast(transform.position, direction, out var hit, frameDistance, hitMask, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<SimpleEnemyController>();
                if (enemy != null)
                {
                    enemy.ApplyDamage(damage);
                    SpawnImpactFlash(hit.point);
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position += direction * frameDistance;
            remainingDistance -= frameDistance;
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f || remainingDistance <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void SpawnImpactFlash(Vector3 position)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "FireImpactFlash";
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * 0.22f;
            var renderer = flash.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = glowColor;
            }

            Destroy(flash.GetComponent<Collider>());
            Destroy(flash, 0.12f);
        }
    }
}

using System.Collections.Generic;
using SpellGuard.Core;
using UnityEngine;

namespace SpellGuard.Combat
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform playerRoot;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private float spawnInterval = 2.5f;
        [SerializeField] private float spawnRadius = 18f;
        [SerializeField] private int maxAliveEnemies = 6;
        [SerializeField] private EnemyConfig enemyConfig = EnemyConfig.Default;

        private readonly List<SimpleEnemyController> aliveEnemies = new List<SimpleEnemyController>();
        private float nextSpawnTime;
        private bool spawningEnabled = true;

        public IReadOnlyList<SimpleEnemyController> AliveEnemies => aliveEnemies;
        public float SpawnInterval => spawnInterval;
        public float SpawnRadius => spawnRadius;
        public int MaxAliveEnemies => maxAliveEnemies;
        public EnemyConfig EnemyConfig => enemyConfig;

        private void Update()
        {
            aliveEnemies.RemoveAll(enemy => enemy == null);

            if (!spawningEnabled || playerRoot == null || playerHealth == null || !playerHealth.IsAlive)
            {
                return;
            }

            if (aliveEnemies.Count >= maxAliveEnemies || Time.time < nextSpawnTime)
            {
                return;
            }

            SpawnEnemy();
            nextSpawnTime = Time.time + spawnInterval;
        }

        public void ClearAll()
        {
            foreach (var enemy in aliveEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }

            aliveEnemies.Clear();
        }

        public void SetSpawningEnabled(bool value)
        {
            spawningEnabled = value;
        }

        public void ApplySettings(SpellGuardDifficulty difficulty)
        {
            var config = difficulty switch
            {
                SpellGuardDifficulty.Relaxed => DifficultySettings.Relaxed,
                SpellGuardDifficulty.Intense => DifficultySettings.Intense,
                _ => DifficultySettings.Standard
            };

            spawnInterval = config.SpawnInterval;
            maxAliveEnemies = config.MaxAliveEnemies;
        }

        public void ApplyWaveConfig(WaveConfig config)
        {
            var fallback = WaveConfig.Default;
            spawnInterval = config.SpawnInterval > 0f ? config.SpawnInterval : fallback.SpawnInterval;
            spawnRadius = config.SpawnRadius > 0f ? config.SpawnRadius : fallback.SpawnRadius;
            maxAliveEnemies = config.MaxAliveEnemies > 0 ? config.MaxAliveEnemies : fallback.MaxAliveEnemies;
            enemyConfig = IsValidEnemyConfig(config.Enemy) ? config.Enemy : fallback.Enemy;
        }

        private void SpawnEnemy()
        {
            var angle = Random.Range(-70f, 70f);
            var rotation = Quaternion.Euler(0f, angle, 0f);
            var spawnOffset = rotation * playerRoot.forward * spawnRadius;
            var spawnPosition = playerRoot.position + spawnOffset;
            spawnPosition.y = 1f;

            var enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObject.name = $"Enemy_{aliveEnemies.Count + 1}";
            enemyObject.transform.position = spawnPosition;
            enemyObject.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f);

            var renderer = enemyObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.85f, 0.25f, 0.2f);
            }

            var enemy = enemyObject.AddComponent<SimpleEnemyController>();
            enemy.Initialize(playerRoot, playerHealth);
            enemy.ApplyConfig(enemyConfig);
            aliveEnemies.Add(enemy);
        }

        private static bool IsValidEnemyConfig(EnemyConfig config)
        {
            return config.Speed > 0f && config.HitPoints > 0 && config.AttackDistance > 0f;
        }
    }
}

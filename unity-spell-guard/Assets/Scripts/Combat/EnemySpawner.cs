using System.Collections.Generic;
using SpellGuard.Core;
using UnityEngine;

namespace SpellGuard.Combat
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform playerRoot;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private float spawnInterval = 0f;
        [SerializeField] private float spawnRadius = 0f;
        [SerializeField] private int maxAliveEnemies = 3;
        [SerializeField] private EnemyConfig enemyConfig = EnemyConfig.Default;
        [SerializeField] private bool spawnOnceAtStart = true;
        [SerializeField] private Vector3[] spawnPoints =
        {
            new Vector3(-4f, 1f, 6f),
            new Vector3(0f, 1f, 7f),
            new Vector3(4f, 1f, 6f)
        };

        private readonly List<SimpleEnemyController> aliveEnemies = new List<SimpleEnemyController>();
        private float nextSpawnTime;
        private bool spawningEnabled = true;
        private int defeatedEnemies;
        private bool initialSpawnDone;

        public IReadOnlyList<SimpleEnemyController> AliveEnemies => aliveEnemies;
        public float SpawnInterval => spawnInterval;
        public float SpawnRadius => spawnRadius;
        public int MaxAliveEnemies => maxAliveEnemies;
        public EnemyConfig EnemyConfig => enemyConfig;
        public int DefeatedEnemies => defeatedEnemies;

        private void OnEnable()
        {
            SimpleEnemyController.AnyEnemyDied += HandleEnemyDied;
        }

        private void OnDisable()
        {
            SimpleEnemyController.AnyEnemyDied -= HandleEnemyDied;
        }

        private void Update()
        {
            aliveEnemies.RemoveAll(enemy => enemy == null);

            if (!spawningEnabled || playerRoot == null || playerHealth == null || !playerHealth.IsAlive)
            {
                return;
            }

            if (spawnOnceAtStart && initialSpawnDone)
            {
                return;
            }

            if (aliveEnemies.Count >= maxAliveEnemies || Time.time < nextSpawnTime)
            {
                return;
            }

            SpawnEnemy();
            nextSpawnTime = Time.time + spawnInterval;
            if (spawnOnceAtStart && aliveEnemies.Count >= maxAliveEnemies)
            {
                initialSpawnDone = true;
            }
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
            defeatedEnemies = 0;
            initialSpawnDone = false;
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
            spawnOnceAtStart = config.SpawnInterval <= 0f;
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
            var spawnPosition = spawnPoints.Length > 0
                ? spawnPoints[aliveEnemies.Count % spawnPoints.Length]
                : playerRoot.position + playerRoot.forward * Mathf.Max(0f, spawnRadius);
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
            if (aliveEnemies.Count >= maxAliveEnemies)
            {
                initialSpawnDone = true;
            }
        }

        private void HandleEnemyDied(SimpleEnemyController enemy)
        {
            if (!aliveEnemies.Remove(enemy))
            {
                return;
            }

            defeatedEnemies += 1;
        }

        private static bool IsValidEnemyConfig(EnemyConfig config)
        {
            return config.Speed > 0f && config.HitPoints > 0 && config.AttackDistance > 0f;
        }
    }
}

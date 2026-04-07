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

        private readonly List<SimpleEnemyController> aliveEnemies = new List<SimpleEnemyController>();
        private float nextSpawnTime;
        private bool spawningEnabled = true;

        public IReadOnlyList<SimpleEnemyController> AliveEnemies => aliveEnemies;

        public void Configure(Transform root, PlayerHealth health)
        {
            playerRoot = root;
            playerHealth = health;
        }

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
            switch (difficulty)
            {
                case SpellGuardDifficulty.Relaxed:
                    spawnInterval = 3.1f;
                    maxAliveEnemies = 4;
                    break;
                case SpellGuardDifficulty.Intense:
                    spawnInterval = 1.8f;
                    maxAliveEnemies = 8;
                    break;
                default:
                    spawnInterval = 2.5f;
                    maxAliveEnemies = 6;
                    break;
            }
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
            aliveEnemies.Add(enemy);
        }
    }
}

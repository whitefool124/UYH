using SpellGuard.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellGuard.Core
{
    public class GameFlowManager : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private EnemySpawner enemySpawner;

        public bool GameOver { get; private set; }

        public void Configure(PlayerHealth health, EnemySpawner spawner)
        {
            playerHealth = health;
            enemySpawner = spawner;
        }

        public void ResetGameOver()
        {
            GameOver = false;
        }

        private void Update()
        {
            if (GameOver)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }

                return;
            }

            if (playerHealth != null && !playerHealth.IsAlive)
            {
                GameOver = true;
                if (enemySpawner != null)
                {
                    enemySpawner.ClearAll();
                }
            }
        }
    }
}

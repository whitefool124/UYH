using SpellGuard.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellGuard.Core
{
    public class GameFlowManager : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private int targetScoreToWin = 12;

        public bool GameOver { get; private set; }
        public SpellGuardRunResult RunResult { get; private set; }
        public int TargetScoreToWin => Mathf.Max(1, targetScoreToWin);

        public void ApplyLevelConfig(LevelConfig config)
        {
            if (config == null)
            {
                return;
            }

            targetScoreToWin = config.TargetScore;
        }

        public void ResetGameOver()
        {
            GameOver = false;
            RunResult = SpellGuardRunResult.None;
        }

        public void ReportCombatScore(int combatScore)
        {
            if (GameOver)
            {
                return;
            }

            if (combatScore >= TargetScoreToWin)
            {
                EndRun(SpellGuardRunResult.Victory);
            }
        }

        private void Update()
        {
            if (GameOver)
            {
                return;
            }

            if (playerHealth != null && !playerHealth.IsAlive)
            {
                EndRun(SpellGuardRunResult.Defeat);
            }
        }

        private void EndRun(SpellGuardRunResult result)
        {
            GameOver = true;
            RunResult = result;
            if (enemySpawner != null)
            {
                enemySpawner.ClearAll();
            }
        }
    }
}

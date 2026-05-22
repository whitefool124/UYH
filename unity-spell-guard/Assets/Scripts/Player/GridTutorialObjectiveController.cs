using SpellGuard.Combat;
using SpellGuard.Core;
using UnityEngine;

namespace SpellGuard.Player
{
    public class GridTutorialObjectiveController : MonoBehaviour
    {
        [SerializeField] private Transform playerRoot;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private GameFlowManager gameFlow;
        [SerializeField] private int requiredKills = 3;
        [SerializeField] private float exitRadius = 1.4f;
        [SerializeField] private Vector3 exitPosition = new Vector3(0f, 0f, 9f);

        public string ObjectiveText { get; private set; } = "教学：按 WASD 移动，左键或 1 发射火球，击败 3 个固定敌人。";
        public bool ExitReady { get; private set; }

        private void Update()
        {
            if (gameFlow == null || gameFlow.GameOver)
            {
                return;
            }

            var kills = GetKills();
            ExitReady = kills >= Mathf.Max(1, requiredKills);
            if (!ExitReady)
            {
                ObjectiveText = $"目标：用火球击败敌人 {kills}/{requiredKills}";
                return;
            }

            var distance = playerRoot != null ? Vector3.Distance(Flat(playerRoot.position), Flat(exitPosition)) : float.PositiveInfinity;
            ObjectiveText = distance <= exitRadius ? "出口已激活，按 E 完成教学" : "敌人已清空，前往蓝色出口格";
            if (distance <= exitRadius && Input.GetKeyDown(KeyCode.E))
            {
                gameFlow.CompleteVictory();
            }
        }

        private int GetKills()
        {
            return enemySpawner != null ? enemySpawner.DefeatedEnemies : 0;
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}

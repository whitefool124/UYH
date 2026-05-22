using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.UI
{
    public class SimpleGameplayHud : MonoBehaviour
    {
        [SerializeField] private SpellGuardFlowController flowController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GestureSpellCaster spellCaster;
        [SerializeField] private GridTutorialObjectiveController objectiveController;
        [SerializeField] private EnemySpawner enemySpawner;

        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private GUIStyle titleStyle;
        private Texture2D panelTexture;

        private void OnGUI()
        {
            if (flowController == null || flowController.Screen != SpellGuardScreen.Playing)
            {
                return;
            }

            EnsureStyles();
            var rect = new Rect(20f, 20f, 420f, 176f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, rect.height - 24f));
            GUILayout.Label("教学关：固定三敌", titleStyle);
            GUILayout.Label(objectiveController != null ? objectiveController.ObjectiveText : flowController.HintText, labelStyle);
            GUILayout.Label($"生命：{(playerHealth != null ? playerHealth.CurrentHealth : 0)}/{(playerHealth != null ? playerHealth.MaxHealth : 0)}", labelStyle);
            GUILayout.Label($"火焰：{(spellCaster != null ? spellCaster.SelectedFireName : "未绑定")} · {(spellCaster != null ? spellCaster.StatusText : "无")}", labelStyle);
            GUILayout.Label($"敌人：{(enemySpawner != null ? enemySpawner.AliveEnemies.Count : 0)} / 3 · 操作：WASD 移动 / 左键或 1 施法 / E 过关", labelStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.06f, 0.78f));
            panelTexture.Apply();
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.normal.textColor = Color.white;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = Color.white } };
            titleStyle = new GUIStyle(labelStyle) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.25f, 1f) } };
        }
    }
}

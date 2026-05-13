using UnityEngine;

namespace SpellGuard.Combat
{
    [CreateAssetMenu(fileName = "LevelConfigLibrary", menuName = "Spell Guard/Combat/Level Config Library")]
    public class LevelConfigLibrary : ScriptableObject
    {
        [SerializeField] private LevelConfig tutorialLevel;
        [SerializeField] private LevelConfig combatLevel;

        public LevelConfig TutorialLevel => tutorialLevel;
        public LevelConfig CombatLevel => combatLevel;
    }
}

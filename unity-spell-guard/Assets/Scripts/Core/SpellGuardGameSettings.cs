using System;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardGameSettings : MonoBehaviour
    {
        [SerializeField] private float[] confirmSecondsOptions = { 0.42f, 0.56f, 0.72f };
        [SerializeField] private SpellGuardDifficulty[] difficultyOptions =
        {
            SpellGuardDifficulty.Relaxed,
            SpellGuardDifficulty.Standard,
            SpellGuardDifficulty.Intense,
        };

        [SerializeField] private int confirmIndex = 1;
        [SerializeField] private int difficultyIndex = 1;
        [SerializeField] private float menuDwellSeconds = 0.82f;
        [SerializeField] private float menuBackHoldSeconds = 0.65f;

        public float ConfirmSeconds => confirmSecondsOptions[Mathf.Clamp(confirmIndex, 0, confirmSecondsOptions.Length - 1)];
        public SpellGuardDifficulty Difficulty => difficultyOptions[Mathf.Clamp(difficultyIndex, 0, difficultyOptions.Length - 1)];
        public float MenuDwellSeconds => menuDwellSeconds;
        public float MenuBackHoldSeconds => menuBackHoldSeconds;
        public string ConfirmLabel => $"{Mathf.RoundToInt(ConfirmSeconds * 1000f)} ms";
        public string DifficultyLabel => Difficulty switch
        {
            SpellGuardDifficulty.Relaxed => "轻松",
            SpellGuardDifficulty.Intense => "紧张",
            _ => "标准",
        };

        public void CycleConfirm()
        {
            confirmIndex = (confirmIndex + 1) % confirmSecondsOptions.Length;
        }

        public void CycleDifficulty()
        {
            difficultyIndex = (difficultyIndex + 1) % difficultyOptions.Length;
        }
    }
}

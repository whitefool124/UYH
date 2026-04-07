using UnityEngine;

namespace SpellGuard.Combat
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 5;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public float ShieldActiveUntil { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public bool ShieldActive => Time.time < ShieldActiveUntil;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            ShieldActiveUntil = 0f;
        }

        public void ActivateShield(float duration)
        {
            ShieldActiveUntil = Time.time + duration;
        }

        public bool ApplyHit(int damage)
        {
            if (!IsAlive)
            {
                return false;
            }

            if (ShieldActive)
            {
                ShieldActiveUntil = 0f;
                return false;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            return CurrentHealth <= 0;
        }
    }
}

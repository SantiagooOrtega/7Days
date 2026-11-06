using System;
using UnityEngine;

namespace SevenDays.Core
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        /// <summary>Aplica daño. Devuelve true si este golpe mató al objetivo.</summary>
        bool TakeDamage(DamageInfo info);
    }

    /// <summary>Vida reutilizable: jugador, zombies, jefes y blancos de práctica.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        private void Awake() => Current = maxHealth;

        public void SetMax(float max, bool fill = true)
        {
            maxHealth = max;
            if (fill) Current = max;
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead) return false;
            Current = Mathf.Max(0f, Current - info.Amount);
            Damaged?.Invoke(info);
            if (Current > 0f) return false;
            Died?.Invoke();
            return true;
        }

        public void Heal(float amount) => Current = Mathf.Min(maxHealth, Current + amount);
        public void ResetHealth() => Current = maxHealth;
    }
}

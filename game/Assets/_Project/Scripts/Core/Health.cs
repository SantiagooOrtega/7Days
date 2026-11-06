using System;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Core
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
        // --- impacto de un arma contra una zona (lo rellena el hitbox y el sistema de disparo)
        public bool UseZone;               // true: Amount es el daño base (con falloff) y Health aplica zona y resistencia
        public HitZone Zone;
        public float ArmorPenetration;
        public float HeadshotMultiplier;   // del arma; 0 se trata como 1
        public float Knockback;            // del arma
        public float Distance;
        public string WeaponId;
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        /// <summary>Aplica daño. Devuelve true si este golpe mató al objetivo.</summary>
        bool TakeDamage(DamageInfo info);
    }

    /// <summary>
    /// Vida reutilizable: jugador, zombies, jefes y blancos de práctica. Envuelve CombatantCore (lógica pura probada en EditMode): estados
    /// vivo/aturdido/muerto, resistencias y multiplicadores por zona.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

        private CombatantCore _core;
        private CombatantCore Core => _core ??= new CombatantCore(maxHealth);

        public float Max => Core.Max;
        public float Current => Core.Current;
        public float Fraction => Core.Fraction;
        public bool IsDead => Core.IsDead;
        public bool IsStunned => Core.State == HealthState.Stunned;
        public HealthState State => Core.State;
        public bool KilledByHeadshot => Core.KilledByHeadshot;
        public HitReport LastReport { get; private set; }
        /// <summary>Datos del combatiente (resistencias y zonas) para configurar tipos de zombie.</summary>
        public CombatantCore Combatant => Core;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo, HitReport> Hit;   // solo impactos por zona
        public event Action Died;
        public event Action<float> Stunned;                // segundos

        private void Awake() { _core = new CombatantCore(maxHealth); }

        private void Update() { if (_core != null && _core.State == HealthState.Stunned) _core.Tick(Time.deltaTime); }

        public void SetMax(float max, bool fill = true)
        {
            maxHealth = max;
            if (fill) Core.Reset(max);
        }

        public void Configure(float max, float resistance, float stunResistance, ZoneMultipliers zones)
        {
            maxHealth = max;
            Core.Reset(max);
            Core.Resistance = resistance;
            Core.StunResistance = stunResistance;
            Core.Zones = zones ?? new ZoneMultipliers();
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead) return false;
            if (info.UseZone)
            {
                var r = Core.ApplyHit(info.Zone, info.Amount, info.ArmorPenetration, info.HeadshotMultiplier <= 0f ? 1f : info.HeadshotMultiplier);
                LastReport = r;
                info.Amount = r.FinalDamage;
                Damaged?.Invoke(info);
                Hit?.Invoke(info, r);
                if (!r.Killed) return false;
                Died?.Invoke();
                return true;
            }
            bool killed = Core.ApplyResolved(info.Amount);
            LastReport = new HitReport { Valid = true, FinalDamage = info.Amount, BaseDamage = info.Amount, Zone = HitZone.Torso, Killed = killed };
            Damaged?.Invoke(info);
            if (!killed) return false;
            Died?.Invoke();
            return true;
        }

        public bool Stun(float seconds)
        {
            if (!Core.Stun(seconds)) return false;
            Stunned?.Invoke(Core.StunRemaining);
            return true;
        }

        public void Heal(float amount) => Core.Heal(amount);
        public void ResetHealth() => Core.Reset(maxHealth);
    }
}

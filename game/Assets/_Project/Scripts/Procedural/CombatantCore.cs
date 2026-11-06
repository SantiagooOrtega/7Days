using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Lógica pura de vida de un combatiente (zombie, jefe): vida, estados vivo/aturdido/muerto, resistencias y reenvío de daño por zona.
    /// El componente Health de Unity la envuelve; así se prueba sin el editor.
    /// </summary>
    public class CombatantCore
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public float Resistance;
        public float StunResistance;
        public ZoneMultipliers Zones = new ZoneMultipliers();
        public HealthState State { get; private set; } = HealthState.Alive;
        public float StunRemaining { get; private set; }
        /// <summary>Multiplicador extra de daño recibido (p. ej. el jefe aturdido contra un muro recibe más).</summary>
        public float VulnerabilityMultiplier = 1f;
        public bool KilledByHeadshot { get; private set; }
        public HitZone LastZone { get; private set; }

        public bool IsDead => State == HealthState.Dead;
        public float Fraction => Max <= 0f ? 0f : Current / Max;

        public CombatantCore(float max) { Reset(max); }

        public void Reset(float max)
        {
            Max = Math.Max(1f, max);
            Current = Max;
            State = HealthState.Alive;
            StunRemaining = 0f;
            KilledByHeadshot = false;
            VulnerabilityMultiplier = 1f;
        }

        /// <summary>Impacto de un arma: daño base (ya con falloff) y el arma; devuelve el informe completo.</summary>
        public HitReport ApplyHit(HitZone zone, float baseDamage, float armorPenetration = 0f, float weaponHeadshotMultiplier = 1f)
        {
            var r = new HitReport { Zone = zone, BaseDamage = baseDamage };
            if (IsDead) return r;
            float zm = DamageModel.ZoneMultiplier(Zones, zone, weaponHeadshotMultiplier) * Math.Max(0f, VulnerabilityMultiplier);
            int dmg = DamageModel.Final(baseDamage, 1f, zm, Resistance, armorPenetration);
            r.Valid = true;
            r.Headshot = zone == HitZone.Head;
            r.FinalDamage = dmg;
            r.Reduced = dmg < baseDamage;
            LastZone = zone;
            Subtract(dmg);
            r.Killed = IsDead;
            if (r.Killed) KilledByHeadshot = r.Headshot;
            return r;
        }

        /// <summary>Daño ya resuelto (explosiones, proyectiles): no vuelve a aplicar zona ni resistencia.</summary>
        public bool ApplyResolved(float amount)
        {
            if (IsDead || amount <= 0f) return false;
            Subtract(amount);
            return IsDead;
        }

        private void Subtract(float amount)
        {
            Current = Math.Max(0f, Current - amount);
            if (Current <= 0f) { State = HealthState.Dead; StunRemaining = 0f; }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Math.Min(Max, Current + amount);
        }

        /// <summary>Aturde (la resistencia al aturdimiento recorta o anula la duración). Devuelve true si quedó aturdido.</summary>
        public bool Stun(float seconds)
        {
            if (IsDead) return false;
            float d = StunMath.Duration(seconds, StunResistance);
            if (d <= 0.02f) return false;
            StunRemaining = Math.Max(StunRemaining, d);
            State = HealthState.Stunned;
            return true;
        }

        public void Tick(float dt)
        {
            if (State != HealthState.Stunned) return;
            StunRemaining -= dt;
            if (StunRemaining <= 0f) { StunRemaining = 0f; State = HealthState.Alive; }
        }
    }
}

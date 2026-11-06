using System;

namespace SevenDays.Procedural
{
    public enum HitZone { Head, Torso, Arms, Legs, WeakPoint }
    public enum HealthState { Alive, Stunned, Dead }

    /// <summary>Multiplicador de daño por zona (configurable por tipo de zombie): cabeza x2.5, torso x1, brazos x0.6, piernas x0.7; el punto débil de un jefe x4.</summary>
    [Serializable]
    public class ZoneMultipliers
    {
        public float head = 2.5f, torso = 1f, arms = 0.6f, legs = 0.7f, weakPoint = 4f;

        public float For(HitZone z)
        {
            switch (z)
            {
                case HitZone.Head: return head;
                case HitZone.Torso: return torso;
                case HitZone.Arms: return arms;
                case HitZone.Legs: return legs;
                default: return weakPoint;
            }
        }

        public ZoneMultipliers Clone() => (ZoneMultipliers)MemberwiseClone();
    }

    /// <summary>Resultado de un impacto, para el hitmarker, los números flotantes, el sonido y las métricas del director.</summary>
    public struct HitReport
    {
        public HitZone Zone;
        public float FinalDamage;
        public float BaseDamage;     // daño con falloff, antes de zona y resistencia
        public bool Killed;
        public bool Headshot;
        public bool Reduced;         // el daño final fue menor que el base (zona resistente, armadura, resistencia)
        public bool Valid;
    }

    /// <summary>
    /// Fórmula de daño: daño final = daño base × falloff(distancia) × multiplicador de zona × (1 − resistencia). La penetración de armadura del arma resta
    /// resistencia, el headshot lleva además el multiplicador propio del arma, y el redondeo es estable (mitades hacia arriba) y nunca negativo.
    /// </summary>
    public static class DamageModel
    {
        /// <summary>Fracción del daño a esa distancia: 1 hasta falloffStart x alcance, luego baja linealmente hasta minFraction en el alcance máximo.</summary>
        public static float Falloff(float distance, float range, float falloffStart, float minFraction)
        {
            if (range <= 0f || distance <= 0f) return 1f;
            float start = Math.Max(0f, Math.Min(0.95f, falloffStart)) * range;
            if (distance <= start) return 1f;
            if (distance >= range) return Clamp01(minFraction);
            float t = (distance - start) / Math.Max(1e-4f, range - start);
            return 1f + (Clamp01(minFraction) - 1f) * t;
        }

        public static float Falloff(WeaponData w, float distance) => w.IsMelee ? 1f : Falloff(distance, w.range, w.falloffStart, w.minDamageFraction);

        /// <summary>Multiplicador de la zona; en la cabeza se multiplica además por el multiplicador de headshot propio del arma.</summary>
        public static float ZoneMultiplier(ZoneMultipliers zones, HitZone zone, float weaponHeadshotMultiplier = 1f)
        {
            float m = zones.For(zone);
            if (zone == HitZone.Head) m *= Math.Max(0.1f, weaponHeadshotMultiplier);
            return Math.Max(0f, m);
        }

        /// <summary>Resistencia efectiva: la penetración de armadura del arma la reduce (nunca por debajo de 0 ni por encima de 0.95).</summary>
        public static float EffectiveResistance(float resistance, float armorPenetration)
            => Math.Max(0f, Math.Min(0.95f, resistance - Math.Max(0f, armorPenetration)));

        /// <summary>Daño sin redondear (nunca negativo).</summary>
        public static float Raw(float baseDamage, float falloff, float zoneMultiplier, float resistance, float armorPenetration = 0f)
        {
            float v = baseDamage * falloff * zoneMultiplier * (1f - EffectiveResistance(resistance, armorPenetration));
            return v > 0f ? v : 0f;
        }

        /// <summary>Daño final entero: redondeo estable (0.5 sube); si hay daño positivo, mínimo 1 para que nada sea inmune por redondeo.</summary>
        public static int Final(float baseDamage, float falloff, float zoneMultiplier, float resistance, float armorPenetration = 0f)
        {
            float raw = Raw(baseDamage, falloff, zoneMultiplier, resistance, armorPenetration);
            if (raw <= 0f) return 0;
            return Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        /// <summary>Daño de UN disparo contra un zombie en una zona, con todos los proyectiles acertando (la escopeta suma sus perdigones).</summary>
        public static int PerShot(WeaponData w, ZombieTypeData z, HitZone zone, float distance, float healthResistanceOverride = -1f)
        {
            float res = healthResistanceOverride >= 0f ? healthResistanceOverride : z.resistance;
            float falloff = Falloff(w, distance);
            float zm = ZoneMultiplier(z.zones, zone, w.headshotMultiplier);
            int perPellet = Final(w.damage, falloff, zm, res, w.armorPenetration);
            return perPellet * Math.Max(1, w.pellets);
        }

        /// <summary>Tiros para matar (TTK en disparos) con todos los proyectiles acertando en esa zona y distancia. int.MaxValue si no hace daño.</summary>
        public static int ShotsToKill(WeaponData w, ZombieTypeData z, HitZone zone, float distance)
        {
            int dmg = PerShot(w, z, zone, distance);
            if (dmg <= 0) return int.MaxValue;
            return (int)Math.Ceiling(z.maxHealth / (double)dmg);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>Reacción de un zombie a un impacto: empujón y aturdimiento ligero. Más fuerte con la escopeta (knockback del arma) y en la cabeza; los jefes resisten el aturdimiento.</summary>
    public struct HitReaction
    {
        public bool Stagger;
        public float StunSeconds;
        public float KnockbackMeters;

        public static HitReaction Compute(float finalDamage, float maxHealth, HitZone zone, float weaponKnockback, float stunResistance)
        {
            var r = new HitReaction();
            if (maxHealth <= 0f || finalDamage <= 0f) return r;
            float frac = finalDamage / maxHealth;
            float threshold = zone == HitZone.Head ? 0.05f : 0.08f;
            float push = Math.Max(0f, weaponKnockback) * (zone == HitZone.Head ? 1.4f : 1f);
            r.KnockbackMeters = Math.Min(1.2f, 0.12f * push + frac * 1.5f) * (1f - Math.Min(1f, Math.Max(0f, stunResistance)) * 0.7f);
            if (frac >= threshold)
            {
                r.StunSeconds = StunMath.Duration(Math.Min(0.7f, 0.15f + frac * 1.2f) * (zone == HitZone.Head ? 1.3f : 1f) * (0.6f + 0.4f * push), stunResistance);
                r.Stagger = r.StunSeconds > 0.12f;   // por debajo no se nota: el jefe aguanta el golpe sin inmutarse
                if (!r.Stagger) r.StunSeconds = 0f;
            }
            return r;
        }
    }

    public static class StunMath
    {
        /// <summary>Duración real del aturdimiento: se reduce por la resistencia (1 = inmune).</summary>
        public static float Duration(float baseSeconds, float stunResistance)
        {
            float res = Math.Max(0f, Math.Min(1f, stunResistance));
            return Math.Max(0f, baseSeconds * (1f - res));
        }
    }
}

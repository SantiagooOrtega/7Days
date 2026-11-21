using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Granada de fragmentación (pura): mecha de 2.5 s, radio de 6 m con caída lineal hasta el 25 % en el borde, multiplicador por tipo de zombi
    /// (común casi seguro muerto, tanque y blindado mucho, jefes poco), cobertura sólida entre la granada y el objetivo = sin daño, y al propio jugador
    /// solo si está muy cerca (4 m). Empiezas con 2, máximo 6. Valores en docs/BALANCE.md (GrenadeTests).
    /// </summary>
    public static class GrenadeRules
    {
        public const float FuseSeconds = 2.5f, Radius = 6f, MaxDamage = 260f, Knockback = 9f;
        public const float ThrowSpeed = 15f, ThrowUp = 0.22f, Gravity = 9.81f, Cooldown = 0.8f;
        public const float SelfRadius = 4f, SelfFactor = 0.55f;
        public const int Start = 2, Max = 6;

        /// <summary>Cuánto le afecta a cada tipo (la resistencia del zombi se aplica después, en su Health).</summary>
        public static float TypeFactor(string zombieId, bool boss)
        {
            if (boss) return 0.15f;
            switch (zombieId)
            {
                case ZombieIds.Armored: return 0.6f;
                case ZombieIds.Tank: return 1f;   // 0.35 de resistencia propia: ~40 % de su vida en el centro
                default: return 1f;
            }
        }

        /// <summary>Daño a "distance" metros. blocked = una cobertura sólida tapa la línea entre la granada y el objetivo (no pasa nada).</summary>
        public static float Damage(float distance, bool blocked, float typeFactor = 1f)
        {
            if (blocked) return 0f;
            return AreaDamage.At(distance, Radius, MaxDamage) * typeFactor;
        }

        public static float PlayerDamage(float distance, bool blocked) => distance >= SelfRadius ? 0f : Damage(distance, blocked) * SelfFactor;

        public static float Push(float distance, bool blocked) => blocked || distance >= Radius ? 0f : Knockback * (1f - distance / Radius);

        /// <summary>Probabilidad de que un zombi suelte una granada al morir (aparte de su drop normal). Pocas en comunes; más en tanques y jefes.</summary>
        public static double DropChance(string dropKind)
        {
            switch (dropKind)
            {
                case "boss": return 1.0;
                case "tank": return 0.25;
                case "brute": return 0.15;
                case "armored": return 0.1;
                case "spitter": return 0.05;
                case "runner": return 0.03;
                default: return 0.02;
            }
        }

        /// <summary>Granadas que suelta: 2 el jefe, 1 los demás.</summary>
        public static int DropAmount(string dropKind) => dropKind == "boss" ? 2 : 1;

        /// <summary>Velocidad de lanzamiento hacia "aim" (normalizado) con un poco de elevación.</summary>
        public static V3 ThrowVelocity(V3 aim)
        {
            var d = new V3(aim.X, aim.Y + ThrowUp, aim.Z);
            float len = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
            if (len < 1e-4f) return new V3(0f, ThrowSpeed * 0.5f, ThrowSpeed * 0.8f);
            return new V3(d.X / len * ThrowSpeed, d.Y / len * ThrowSpeed, d.Z / len * ThrowSpeed);
        }

        /// <summary>Puntos de la parábola cada "dt" segundos (sin choques; el juego corta con raycasts). Escribe en "buffer" sin reservar memoria y devuelve cuántos.</summary>
        public static int Arc(V3 origin, V3 velocity, float dt, V3[] buffer, float minY = -2f)
        {
            int n = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i * dt;
                var p = new V3(origin.X + velocity.X * t, origin.Y + velocity.Y * t - 0.5f * Gravity * t * t, origin.Z + velocity.Z * t);
                buffer[n++] = p;
                if (p.Y < minY) break;
            }
            return n;
        }
    }

    /// <summary>Granadas que lleva el jugador (empieza con 2, máximo 6).</summary>
    public class GrenadePouch
    {
        public int Count { get; private set; } = GrenadeRules.Start;
        public int Max => GrenadeRules.Max;

        /// <summary>Añade hasta el máximo. Devuelve cuántas entraron (0 = lleno: el pickup no se consume).</summary>
        public int Add(int n)
        {
            int added = Math.Max(0, Math.Min(n, Max - Count));
            Count += added;
            return added;
        }

        public bool TryUse() { if (Count <= 0) return false; Count--; return true; }
        public void Set(int n) => Count = Math.Max(0, Math.Min(Max, n));
    }
}

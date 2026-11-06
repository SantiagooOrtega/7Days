using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum FireKind { Barrel, Campfire, Car, Torch }

    /// <summary>Datos de cada tipo de fuego: radio donde quema, tamaño de llama, humo, chispas y luz.</summary>
    public class FireProfile
    {
        public float BurnRadius;      // 0 = no quema (antorcha en la pared)
        public float FlameScale;
        public float SmokeRate;       // partículas de humo por segundo
        public float SparkRate;       // chispas por segundo
        public float LightRange, LightIntensity;
        public float ScorchSize;      // tamaño de la mancha de quemadura en el suelo (0 = ninguna)
        public int Priority;          // desempata la selección de luces: mayor = más importante

        public static FireProfile For(FireKind k)
        {
            switch (k)
            {
                case FireKind.Barrel: return new FireProfile { BurnRadius = 0.7f, FlameScale = 1.0f, SmokeRate = 5f, SparkRate = 8f, LightRange = 12f, LightIntensity = 3.2f, ScorchSize = 1.6f, Priority = 2 };
                case FireKind.Campfire: return new FireProfile { BurnRadius = 0.9f, FlameScale = 1.1f, SmokeRate = 4f, SparkRate = 10f, LightRange = 11f, LightIntensity = 3.0f, ScorchSize = 2.4f, Priority = 2 };
                case FireKind.Car: return new FireProfile { BurnRadius = 2.0f, FlameScale = 2.2f, SmokeRate = 12f, SparkRate = 14f, LightRange = 16f, LightIntensity = 4.2f, ScorchSize = 4.4f, Priority = 3 };
                default: return new FireProfile { BurnRadius = 0f, FlameScale = 0.45f, SmokeRate = 1.5f, SparkRate = 3f, LightRange = 7f, LightIntensity = 2.0f, ScorchSize = 0f, Priority = 1 };
            }
        }
    }

    /// <summary>Cuentas del fuego sin UnityEngine: daño por quemadura, cuántas luces se pueden mantener y cuáles.</summary>
    public static class FireMath
    {
        public const float BelowTolerance = 0.3f, AboveReach = 2.2f;

        /// <summary>
        /// Daño por quemadura en un intervalo de "dt" segundos: el jugador debe estar dentro del radio (en horizontal) y a una altura razonable respecto al
        /// fuego. Fuera de eso, o si el fuego no quema (radio 0) o dps es 0, no hay daño.
        /// </summary>
        public static float BurnDamage(float horizontalDistance, float heightAboveFire, float radius, float dps, float dt)
        {
            if (radius <= 0f || dps <= 0f || dt <= 0f) return 0f;
            if (horizontalDistance > radius) return 0f;
            if (heightAboveFire < -BelowTolerance || heightAboveFire > AboveReach) return 0f;
            return dps * dt;
        }

        /// <summary>Cuántas luces de fuego se mantienen encendidas según el nivel de recorte (0 = todas, 3 = solo 1).</summary>
        public static int MaxLights(int baseMax, int level)
        {
            if (baseMax <= 0) return 0;
            if (level <= 0) return baseMax;
            if (level == 1) return Math.Max(1, (int)Math.Ceiling(baseMax * 0.6));
            if (level == 2) return Math.Min(2, baseMax);
            return 1;
        }

        /// <summary>Índices de las "max" fuentes más cercanas al observador (a igual distancia gana la de mayor prioridad).</summary>
        public static int[] NearestLights(IList<V3> positions, IList<int> priorities, V3 viewer, int max)
        {
            var idx = new List<int>();
            for (int i = 0; i < positions.Count; i++) idx.Add(i);
            idx.Sort((a, b) =>
            {
                float da = Dist2(positions[a], viewer) - 4f * (priorities != null ? priorities[a] : 0); // la prioridad "acerca" la fuente unos metros
                float db = Dist2(positions[b], viewer) - 4f * (priorities != null ? priorities[b] : 0);
                return da.CompareTo(db);
            });
            if (idx.Count > max) idx.RemoveRange(max, idx.Count - max);
            return idx.ToArray();
        }

        /// <summary>Factor de humo y chispas según el nivel de recorte.</summary>
        public static float SmokeFactor(int level) => level <= 0 ? 1f : (level == 1 ? 0.6f : (level == 2 ? 0.3f : 0f));
        public static float SparkFactor(int level) => level <= 1 ? 1f : 0f;

        private static float Dist2(V3 a, V3 b) { float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z; return dx * dx + dy * dy + dz * dz; }
    }
}

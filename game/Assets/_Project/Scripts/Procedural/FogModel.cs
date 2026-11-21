using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Niebla exponencial al cuadrado (la de Unity/URP), en puro para poder probarla: cuánto se ve algo a una distancia y la densidad máxima que deja ver
    /// un zombi a 25 m (al menos el 25 % de su color sin niebla). El Bosque ya iba cerca de ese límite: allí la niebla extra va al suelo y al horizonte.
    /// </summary>
    public static class FogModel
    {
        public const float SafeDistance = 25f, MinVisibility = 0.25f;

        /// <summary>Fracción visible (1 = sin niebla, 0 = solo niebla) a "distance" metros.</summary>
        public static float Visibility(float density, float distance)
        {
            double x = density * distance;
            return (float)Math.Exp(-x * x);
        }

        /// <summary>Densidad máxima con la que algo a "distance" metros conserva "minVisibility".</summary>
        public static float MaxDensity(float distance = SafeDistance, float minVisibility = MinVisibility)
            => (float)(Math.Sqrt(-Math.Log(minVisibility)) / Math.Max(1f, distance));

        /// <summary>Distancia a la que la niebla ya tapa casi todo (queda un 5 %): la "visibilidad lejana".</summary>
        public static float FarDistance(float density) => density <= 0f ? float.PositiveInfinity : (float)(Math.Sqrt(-Math.Log(0.05)) / density);

        /// <summary>Color de la pared de bruma del horizonte por altura (0 abajo, 1 arriba): del color de la niebla al del cielo, para que no se vea el corte.</summary>
        public static float[] HorizonColor(float[] fog, float[] sky, float height01)
        {
            float t = Math.Max(0f, Math.Min(1f, height01));
            return new[] { fog[0] + (sky[0] - fog[0]) * t, fog[1] + (sky[1] - fog[1]) * t, fog[2] + (sky[2] - fog[2]) * t };
        }
    }
}

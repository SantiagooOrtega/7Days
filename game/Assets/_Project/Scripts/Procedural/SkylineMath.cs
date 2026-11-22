using System;

namespace SevenDays.Procedural
{
    /// <summary>Geometría pura de las siluetas del fondo: dónde va la azotea rota de un edificio para que quede siempre dentro de su planta.</summary>
    public static class SkylineMath
    {
        public const float Margin = 0.3f;

        /// <summary>Desplazamiento (en los ejes del edificio, y = 0) del bloque de azotea de tamaño sx x sz sobre un edificio w x d; r1 y r2 en 0..1.</summary>
        public static void TopOffset(float w, float d, float sx, float sz, float r1, float r2, out float ox, out float oz)
        {
            float mx = Math.Max(0f, (w - sx) / 2f - Margin), mz = Math.Max(0f, (d - sz) / 2f - Margin);
            ox = (r1 * 2f - 1f) * mx; oz = (r2 * 2f - 1f) * mz;
        }
    }
}

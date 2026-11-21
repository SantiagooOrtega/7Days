using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Regla dura del botín (pura): todo pickup va dentro del polígono del perímetro con 1 m de margen. El perímetro de los mapas es el cuadrado
    /// ±halfSize (la cara interior del muro). Lo que cae fuera se acerca al centro hasta quedar dentro. NavMesh y camino los comprueba LootManager.
    /// </summary>
    public static class LootPlacement
    {
        public const float Margin = 1f;

        public static float[][] Square(float halfSize) => new[] { new[] { -halfSize, -halfSize }, new[] { halfSize, -halfSize }, new[] { halfSize, halfSize }, new[] { -halfSize, halfSize } };

        /// <summary>Dentro del polígono y a "margin" metros o más de cada borde.</summary>
        public static bool Inside(float[][] poly, float x, float z, float margin = Margin)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                float xi = poly[i][0], zi = poly[i][1], xj = poly[j][0], zj = poly[j][1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            if (!inside) return false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (SegmentDistance(x, z, poly[j][0], poly[j][1], poly[i][0], poly[i][1]) < margin) return false;
            return true;
        }

        public static bool Inside(float halfSize, float x, float z, float margin = Margin) => Inside(Square(halfSize), x, z, margin);

        private static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            float t = l2 <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (pz - az) * dz) / l2));
            float qx = ax + t * dx - px, qz = az + t * dz - pz;
            return (float)Math.Sqrt(qx * qx + qz * qz);
        }

        /// <summary>El punto válido más cercano: si ya vale, el mismo; si no, se acerca al centro del polígono en pasos de 0.25 m.</summary>
        public static float[] Clamp(float[][] poly, float x, float z, float margin = Margin)
        {
            if (Inside(poly, x, z, margin)) return new[] { x, z };
            float cx = 0, cz = 0; foreach (var p in poly) { cx += p[0]; cz += p[1]; } cx /= poly.Length; cz /= poly.Length;
            float len = (float)Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
            if (len < 1e-4f) return new[] { cx, cz };
            for (float d = len; d >= 0f; d -= 0.25f)
            {
                float k = d / len, nx = cx + (x - cx) * k, nz = cz + (z - cz) * k;
                if (Inside(poly, nx, nz, margin)) return new[] { nx, nz };
            }
            return new[] { cx, cz };
        }

        /// <summary>Puntos del botín fuera del perímetro (para validar JSON y la auditoría).</summary>
        public static List<string> Outside(LootLayoutData l, float halfSize)
        {
            var bad = new List<string>();
            if (l?.points != null) foreach (var p in l.points) if (!Inside(halfSize, p.x, p.z)) bad.Add(p.id);
            return bad;
        }
    }
}

using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Mano que envuelve un mango (pura). Medidas de la mano procedural (RigHand, escala 1): palma de 8.5 cm, dedos de dos falanges que se doblan hacia la palma
    /// (-X local; los dedos apuntan a -Y). Un mango cilíndrico de radio r con su eje a lo ancho de la palma (eje Z de la mano) se agarra con su eje en
    /// <see cref="Anchor"/>: pegado a la palma, a 60 % de su largo. <see cref="Wrap"/> dobla cada dedo hasta tocar el mango, sin atravesarlo.
    /// </summary>
    public static class HandWrap
    {
        public const float PalmLength = 0.085f, PalmWidth = 0.08f, PalmThickness = 0.03f, FingerHalf = 0.0095f, AnchorAlong = 0.6f;
        public static readonly float[] Z = { 0.03f, 0.01f, -0.01f, -0.028f };
        public static readonly float[] L1 = { 0.042f, 0.046f, 0.044f, 0.036f }, L2 = { 0.036f, 0.038f, 0.036f, 0.03f };

        /// <summary>Eje del mango en el espacio de la mano (desde la muñeca): delante de la palma a "r" de su cara, a 60 % de su largo.</summary>
        public static V3 Anchor(float radius, float k) => new V3(-(PalmThickness * 0.5f * k + radius), -PalmLength * AnchorAlong * k, 0f);

        /// <summary>
        /// Flexión de un dedo (0 índice .. 3 meñique) para envolver un mango de radio r con el eje en Anchor: cada falange se dobla lo más posible sin que el dedo
        /// (con su grosor) entre en el mango; tope 100°.
        /// </summary>
        public static FingerCurl Finger(int i, float radius, float k)
        {
            V3 c = Anchor(radius, k);
            float cx = c.X, cy = c.Y, rr = radius + FingerHalf * k;
            float bx = 0f, by = -PalmLength * k, l1 = L1[i] * k, l2 = L2[i] * k;
            float a1 = Fit(bx, by, 0f, l1, cx, cy, rr);
            double r1 = a1 * Math.PI / 180.0;
            float jx = bx - (float)Math.Sin(r1) * l1, jy = by - (float)Math.Cos(r1) * l1;
            float a2 = Fit(jx, jy, a1, l2, cx, cy, rr);
            return new FingerCurl(a1, a2);
        }

        /// <summary>Mayor ángulo (0..100, en pasos de 1°) que puede doblarse un segmento sin acercarse al centro del mango a menos de rr.</summary>
        private static float Fit(float x, float y, float baseAngle, float len, float cx, float cy, float rr)
        {
            float best = 0f;
            for (int a = 0; a <= (int)HandPoses.MaxCurl; a++)
            {
                double t = (baseAngle + a) * Math.PI / 180.0;
                float ex = x - (float)Math.Sin(t) * len, ey = y - (float)Math.Cos(t) * len;
                if (SegmentDistance(x, y, ex, ey, cx, cy) < rr) break;
                best = a;
            }
            return best;
        }

        public static float SegmentDistance(float ax, float ay, float bx, float by, float px, float py)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            float t = l2 > 1e-9f ? Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (py - ay) * dy) / l2)) : 0f;
            float qx = ax + dx * t - px, qy = ay + dy * t - py;
            return (float)Math.Sqrt(qx * qx + qy * qy);
        }

        /// <summary>Pose de mano que envuelve un mango de radio r; el pulgar viene de "thumbFrom" (cada agarre coloca el pulgar a su manera).</summary>
        public static HandPose Wrap(float radius, float k, HandPose thumbFrom)
        {
            var p = thumbFrom;
            p.Index = Finger(0, radius, k); p.Middle = Finger(1, radius, k); p.Ring = Finger(2, radius, k); p.Pinky = Finger(3, radius, k);
            return p;
        }
    }
}

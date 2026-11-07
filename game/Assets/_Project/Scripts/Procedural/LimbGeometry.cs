using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Segmento de extremidad (muslo, pantorrilla, caña de bota): tubo de varios anillos con su PIVOTE EN LA ARTICULACIÓN de arriba (y = 0) y que baja por -Y
    /// hasta "length", como el hueso. Así la pieza gira exactamente alrededor de la articulación y no asoma por encima de ella. Tapas en los dos extremos (malla
    /// cerrada). "rings" = pares (t 0..1 a lo largo, radio en m); "depth" estira el grosor delante-detrás (Z). UV: u alrededor, v en metros a lo largo.
    /// </summary>
    public static class LimbGeometry
    {
        public static MeshData Segment(float length, float[] rings, int sides, float depth, float tileMeters = 0.5f)
        {
            var m = new MeshData();
            int count = rings.Length / 2;
            for (int r = 0; r < count; r++)
            {
                float y = -rings[r * 2] * length, rad = rings[r * 2 + 1];
                for (int s = 0; s <= sides; s++)
                {
                    double a = s * 2.0 * Math.PI / sides;
                    float c = (float)Math.Cos(a), n = (float)Math.Sin(a);
                    m.Positions.Add(new V3(c * rad, y, n * rad * depth));
                    m.Normals.Add(new V3(c * depth, 0f, n).Normalized);
                    m.Tangents.Add(new V3(-n, 0f, c * depth).Normalized);
                    m.Uvs.Add(new V2(s / (float)sides, -y / tileMeters));
                }
            }
            int row = sides + 1;
            for (int r = 0; r < count - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = r * row + s, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    Oriented(m, a0, b0, a1, Radial(m, a0, a1, b0));
                    Oriented(m, a1, b0, b1, Radial(m, a1, b0, b1));
                }
            Cap(m, 0, sides, new V3(0f, 1f, 0f));
            Cap(m, (count - 1) * row, sides, new V3(0f, -1f, 0f));
            return m;
        }

        private static V3 Radial(MeshData m, int a, int b, int c)
        {
            V3 p = (m.Positions[a] + m.Positions[b] + m.Positions[c]) * (1f / 3f);
            return new V3(p.X, 0f, p.Z);
        }

        /// <summary>Tapa plana (abanico) con su propio anillo de vértices para que la normal sea la de la tapa.</summary>
        private static void Cap(MeshData m, int ringStart, int sides, V3 normal)
        {
            int center = m.Positions.Count;
            V3 c = m.Positions[ringStart] + (m.Positions[ringStart + sides / 2] - m.Positions[ringStart]) * 0.5f;
            m.Positions.Add(c); m.Normals.Add(normal); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(0.5f, 0.5f));
            int first = m.Positions.Count;
            for (int s = 0; s <= sides; s++)
            {
                V3 p = m.Positions[ringStart + s];
                m.Positions.Add(p); m.Normals.Add(normal); m.Tangents.Add(new V3(1f, 0f, 0f)); m.Uvs.Add(new V2(0.5f + (p.X - c.X) * 2f, 0.5f + (p.Z - c.Z) * 2f));
            }
            for (int s = 0; s < sides; s++) Oriented(m, center, first + s, first + s + 1, normal);
        }

        private static void Oriented(MeshData m, int a, int b, int c, V3 outward)
        {
            bool flip = !Winding.FacesOutward(m.Positions[a], m.Positions[b], m.Positions[c], outward);
            m.Triangles.Add(a); m.Triangles.Add(flip ? c : b); m.Triangles.Add(flip ? b : c);
        }
    }

    /// <summary>Medidas de la bota respecto al tobillo (medidas de la malla real al construirla).</summary>
    public struct FootShape
    {
        public float Drop;   // del tobillo a la suela (m)
        public float Heel;   // el talón queda esto detrás del tobillo
        public float Ball;   // la articulación de la punta (bola del pie) queda esto delante del tobillo, a la altura de la suela
        public float Toe;    // largo del segmento de la punta desde la bola

        public static FootShape FromSpec(float ankleHeight, float toeJoint) => new FootShape { Drop = ankleHeight, Heel = 0.07f, Ball = toeJoint, Toe = 0.075f };
    }

    /// <summary>
    /// Regla dura del pie (pura): con el pie inclinado "pitch" (+ punta abajo) y la punta doblada "toeBend" (+ hacia arriba respecto al pie), el tobillo debe estar
    /// al menos a esta altura sobre el suelo para que NINGÚN punto de la suela (talón, bola, punta) quede bajo el suelo. Incluye el margen de suela.
    /// </summary>
    public static class FootGeometry
    {
        public static float MinAnkleHeight(FootShape s, float pitchDeg, float toeBendDeg)
        {
            double p = pitchDeg * Math.PI / 180.0, tp = (pitchDeg - toeBendDeg) * Math.PI / 180.0;
            float cos = (float)Math.Cos(p), sin = (float)Math.Sin(p);
            float heel = s.Drop * cos - s.Heel * sin;      // cuánto baja cada punto bajo el tobillo
            float ball = s.Drop * cos + s.Ball * sin;
            float tip = ball + s.Toe * (float)Math.Sin(tp);
            return Math.Max(heel, Math.Max(ball, tip)) + LegSolver.Sole;
        }

        /// <summary>Doblado de la punta para que quede plana en el suelo con el pie inclinado (limitado a lo que dobla un pie: 0..75°).</summary>
        public static float FlatToeBend(float pitchDeg) => Math.Max(0f, Math.Min(75f, pitchDeg));
    }
}

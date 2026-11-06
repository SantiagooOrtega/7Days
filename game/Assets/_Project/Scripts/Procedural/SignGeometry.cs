using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum SignType { Stop, Yield, Speed, NoTurn, Hazard, Generic }

    /// <summary>
    /// Formas genéricas de placas (octágono, triángulos, círculo, rectángulo; sin logos ni textos protegidos) y su malla:
    /// frente hacia +Z (submesh 0, con la textura de la señal) y dorso + cantos (submesh 1, metal). Las UV del frente
    /// coinciden con las coordenadas que usa SignGenerator, con u hacia la derecha de quien mira la placa (por eso u = 0.5 - x/w).
    /// </summary>
    public static class SignGeometry
    {
        public const float Thickness = 0.02f;

        /// <summary>Ancho y alto de la placa en metros.</summary>
        public static (float w, float h) Dimensions(SignType t)
        {
            switch (t)
            {
                case SignType.Stop: return (0.76f, 0.76f);
                case SignType.Yield: return (0.9f, 0.78f);
                case SignType.Speed: return (0.62f, 0.62f);
                case SignType.NoTurn: return (0.62f, 0.62f);
                case SignType.Hazard: return (0.9f, 0.78f);
                default: return (0.96f, 0.64f);
            }
        }

        /// <summary>Contorno convexo centrado en el origen, en sentido antihorario (en el plano XY, visto desde +Z).</summary>
        public static List<V2> Outline(SignType t)
        {
            var pts = new List<V2>();
            switch (t)
            {
                case SignType.Stop:
                {
                    float r = 0.38f / (float)Math.Cos(Math.PI / 8);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = Math.PI / 8 + i * Math.PI / 4;
                        pts.Add(new V2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r));
                    }
                    break;
                }
                case SignType.Yield:   // triángulo invertido
                    pts.Add(new V2(-0.45f, 0.39f)); pts.Add(new V2(0f, -0.39f)); pts.Add(new V2(0.45f, 0.39f));
                    break;
                case SignType.Hazard:  // triángulo hacia arriba
                    pts.Add(new V2(-0.45f, -0.39f)); pts.Add(new V2(0.45f, -0.39f)); pts.Add(new V2(0f, 0.39f));
                    break;
                case SignType.Speed:
                case SignType.NoTurn:
                {
                    const int n = 32;
                    for (int i = 0; i < n; i++)
                    {
                        double a = i * 2 * Math.PI / n;
                        pts.Add(new V2((float)Math.Cos(a) * 0.31f, (float)Math.Sin(a) * 0.31f));
                    }
                    break;
                }
                default:
                    pts.Add(new V2(-0.48f, -0.32f)); pts.Add(new V2(0.48f, -0.32f)); pts.Add(new V2(0.48f, 0.32f)); pts.Add(new V2(-0.48f, 0.32f));
                    break;
            }
            return pts;
        }

        /// <summary>Distancia con signo al contorno (negativa dentro), en metros. Exacta para polígonos convexos en el interior.</summary>
        public static float SignedDistance(IList<V2> poly, float x, float y)
        {
            float d = -1e9f;
            for (int i = 0; i < poly.Count; i++)
            {
                V2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float ex = b.X - a.X, ey = b.Y - a.Y;
                float len = (float)Math.Sqrt(ex * ex + ey * ey);
                // normal exterior de una arista antihoraria = (ey, -ex)
                float nx = ey / len, ny = -ex / len;
                d = Math.Max(d, (x - a.X) * nx + (y - a.Y) * ny);
            }
            return d;
        }

        public static MeshData Plate(SignType type)
        {
            var poly = Outline(type);
            var (w, h) = Dimensions(type);
            var m = new MeshData { TangentSign = -1f };
            float half = Thickness * 0.5f;
            int n = poly.Count;

            // Frente (z = +half): abanico desde el primer vértice
            int s = m.Positions.Count;
            for (int i = 0; i < n; i++)
            {
                m.Positions.Add(new V3(poly[i].X, poly[i].Y, half));
                m.Normals.Add(new V3(0, 0, 1));
                m.Tangents.Add(new V3(-1, 0, 0)); // u crece hacia -X: es la derecha de quien mira la placa desde +Z (no sale espejada)
                m.Uvs.Add(new V2(0.5f - poly[i].X / w, poly[i].Y / h + 0.5f));
            }
            for (int i = 1; i < n - 1; i++) AddOriented(m, s, s + i, s + i + 1, new V3(0, 0, 1));
            m.SubmeshSplit = m.Triangles.Count;

            // Dorso (z = -half)
            int b0 = m.Positions.Count;
            for (int i = 0; i < n; i++)
            {
                m.Positions.Add(new V3(poly[i].X, poly[i].Y, -half));
                m.Normals.Add(new V3(0, 0, -1));
                m.Tangents.Add(new V3(1, 0, 0));
                m.Uvs.Add(new V2(poly[i].X / w + 0.5f, poly[i].Y / h + 0.5f)); // dorso: se ve desde -Z, u crece hacia +X
            }
            for (int i = 1; i < n - 1; i++) AddOriented(m, b0, b0 + i, b0 + i + 1, new V3(0, 0, -1));

            // Cantos
            for (int i = 0; i < n; i++)
            {
                V2 a = poly[i], b = poly[(i + 1) % n];
                V3 nrm = new V3(b.Y - a.Y, -(b.X - a.X), 0).Normalized;
                int e = m.Positions.Count;
                foreach (var p in new[] { new V3(a.X, a.Y, half), new V3(b.X, b.Y, half), new V3(b.X, b.Y, -half), new V3(a.X, a.Y, -half) })
                {
                    m.Positions.Add(p);
                    m.Normals.Add(nrm);
                    m.Tangents.Add(new V3(0, 0, 1));
                    m.Uvs.Add(new V2(0, 0));
                }
                AddOriented(m, e, e + 1, e + 2, nrm);
                AddOriented(m, e, e + 2, e + 3, nrm);
            }
            return m;
        }

        private static void AddOriented(MeshData m, int a, int b, int c, V3 outward)
        {
            V3 p0 = m.Positions[a], p1 = m.Positions[b], p2 = m.Positions[c];
            bool flip = !Winding.FacesOutward(p0, p1, p2, outward); // la normal frontal de Unity es +Cross
            m.Triangles.Add(a); m.Triangles.Add(flip ? c : b); m.Triangles.Add(flip ? b : c);
        }
    }
}

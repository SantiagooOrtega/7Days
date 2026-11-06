using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Convención de caras de Unity en UN solo lugar, para que ningún generador la reinvente:
    ///  - El FRENTE de un triángulo es el lado desde el que sus vértices (a, b, c) se ven en sentido HORARIO.
    ///  - La normal del frente es +Cross(b - a, c - a). (Ejemplo del manual de Unity: vértices 0:(0,0,0) 2:(0,h,0) 1:(w,0,0)
    ///    dan Cross = (0,0,-hw), es decir -Z, que es la normal que el manual le asigna al quad.)
    ///  - Una textura NO espejada tiene u hacia la derecha y v hacia arriba vistos desde el frente: Cross(dP/du, dP/dv) apunta
    ///    HACIA DENTRO de la superficie (contra la normal).
    ///  - Con esos u y v, la tangente es dP/du y su w debe ser -1 (Unity calcula bitangente = Cross(normal, tangente) * w).
    /// </summary>
    public static class Winding
    {
        public static V3 FrontNormal(V3 a, V3 b, V3 c) => V3.Cross(b - a, c - a);

        /// <summary>Volumen con signo de una malla cerrada: positivo si todos los triángulos miran hacia afuera.</summary>
        public static float SignedVolume(MeshData m)
        {
            double vol = 0;
            for (int i = 0; i < m.Triangles.Count; i += 3)
            {
                V3 a = m.Positions[m.Triangles[i]], b = m.Positions[m.Triangles[i + 1]], c = m.Positions[m.Triangles[i + 2]];
                // determinante a·(b×c)/6 con la normal frontal = +Cross(b-a,c-a): positivo para una malla orientada hacia afuera
                vol += V3.Dot(a, V3.Cross(b, c)) / 6.0;
            }
            return (float)vol;
        }

        /// <summary>true si el triángulo (a,b,c) mira hacia "outward" según la convención de Unity.</summary>
        public static bool FacesOutward(V3 a, V3 b, V3 c, V3 outward) => V3.Dot(FrontNormal(a, b, c), outward) > 0f;
    }
}

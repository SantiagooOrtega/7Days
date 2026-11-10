using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Un árbol en un nivel de detalle: tronco (corteza) y copa (agujas o ramas secas), cada uno con su material.</summary>
    public sealed class ForestTreeMesh
    {
        public MeshData Trunk = new MeshData(), Crown = new MeshData();
        public float Height, TrunkRadius;
        public int Triangles => (Trunk.Triangles.Count + Crown.Triangles.Count) / 3;
    }

    /// <summary>
    /// Geometría del bosque (pura): 3 pinos y 2 árboles muertos altos (5 variantes de varias alturas) en 3 niveles de detalle (LOD 0 completo, LOD 1 medio y LOD 2
    /// una "cruz" de 4 caras para lo lejano), helechos, rocas con musgo, troncos caídos y la caravana oxidada. Diseños propios hechos con tornos y cajas.
    /// </summary>
    public static class ForestGeometry
    {
        public const int PineVariants = 3, TallVariants = 2;
        private static readonly AtlasCell Cell = new AtlasCell(0f, 0f, 1f, 0f);

        private static float F(Random r) => (float)r.NextDouble();

        /// <summary>Altura de cada variante (m): pinos de 9, 12 y 15 m; árboles muertos de 10 y 13 m.</summary>
        public static float HeightOf(string type, int variant) => type == PropTypes.Pine ? 9f + 3f * variant : 10f + 3f * variant;
        public static float TrunkRadiusOf(string type, int variant) => type == PropTypes.Pine ? 0.22f + 0.05f * variant : 0.3f + 0.06f * variant;

        public static ForestTreeMesh Tree(string type, int variant, int lod)
        {
            lod = Math.Max(0, Math.Min(2, lod));
            return type == PropTypes.Pine ? Pine(variant % PineVariants, lod) : Tall(variant % TallVariants, lod);
        }

        private static int Sides(int lod, int full) => lod == 0 ? full : (lod == 1 ? Math.Max(5, full / 2 + 1) : 4);

        /// <summary>Pino: tronco cónico y capas de copa (conos apilados que se estrechan); en el LOD 2 un solo cono de 4 caras.</summary>
        private static ForestTreeMesh Pine(int v, int lod)
        {
            var t = new ForestTreeMesh { Height = HeightOf(PropTypes.Pine, v), TrunkRadius = TrunkRadiusOf(PropTypes.Pine, v) };
            var rng = new Random(101 + v * 17);
            float h = t.Height, r = t.TrunkRadius;
            var trunk = new MeshBuilder();
            trunk.Lathe(default, default, lod == 2 ? new[] { 0f, r * 1.1f, h * 0.9f, r * 0.15f } : new[] { 0f, r * 1.25f, h * 0.15f, r, h * 0.85f, r * 0.35f, h * 0.97f, r * 0.08f }, Sides(lod, 9), 1f, false, true, Cell, 2f);
            var crown = new MeshBuilder();
            if (lod == 2)
                crown.Lathe(new V3(0f, h * 0.22f, 0f), default, new[] { 0f, h * 0.24f, h * 0.78f, 0.05f }, 4, 1f, true, false, Cell, 2f);
            else
            {
                int layers = lod == 0 ? 6 : 3;
                for (int i = 0; i < layers; i++)
                {
                    float k = i / (float)layers;
                    float y0 = h * (0.22f + 0.7f * k), rad = h * 0.26f * (1f - 0.78f * k) * (0.9f + 0.2f * F(rng)), len = h * (0.24f - 0.1f * k);
                    // cada capa: un cono ancho con el borde caído (falda) y ligeramente girado para que no parezcan idénticas
                    crown.Lathe(new V3(0f, y0, 0f), new V3(0f, F(rng) * 60f, 0f), new[] { -len * 0.12f, rad * 0.75f, 0f, rad, len * 0.25f, rad * 0.7f, len, 0.04f }, Sides(lod, 10), 0.92f + 0.16f * F(rng), true, false, Cell, 2f);
                }
            }
            t.Trunk = trunk.Data; t.Crown = crown.Data;
            return t;
        }

        /// <summary>Árbol muerto alto: tronco torcido con ramas secas que suben; en el LOD 2, tronco y dos ramas en cruz.</summary>
        private static ForestTreeMesh Tall(int v, int lod)
        {
            var t = new ForestTreeMesh { Height = HeightOf(PropTypes.TreeTall, v), TrunkRadius = TrunkRadiusOf(PropTypes.TreeTall, v) };
            var rng = new Random(303 + v * 29);
            float h = t.Height, r = t.TrunkRadius;
            var trunk = new MeshBuilder();
            trunk.Lathe(default, default, lod == 2 ? new[] { 0f, r * 1.2f, h, r * 0.15f } : new[] { 0f, r * 1.4f, h * 0.1f, r, h * 0.6f, r * 0.6f, h, r * 0.12f }, Sides(lod, 8), 1f, false, true, Cell, 2f);
            var crown = new MeshBuilder();
            int branches = lod == 0 ? 8 : (lod == 1 ? 4 : 2);
            for (int i = 0; i < branches; i++)
            {
                float y = h * (0.45f + 0.5f * i / Math.Max(1, branches)), len = h * (0.32f - 0.15f * i / Math.Max(1, branches)) * (0.8f + 0.4f * F(rng));
                float yaw = i * 137.5f + F(rng) * 30f, tilt = 35f + F(rng) * 25f;
                crown.Lathe(new V3(0f, y, 0f), new V3(tilt, yaw, 0f), new[] { 0f, r * 0.45f, len, 0.02f }, lod == 2 ? 3 : Sides(lod, 5), 1f, lod < 2, false, Cell, 1f);
            }
            t.Trunk = trunk.Data; t.Crown = crown.Data;
            return t;
        }

        /// <summary>Helecho: frondas planas arqueadas que salen del centro (material de doble cara, sin sombras).</summary>
        public static MeshData Fern(int seed)
        {
            var rng = new Random(seed * 13 + 5);
            var mb = new MeshBuilder();
            int fronds = 7;
            for (int i = 0; i < fronds; i++)
            {
                float yaw = i * 360f / fronds + F(rng) * 20f, len = 0.55f + 0.3f * F(rng);
                mb.Box(new V3((float)Math.Sin(yaw * Math.PI / 180) * len * 0.45f, 0.22f, (float)Math.Cos(yaw * Math.PI / 180) * len * 0.45f), new V3(0.16f, 0.01f, len), new V3(-28f - 15f * F(rng), yaw, 0f), 0f, Cell, 1f);
            }
            return mb.Data;
        }

        /// <summary>Roca redondeada y achatada (tornos con radio irregular).</summary>
        public static MeshData Boulder(int variant)
        {
            var rng = new Random(77 + variant * 11);
            var mb = new MeshBuilder();
            mb.Lathe(default, new V3(0f, F(rng) * 90f, 0f), new[] { -0.45f, 0.35f, -0.25f, 0.9f, 0.1f, 1f, 0.45f, 0.75f, 0.7f, 0.25f }, 9, 0.75f + 0.25f * F(rng), true, true, Cell, 1f);
            return mb.Data;
        }

        /// <summary>Tronco caído (a lo largo de X), radio 1 y largo 1: se escala con la pieza.</summary>
        public static MeshData Trunk()
        {
            var mb = new MeshBuilder();
            mb.Lathe(default, new V3(0f, 0f, 90f), new[] { -0.5f, 0.92f, -0.2f, 1f, 0.2f, 0.95f, 0.5f, 0.85f }, 9, 1f, true, true, Cell, 1f);
            return mb.Data;
        }

        /// <summary>Lista ordenada de los árboles del bosque (tipo, variante) para el renderizador.</summary>
        public static IEnumerable<(string type, int variant)> TreeKinds()
        {
            for (int v = 0; v < PineVariants; v++) yield return (PropTypes.Pine, v);
            for (int v = 0; v < TallVariants; v++) yield return (PropTypes.TreeTall, v);
        }
    }
}

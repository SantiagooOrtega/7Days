using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Dónde va una herida: hueso, posición a lo largo del hueso (0 al 1), ángulo alrededor (0 delante, +90 a la derecha) y tamaño; la forma es una celda del atlas de heridas.</summary>
    public struct WoundSpot
    {
        public string Id;
        public HitZone Zone;
        public int Stage;           // 1..3 capa de daño por cuartos de vida; 0 = marca propia del individuo
        public int Slot;            // para las marcas del individuo: bit de ZombieIndividual.WoundMask
        public BoneId Bone;
        public float T, Angle, Size;
        public int Cell;            // celda sobre piel
    }

    /// <summary>Un decal ya generado: malla en el espacio del hueso, pegada a la superficie real (piel o ropa) 3 mm por fuera.</summary>
    public sealed class WoundPatch
    {
        public WoundSpot Spot;
        public MeshData Mesh;
        public bool OnCloth;
    }

    /// <summary>
    /// Heridas del zombi como decals: 12 de la capa de daño (3 por zona, que se revelan al bajar la vida al 75, 50 y 25 %) y 8 marcas propias de cada individuo (mordidas, tajos,
    /// moratones, costras, manchas). Cada decal se proyecta con rayos sobre la malla REAL del cuerpo (la superficie más externa: la ropa si la hay, donde sale un desgarro), así
    /// se pega a cada tipo y altura sin flotar ni hundirse.
    /// </summary>
    public static class ZombieWounds
    {
        public const int Grid = 4;
        public const float Lift = 0.003f;

        public static List<WoundSpot> Spots()
        {
            var l = new List<WoundSpot>();
            void D(HitZone z, int stage, BoneId b, float t, float ang, float size, int cell) => l.Add(new WoundSpot { Id = z + "" + stage, Zone = z, Stage = stage, Bone = b, T = t, Angle = ang, Size = size, Cell = cell });
            D(HitZone.Head, 1, BoneId.Head, 0.45f, 40f, 0.05f, ZombieAtlas.WdHead); D(HitZone.Head, 2, BoneId.Head, 0.5f, -45f, 0.055f, ZombieAtlas.WdGash); D(HitZone.Head, 3, BoneId.Head, 0.7f, 5f, 0.06f, ZombieAtlas.WdBullet);
            D(HitZone.Torso, 1, BoneId.Chest, 0.55f, 25f, 0.075f, ZombieAtlas.WdBullet); D(HitZone.Torso, 2, BoneId.Chest, 0.3f, -30f, 0.1f, ZombieAtlas.WdGash); D(HitZone.Torso, 3, BoneId.UpperChest, 0.4f, 8f, 0.15f, ZombieAtlas.WdStain);
            D(HitZone.Arms, 1, BoneId.RightLowerArm, 0.5f, 45f, 0.08f, ZombieAtlas.WdGash); D(HitZone.Arms, 2, BoneId.LeftUpperArm, 0.5f, -45f, 0.09f, ZombieAtlas.WdBullet); D(HitZone.Arms, 3, BoneId.RightUpperArm, 0.5f, 70f, 0.12f, ZombieAtlas.WdStain);
            D(HitZone.Legs, 1, BoneId.LeftUpperLeg, 0.45f, 20f, 0.08f, ZombieAtlas.WdBullet); D(HitZone.Legs, 2, BoneId.RightLowerLeg, 0.5f, -20f, 0.1f, ZombieAtlas.WdGash); D(HitZone.Legs, 3, BoneId.RightUpperLeg, 0.45f, 30f, 0.13f, ZombieAtlas.WdStain);
            int slot = 0;
            void I(string id, BoneId b, float t, float ang, float size, int cell) { l.Add(new WoundSpot { Id = id, Zone = HitZone.Torso, Stage = 0, Slot = slot++, Bone = b, T = t, Angle = ang, Size = size, Cell = cell }); }
            I("mordida cuello", BoneId.Neck, 0.5f, 70f, 0.07f, ZombieAtlas.WdBite); I("mordida hombro", BoneId.UpperChest, 0.8f, -80f, 0.085f, ZombieAtlas.WdBiteB);
            I("tajo antebrazo", BoneId.LeftLowerArm, 0.5f, 40f, 0.1f, ZombieAtlas.WdGashB); I("mancha costado", BoneId.Chest, 0.4f, 90f, 0.16f, ZombieAtlas.WdStainB);
            I("moraton muslo", BoneId.RightUpperLeg, 0.5f, -10f, 0.12f, ZombieAtlas.WdBruise); I("costra cara", BoneId.Head, 0.45f, -30f, 0.05f, ZombieAtlas.WdScab);
            I("salpicadura", BoneId.RightLowerArm, 0.6f, 0f, 0.12f, ZombieAtlas.WdSplat); I("chorreras vientre", BoneId.Spine, 0.5f, 0f, 0.14f, ZombieAtlas.WdDrip);
            return l;
        }

        private static BoneId Child(BoneId b)
        {
            switch (b)
            {
                case BoneId.Hips: return BoneId.Spine; case BoneId.Spine: return BoneId.Chest; case BoneId.Chest: return BoneId.UpperChest; case BoneId.UpperChest: return BoneId.Neck;
                case BoneId.Neck: return BoneId.Head; case BoneId.LeftUpperArm: return BoneId.LeftLowerArm; case BoneId.RightUpperArm: return BoneId.RightLowerArm;
                case BoneId.LeftLowerArm: return BoneId.LeftHand; case BoneId.RightLowerArm: return BoneId.RightHand; case BoneId.LeftUpperLeg: return BoneId.LeftLowerLeg;
                case BoneId.RightUpperLeg: return BoneId.RightLowerLeg; case BoneId.LeftLowerLeg: return BoneId.LeftFoot; case BoneId.RightLowerLeg: return BoneId.RightFoot;
                default: return b;
            }
        }

        public static List<WoundPatch> Build(ZombieModelResult model, ZombieSkinned skinned, ZombieAnatomy a)
        {
            // triángulos de toda la superficie (piel y ropa) con la marca de a qué parte pertenecen
            var tris = new List<(V3 a, V3 b, V3 c, bool cloth)>();
            foreach (var part in new[] { skinned.Skin, skinned.Cloth })
            {
                bool cloth = part == skinned.Cloth;
                var m = part.Mesh;
                for (int i = 0; i < m.Triangles.Count; i += 3) tris.Add((m.Positions[m.Triangles[i]], m.Positions[m.Triangles[i + 1]], m.Positions[m.Triangles[i + 2]], cloth));
            }
            var list = new List<WoundPatch>();
            foreach (var spot in Spots())
            {
                var patch = Project(model, tris, spot, a);
                if (patch != null) list.Add(patch);
            }
            return list;
        }

        private static WoundPatch Project(ZombieModelResult model, List<(V3 a, V3 b, V3 c, bool cloth)> tris, WoundSpot spot, ZombieAnatomy a)
        {
            V3 start = HumanoidRigSpec.RestPosition(model.Spec, spot.Bone);
            BoneId child = Child(spot.Bone);
            V3 end = child == spot.Bone ? start + new V3(0f, 0.22f * a.Proportions.headSize * a.Size, 0f) : HumanoidRigSpec.RestPosition(model.Spec, child);
            V3 axisPoint = start + (end - start) * spot.T;
            double ang = spot.Angle * Math.PI / 180.0;
            V3 dir = new V3((float)Math.Sin(ang), 0f, (float)Math.Cos(ang));
            float size = spot.Size * a.Size * (spot.Bone == BoneId.Head ? a.Proportions.headSize : 1f);
            V3 up = new V3(0f, 1f, 0f);
            V3 right = V3.Cross(up, dir).Normalized;   // tangente horizontal a la superficie
            // primero el centro, para saber si cae sobre ropa y ampliar el desgarro
            if (!Cast(tris, axisPoint, dir, out V3 centerHit, out V3 centerN, out bool cloth)) return null;
            int cell = spot.Cell;
            if (cloth && spot.Stage > 0) { cell = ZombieAtlas.WdTear; size *= 1.35f; }
            var mesh = new MeshData();
            var atlas = ZombieAtlas.Wound(cell);
            V3 origin = HumanoidRigSpec.RestPosition(model.Spec, spot.Bone);
            var pts = new V3[(Grid + 1) * (Grid + 1)]; var nrm = new V3[(Grid + 1) * (Grid + 1)];
            for (int j = 0; j <= Grid; j++)
                for (int i = 0; i <= Grid; i++)
                {
                    float du = (i / (float)Grid - 0.5f) * size, dv = (j / (float)Grid - 0.5f) * size;
                    V3 o = axisPoint + right * du + up * dv;
                    V3 p, n;
                    if (!Cast(tris, o, dir, out p, out n, out _)) { p = centerHit + right * du + up * dv; n = centerN; }
                    pts[j * (Grid + 1) + i] = p + n * Lift; nrm[j * (Grid + 1) + i] = n;
                }
            for (int j = 0; j <= Grid; j++)
                for (int i = 0; i <= Grid; i++)
                {
                    int k = j * (Grid + 1) + i;
                    mesh.Positions.Add(pts[k] - origin); mesh.Normals.Add(nrm[k]); mesh.Tangents.Add(right);
                    mesh.TangentW.Add(-1f);
                    var uv = atlas.Map(i / (float)Grid, j / (float)Grid);
                    mesh.Uvs.Add(uv);
                }
            for (int j = 0; j < Grid; j++)
                for (int i = 0; i < Grid; i++)
                {
                    int a0 = j * (Grid + 1) + i, a1 = a0 + 1, b0 = a0 + Grid + 1, b1 = b0 + 1;
                    AddTri(mesh, a0, b0, a1, dir); AddTri(mesh, a1, b0, b1, dir);
                }
            return new WoundPatch { Spot = spot, Mesh = mesh, OnCloth = cloth };
        }

        private static void AddTri(MeshData m, int a, int b, int c, V3 outward)
        {
            bool flip = !Winding.FacesOutward(m.Positions[a], m.Positions[b], m.Positions[c], outward);
            m.Triangles.Add(a); m.Triangles.Add(flip ? c : b); m.Triangles.Add(flip ? b : c);
        }

        /// <summary>Lanza un rayo desde "axisPoint + dir * 0.8" hacia el eje y devuelve la primera superficie que toca (la más externa).</summary>
        private static bool Cast(List<(V3 a, V3 b, V3 c, bool cloth)> tris, V3 axisPoint, V3 dir, out V3 hit, out V3 normal, out bool cloth)
        {
            V3 o = axisPoint + dir * 0.8f, d = -dir;
            float best = float.MaxValue; hit = default; normal = dir; cloth = false;
            foreach (var t in tris)
            {
                if (!Ray(o, d, t.a, t.b, t.c, out float dist) || dist >= best) continue;
                best = dist; hit = o + d * dist; cloth = t.cloth;
                V3 n = Winding.FrontNormal(t.a, t.b, t.c).Normalized;
                normal = V3.Dot(n, dir) < 0f ? -n : n;
            }
            return best < float.MaxValue;
        }

        private static bool Ray(V3 o, V3 d, V3 a, V3 b, V3 c, out float t)
        {
            t = 0f;
            V3 e1 = b - a, e2 = c - a, p = V3.Cross(d, e2);
            float det = V3.Dot(e1, p);
            if (Math.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            V3 s = o - a;
            float u = V3.Dot(s, p) * inv; if (u < 0f || u > 1f) return false;
            V3 q = V3.Cross(s, e1);
            float v = V3.Dot(d, q) * inv; if (v < 0f || u + v > 1f) return false;
            t = V3.Dot(e2, q) * inv;
            return t > 1e-5f;
        }
    }
}

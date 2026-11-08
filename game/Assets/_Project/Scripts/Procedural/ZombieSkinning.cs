using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Hasta 4 huesos por vértice (la suma de pesos es 1). Los índices son del esqueleto (BoneId) y, después de los 22, de los huesos extra.</summary>
    public struct SkinWeight
    {
        public int B0, B1, B2, B3;
        public float W0, W1, W2, W3;

        public int Count => (W0 > 0f ? 1 : 0) + (W1 > 0f ? 1 : 0) + (W2 > 0f ? 1 : 0) + (W3 > 0f ? 1 : 0);
        public float Sum => W0 + W1 + W2 + W3;
        public float Of(int bone) => (B0 == bone ? W0 : 0f) + (B1 == bone ? W1 : 0f) + (B2 == bone ? W2 : 0f) + (B3 == bone ? W3 : 0f);
    }

    /// <summary>Malla de un material con los vértices ya en el espacio del cuerpo (pose de reposo) y sus pesos de hueso.</summary>
    public class SkinnedPart
    {
        public readonly MeshData Mesh = new MeshData();
        public readonly List<SkinWeight> Weights = new List<SkinWeight>();
        /// <summary>Qué pieza originó cada vértice (índice en ZombieModelResult.Pieces), para las pruebas y para ocultar piezas sueltas.</summary>
        public readonly List<int> PieceOf = new List<int>();
    }

    public class ZombieSkinned
    {
        public SkinnedPart Skin = new SkinnedPart(), Cloth = new SkinnedPart();
        public int BoneCount;
        public V3[] RestBonePositions;

        public int Triangles => (Skin.Mesh.Triangles.Count + Cloth.Mesh.Triangles.Count) / 3;
    }

    /// <summary>
    /// Une todas las piezas en dos mallas (piel y ropa) para dibujar cada zombi con 2 llamadas en un solo SkinnedMeshRenderer. Los vértices de las piezas "Blend" (torso,
    /// extremidades) se reparten entre los dos huesos de cada articulación según su posición (codos, rodillas, muñecas, columna y base del cuello se doblan sin romperse);
    /// el resto (manos, pies, cabeza, armadura, accesorios) va rígido a su hueso. El peso depende solo de la POSICIÓN respecto a la articulación, así las piezas de piel y ropa
    /// que se cruzan en ella se doblan igual y no se separan.
    /// </summary>
    public static class ZombieSkinning
    {
        private struct Joint { public BoneId A, C; public float Half; }

        private static Joint J(BoneId a, BoneId c, float half) => new Joint { A = a, C = c, Half = half };

        /// <summary>Articulaciones que se mezclan (mitad del ancho de la zona de mezcla en metros a escala del soldado).</summary>
        private static readonly Joint[] Joints =
        {
            J(BoneId.Hips, BoneId.Spine, 0.055f), J(BoneId.Spine, BoneId.Chest, 0.055f), J(BoneId.Chest, BoneId.UpperChest, 0.055f), J(BoneId.UpperChest, BoneId.Neck, 0.035f),
            J(BoneId.LeftUpperArm, BoneId.LeftLowerArm, 0.045f), J(BoneId.RightUpperArm, BoneId.RightLowerArm, 0.045f),
            J(BoneId.LeftLowerArm, BoneId.LeftHand, 0.028f), J(BoneId.RightLowerArm, BoneId.RightHand, 0.028f),
            J(BoneId.LeftUpperLeg, BoneId.LeftLowerLeg, 0.06f), J(BoneId.RightUpperLeg, BoneId.RightLowerLeg, 0.06f),
            J(BoneId.LeftLowerLeg, BoneId.LeftFoot, 0.04f), J(BoneId.RightLowerLeg, BoneId.RightFoot, 0.04f),
        };

        public static float Smooth(float e0, float e1, float x) { float k = Math.Max(0f, Math.Min(1f, (x - e0) / (e1 - e0))); return k * k * (3f - 2f * k); }

        /// <summary>Pesos de un punto (espacio del cuerpo) de una pieza ligada al hueso "bone" que se mezcla en las articulaciones.</summary>
        public static SkinWeight BlendWeights(BoneDef[] spec, BoneId bone, V3 p, float size)
        {
            var w = new float[HumanoidRigSpec.BoneCount];
            w[(int)bone] = 1f;
            foreach (var j in Joints)
            {
                if (j.A != bone && j.C != bone) continue;
                V3 pa = HumanoidRigSpec.RestPosition(spec, j.A), pc = HumanoidRigSpec.RestPosition(spec, j.C);
                V3 d = (pc - pa).Normalized;
                float h = j.Half * size;
                float x = V3.Dot(p - pc, d);
                float t = w[(int)j.A] + w[(int)j.C];
                float wc = Smooth(-h, h, x);
                w[(int)j.C] = t * wc; w[(int)j.A] = t * (1f - wc);
            }
            return Pack(w, null);
        }

        private static SkinWeight Pack(float[] w, int[] ids)
        {
            // los 4 mayores, normalizados
            int n = w.Length;
            var idx = new int[4]; var val = new float[4];
            for (int k = 0; k < 4; k++)
            {
                int best = -1; float bv = 1e-4f;
                for (int i = 0; i < n; i++)
                {
                    bool used = false; for (int q = 0; q < k; q++) if (idx[q] == i) used = true;
                    if (!used && w[i] > bv) { bv = w[i]; best = i; }
                }
                idx[k] = best < 0 ? 0 : best; val[k] = best < 0 ? 0f : bv;
            }
            float sum = val[0] + val[1] + val[2] + val[3];
            if (sum <= 0f) { val[0] = 1f; sum = 1f; idx[0] = 0; }
            return new SkinWeight { B0 = idx[0], W0 = val[0] / sum, B1 = idx[1], W1 = val[1] / sum, B2 = idx[2], W2 = val[2] / sum, B3 = idx[3], W3 = val[3] / sum };
        }

        public static ZombieSkinned Flatten(ZombieModelResult r, float size)
        {
            var z = new ZombieSkinned { BoneCount = HumanoidRigSpec.BoneCount + r.Extras.Count };
            z.RestBonePositions = new V3[z.BoneCount];
            for (int i = 0; i < HumanoidRigSpec.BoneCount; i++) z.RestBonePositions[i] = HumanoidRigSpec.RestPosition(r.Spec, (BoneId)i);
            for (int e = 0; e < r.Extras.Count; e++) z.RestBonePositions[HumanoidRigSpec.BoneCount + e] = HumanoidRigSpec.RestPosition(r.Spec, r.Extras[e].Parent) + r.Extras[e].LocalPos;

            for (int pi = 0; pi < r.Pieces.Count; pi++)
            {
                var p = r.Pieces[pi];
                var part = p.Slot == ZombieModel.SlotCloth ? z.Cloth : z.Skin;
                V3 origin = p.Extra >= 0 ? z.RestBonePositions[HumanoidRigSpec.BoneCount + p.Extra] : z.RestBonePositions[(int)p.Bone];
                int baseIndex = part.Mesh.Positions.Count;
                var m = p.Mesh;
                for (int i = 0; i < m.Positions.Count; i++)
                {
                    V3 pos = origin + p.Pivot + m.Positions[i];
                    part.Mesh.Positions.Add(pos); part.Mesh.Normals.Add(m.Normals[i]); part.Mesh.Tangents.Add(m.Tangents[i]);
                    part.Mesh.TangentW.Add(m.WOf(i)); part.Mesh.Uvs.Add(m.Uvs[i]);
                    part.PieceOf.Add(pi);
                    if (p.Extra >= 0) part.Weights.Add(new SkinWeight { B0 = HumanoidRigSpec.BoneCount + p.Extra, W0 = 1f });
                    else if (p.Blend) part.Weights.Add(BlendWeights(r.Spec, p.Bone, pos, size));
                    else part.Weights.Add(new SkinWeight { B0 = (int)p.Bone, W0 = 1f });
                }
                foreach (int t in m.Triangles) part.Mesh.Triangles.Add(baseIndex + t);
            }
            return z;
        }
    }
}

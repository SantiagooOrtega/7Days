using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Animation
{
    /// <summary>
    /// Mide el agarre en las MALLAS reales: distancia de cada mano visible a su punto de agarre, cuánto entran los dedos en el arma, cuánto entra el arma en el
    /// torso, flexión y dirección de los codos y piezas de mochila. La usan las pruebas visuales, el ciclo F7 y el panel F6.
    /// </summary>
    public static class GripProbe
    {
        public struct Reading
        {
            public float RightError, LeftError;     // m: mano visible (centro de agarre de la palma) a su punto de agarre; -1 si esa mano no agarra
            public float FingerPenetration;         // m: lo más que entra un vértice de los dedos o la palma en una pieza del arma
            public float TorsoPenetration;          // m: lo más que entra el arma (sin la culata) en el torso
            public float StockPenetration;          // m: lo que entra la culata en el torso (apoyada en el hombro es normal 1-2 cm)
            public float ElbowFlexRight, ElbowFlexLeft;
            public float ElbowDownRight, ElbowDownLeft;   // +1 el codo apunta abajo, -1 arriba (solo con la mano en el arma)
            public float RightWeight, LeftWeight;
            public string FingerCulprit;            // qué pieza de la mano entra en qué pieza del arma (depuración)
            public float LeftReach, RightReach;     // distancia hombro-muñeca objetivo / largo del brazo (> 1: no llega)
        }

        private static readonly List<MeshFilter> Filters = new List<MeshFilter>();
        private static readonly Dictionary<Mesh, Vector3[]> Verts = new Dictionary<Mesh, Vector3[]>();

        public static Reading Measure(WeaponScenario sc) => Measure(sc.Rig, sc.Parts, sc.Data, sc.Rig.Inputs);

        public static Reading Measure(ProceduralRig rig, GunParts parts, WeaponData data, RigInputs inputs)
        {
            var r = new Reading { RightWeight = inputs.RightWeight, LeftWeight = inputs.LeftWeight };
            r.RightError = inputs.RightWeight > 0.95f && rig.RightHandMesh != null ? Vector3.Distance(rig.RightHandMesh.GripCenter, inputs.RightHandGrip) : -1f;
            r.LeftError = inputs.LeftWeight > 0.95f && rig.LeftHandMesh != null ? Vector3.Distance(rig.LeftHandMesh.GripCenter, inputs.LeftHandGrip) : -1f;

            // piezas del arma (cajas) y del torso
            var gun = new List<Transform>();
            if (parts != null) foreach (var mf in parts.GetComponentsInChildren<MeshFilter>()) gun.Add(mf.transform);
            foreach (var hand in new[] { rig.RightHandMesh, rig.LeftHandMesh })
            {
                if (hand == null) continue;
                Filters.Clear(); hand.GetComponentsInChildren(Filters);
                foreach (var mf in Filters)
                    foreach (var v in V(mf.sharedMesh))
                    {
                        Vector3 p = mf.transform.TransformPoint(v);
                        foreach (var g in gun) { float dd = Depth(g, p); if (dd > r.FingerPenetration) { r.FingerPenetration = dd; r.FingerCulprit = mf.transform.parent.name + "/" + mf.name + (hand.IsRight ? "(der)" : "(izq)") + ">" + g.name; } }
                    }
            }
            var torso = new List<Transform>();
            foreach (var b in new[] { BoneId.Hips, BoneId.Spine, BoneId.Chest, BoneId.UpperChest })
            {
                var bone = rig.Bone(b);
                for (int c = 0; c < bone.childCount; c++) { var t = bone.GetChild(c); if (t.GetComponent<MeshFilter>() != null) torso.Add(t); }
            }
            foreach (var g in gun)
            {
                bool stock = g.name == "Stock";
                foreach (var v in V(g.GetComponent<MeshFilter>().sharedMesh))
                {
                    Vector3 p = g.TransformPoint(v);
                    foreach (var t in torso)
                    {
                        float d = Depth(t, p);
                        if (stock) r.StockPenetration = Mathf.Max(r.StockPenetration, d); else r.TorsoPenetration = Mathf.Max(r.TorsoPenetration, d);
                    }
                }
            }
            float arm = rig.Spec[(int)BoneId.LeftUpperArm].Length + rig.Spec[(int)BoneId.LeftLowerArm].Length;
            r.LeftReach = Vector3.Distance(rig.Bone(BoneId.LeftUpperArm).position, inputs.LeftHand) / arm;
            r.RightReach = Vector3.Distance(rig.Bone(BoneId.RightUpperArm).position, inputs.RightHand) / arm;
            r.ElbowFlexRight = rig.ElbowFlexion[1]; r.ElbowFlexLeft = rig.ElbowFlexion[0];
            r.ElbowDownRight = ElbowDown(rig, false); r.ElbowDownLeft = ElbowDown(rig, true);
            return r;
        }

        /// <summary>Cuánto está un punto dentro de la caja de una pieza (m, 0 si está fuera). Las piezas son cubos escalados: su malla ocupa [-0.5, 0.5].</summary>
        public static float Depth(Transform box, Vector3 world)
        {
            var mf = box.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return 0f;
            Bounds b = mf.sharedMesh.bounds;
            Vector3 l = box.InverseTransformPoint(world) - b.center;
            Vector3 s = box.lossyScale;
            float dx = (b.extents.x - Mathf.Abs(l.x)) * Mathf.Abs(s.x), dy = (b.extents.y - Mathf.Abs(l.y)) * Mathf.Abs(s.y), dz = (b.extents.z - Mathf.Abs(l.z)) * Mathf.Abs(s.z);
            if (dx <= 0f || dy <= 0f || dz <= 0f) return 0f;
            return Mathf.Min(dx, Mathf.Min(dy, dz));
        }

        /// <summary>+1 si el codo queda por debajo de la línea hombro-muñeca (apunta abajo), -1 si queda por encima.</summary>
        public static float ElbowDown(ProceduralRig rig, bool left)
        {
            Vector3 s = rig.Bone(left ? BoneId.LeftUpperArm : BoneId.RightUpperArm).position, e = rig.Bone(left ? BoneId.LeftLowerArm : BoneId.RightLowerArm).position;
            Vector3 w = rig.Bone(left ? BoneId.LeftHand : BoneId.RightHand).position;
            Vector3 off = Vector3.ProjectOnPlane(e - s, (w - s).normalized);
            return off.magnitude < 0.01f ? 0f : -Vector3.Dot(off.normalized, Vector3.up);
        }

        /// <summary>Piezas de mochila (no debe haber ninguna).</summary>
        public static int BackpackParts(ProceduralRig rig)
        {
            int n = 0;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) if (t.name.ToLowerInvariant().Contains("mochila") || t.name.ToLowerInvariant().Contains("backpack")) n++;
            return n;
        }

        private static Vector3[] V(Mesh m)
        {
            if (!Verts.TryGetValue(m, out var v) || v == null) { v = m.vertices; Verts[m] = v; }
            return v;
        }

        public static string Line(WeaponScenario sc)
        {
            var r = Measure(sc);
            return sc.Data.id + " " + sc.Step + ": mano der " + Cm(r.RightError) + " izq " + Cm(r.LeftError) + "  dedos dentro " + (r.FingerPenetration * 100f).ToString("0.0") +
                   " cm  arma en torso " + (r.TorsoPenetration * 100f).ToString("0.0") + " cm (culata " + (r.StockPenetration * 100f).ToString("0.0") + ")  codos " +
                   r.ElbowFlexRight.ToString("0") + "°/" + r.ElbowFlexLeft.ToString("0") + "° abajo " + r.ElbowDownRight.ToString("0.0") + "/" + r.ElbowDownLeft.ToString("0.0") + "  alcance " + r.RightReach.ToString("0.00") + "/" + r.LeftReach.ToString("0.00") + "  [" + r.FingerCulprit + "]";
        }

        private static string Cm(float m) => m < 0f ? "-" : (m * 100f).ToString("0.0") + " cm";
    }
}

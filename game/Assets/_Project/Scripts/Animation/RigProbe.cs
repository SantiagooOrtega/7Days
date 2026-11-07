using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Mide lo que SE VE de cada pierna (las mallas) y lo compara con lo que calculó el IK y con los huesos. Las pruebas matemáticas comprueban el IK; esto
    /// comprueba que los huesos y las mallas lo siguen de verdad. Lo usan el panel F6 ("Esqueleto vs malla"), el ciclo F7 y las pruebas visuales.
    /// </summary>
    public static class RigProbe
    {
        public struct LegReading
        {
            public float IkKnee, BoneKnee, MeshKnee;   // flexión de la rodilla (0 = recta) según el IK, según los huesos y según las mallas
            public float GroundY, AnkleBoneY, AnkleIkY;
            public float LowestMeshY;                  // vértice más bajo de todas las piezas de la pierna (exacto)
            public float LowestBoundsY;                // Renderer.bounds.min.y más bajo (caja alineada: puede quedar algo por debajo del vértice real)
            public float KneeForward;                  // hacia dónde apunta la rodilla visible: +1 adelante, -1 atrás
        }

        private static readonly Dictionary<Mesh, Vector3[]> Vertices = new Dictionary<Mesh, Vector3[]>();
        private static readonly List<MeshFilter> Filters = new List<MeshFilter>();
        private static readonly List<Renderer> Renderers = new List<Renderer>();

        public static LegReading Leg(ProceduralRig rig, int f)
        {
            bool left = f == 0;
            Transform hip = rig.Bone(left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg), knee = rig.Bone(left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg);
            Transform ankle = rig.Bone(left ? BoneId.LeftFoot : BoneId.RightFoot);
            var r = new LegReading
            {
                IkKnee = rig.KneeFlexion[f],
                BoneKnee = Vector3.Angle(knee.position - hip.position, ankle.position - knee.position),
                GroundY = rig.FootGround[f],
                AnkleBoneY = ankle.position.y,
                AnkleIkY = rig.IkAnkles[f].y,
                LowestMeshY = LowestVertexY(hip),
                LowestBoundsY = LowestBoundsY(hip),
            };
            Transform thighMesh = Limb(hip), shinMesh = Limb(knee);
            if (thighMesh != null && shinMesh != null)
                r.MeshKnee = Vector3.Angle(Centroid(thighMesh) - hip.position, Centroid(shinMesh) - knee.position);
            // rodilla visible respecto a la línea cadera-tobillo
            Vector3 line = ankle.position - hip.position;
            Vector3 off = Vector3.ProjectOnPlane(knee.position - hip.position, line.normalized);
            r.KneeForward = off.magnitude < 0.005f ? 1f : Vector3.Dot(off.normalized, Vector3.ProjectOnPlane(rig.transform.forward, line.normalized).normalized);
            return r;
        }

        /// <summary>Pieza principal de un hueso (muslo, pantorrilla...): se llama "&lt;hueso&gt; limb".</summary>
        public static Transform Limb(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++)
                if (bone.GetChild(i).name.EndsWith(" limb")) return bone.GetChild(i);
            return null;
        }

        /// <summary>Centro de los vértices de una malla en el mundo (está sobre el eje de la pieza: sirve para medir hacia dónde apunta lo que se ve).</summary>
        public static Vector3 Centroid(Transform meshTransform)
        {
            var mf = meshTransform.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return meshTransform.position;
            var v = Verts(mf.sharedMesh);
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < v.Length; i++) sum += v[i];
            return meshTransform.TransformPoint(sum / Mathf.Max(1, v.Length));
        }

        /// <summary>Altura del vértice más bajo de todas las mallas bajo "root" (exacta: transforma cada vértice).</summary>
        public static float LowestVertexY(Transform root)
        {
            float min = float.MaxValue;
            Filters.Clear();
            root.GetComponentsInChildren(Filters);
            foreach (var mf in Filters)
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.transform.localToWorldMatrix;
                var v = Verts(mf.sharedMesh);
                for (int i = 0; i < v.Length; i++) { float y = m.MultiplyPoint3x4(v[i]).y; if (y < min) min = y; }
            }
            return min;
        }

        public static float LowestBoundsY(Transform root)
        {
            float min = float.MaxValue;
            Renderers.Clear();
            root.GetComponentsInChildren(Renderers);
            foreach (var r in Renderers) min = Mathf.Min(min, r.bounds.min.y);
            return min;
        }

        private static Vector3[] Verts(Mesh mesh)
        {
            if (!Vertices.TryGetValue(mesh, out var v) || v == null) { v = mesh.vertices; Vertices[mesh] = v; }
            return v;
        }

        /// <summary>Una línea por frame para el log: pie (hueso, IK y malla) frente al suelo y rodilla (IK, hueso y malla) de cada pierna.</summary>
        public static string LogLine(ProceduralRig rig)
        {
            var sb = new System.Text.StringBuilder(220);
            sb.Append("[Piernas] f").Append(Time.frameCount).Append(" v=").Append(rig.MeasuredSpeed.ToString("0.0"));
            for (int f = 0; f < 2; f++)
            {
                var r = Leg(rig, f);
                sb.Append(f == 0 ? " | IZQ" : " | DER")
                  .Append(" suelo ").Append(r.GroundY.ToString("0.000"))
                  .Append(" tobillo hueso ").Append(r.AnkleBoneY.ToString("0.000")).Append(" IK ").Append(r.AnkleIkY.ToString("0.000"))
                  .Append(" malla min ").Append(r.LowestMeshY.ToString("0.000")).Append(r.LowestMeshY < r.GroundY - 0.002f ? " BAJO EL SUELO" : "")
                  .Append(" | rodilla IK ").Append(r.IkKnee.ToString("0")).Append("° hueso ").Append(r.BoneKnee.ToString("0")).Append("° malla ").Append(r.MeshKnee.ToString("0")).Append('°');
            }
            return sb.ToString();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SevenDays.Core
{
    /// <summary>
    /// Acumula muchos quads (calcomanías) y hojas de hierba en UNA sola malla: cientos de piezas cuestan una llamada
    /// de dibujo. Sin colliders ni sombras. Límite de 65 000 vértices por malla.
    /// </summary>
    public class MeshBatch
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<int> _t = new List<int>();

        public int VertexCount => _v.Count;
        public bool CanAdd(int vertices) => _v.Count + vertices <= 65000;

        /// <summary>Quad horizontal (mira hacia arriba) de ancho x largo metros, girado "yaw" grados. UV 0..1.</summary>
        public void AddFlatQuad(Vector3 center, float yawDeg, float width, float length)
        {
            if (!CanAdd(4)) return;
            var rot = Quaternion.Euler(0f, yawDeg, 0f);
            int s = _v.Count;
            _v.Add(center + rot * new Vector3(-width / 2f, 0f, -length / 2f));
            _v.Add(center + rot * new Vector3(width / 2f, 0f, -length / 2f));
            _v.Add(center + rot * new Vector3(width / 2f, 0f, length / 2f));
            _v.Add(center + rot * new Vector3(-width / 2f, 0f, length / 2f));
            _uv.Add(new Vector2(0f, 0f)); _uv.Add(new Vector2(1f, 0f)); _uv.Add(new Vector2(1f, 1f)); _uv.Add(new Vector2(0f, 1f));
            for (int i = 0; i < 4; i++) _n.Add(Vector3.up);
            // visto desde arriba, en sentido horario
            _t.Add(s); _t.Add(s + 3); _t.Add(s + 2);
            _t.Add(s); _t.Add(s + 2); _t.Add(s + 1);
        }

        /// <summary>
        /// Hoja de hierba/maleza: UN triángulo afilado que se inclina "lean" metros hacia "yaw". Se dibuja con un material de doble
        /// cara (Cull Off) y la normal hacia arriba, así que las dos caras se iluminan igual sin duplicar triángulos.
        /// </summary>
        public void AddBlade(Vector3 basePos, float yawDeg, float height, float width, float lean)
        {
            if (!CanAdd(3)) return;
            var rot = Quaternion.Euler(0f, yawDeg, 0f);
            int s = _v.Count;
            _v.Add(basePos + rot * new Vector3(-width / 2f, 0f, 0f));
            _v.Add(basePos + rot * new Vector3(width / 2f, 0f, 0f));
            _v.Add(basePos + rot * new Vector3(0f, height, lean));
            for (int i = 0; i < 3; i++) _n.Add(Vector3.up);
            _uv.Add(new Vector2(0, 0)); _uv.Add(new Vector2(1, 0)); _uv.Add(new Vector2(0.5f, 1));
            _t.Add(s); _t.Add(s + 2); _t.Add(s + 1);
        }

        public GameObject Spawn(string name, Transform parent, Material material)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetUVs(0, _uv);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}

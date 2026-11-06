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

        /// <summary>Hoja de hierba/maleza: triángulo afilado de doble cara que se inclina "lean" metros hacia "yaw".</summary>
        public void AddBlade(Vector3 basePos, float yawDeg, float height, float width, float lean)
        {
            if (!CanAdd(6)) return;
            var rot = Quaternion.Euler(0f, yawDeg, 0f);
            Vector3 l = basePos + rot * new Vector3(-width / 2f, 0f, 0f);
            Vector3 r = basePos + rot * new Vector3(width / 2f, 0f, 0f);
            Vector3 tip = basePos + rot * new Vector3(0f, height, lean);
            int s = _v.Count;
            // cara delantera y trasera (así se ve desde cualquier lado, sin shader de doble cara)
            _v.Add(l); _v.Add(r); _v.Add(tip);
            _v.Add(l); _v.Add(r); _v.Add(tip);
            for (int i = 0; i < 6; i++) _n.Add(Vector3.up); // normales hacia arriba: iluminación suave de vegetación
            _uv.Add(new Vector2(0, 0)); _uv.Add(new Vector2(1, 0)); _uv.Add(new Vector2(0.5f, 1));
            _uv.Add(new Vector2(0, 0)); _uv.Add(new Vector2(1, 0)); _uv.Add(new Vector2(0.5f, 1));
            _t.Add(s); _t.Add(s + 2); _t.Add(s + 1);
            _t.Add(s + 3); _t.Add(s + 4); _t.Add(s + 5);
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

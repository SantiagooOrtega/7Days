using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.Core
{
    /// <summary>
    /// Cajas con UV "por cara" en metros: cada cara recibe la textura a escala real (tileMeters por repetición),
    /// así un muro de 12 m no estira la textura como lo hace el cubo por defecto de Unity (que mapea 0..1 por cara).
    /// Incluye tangentes para que funcionen los mapas de normales.
    /// </summary>
    public static class BoxMesh
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static Mesh Get(Vector3 size, float tileMeters, Vector2 uvOffset)
        {
            string key = size.x.ToString("0.###") + "|" + size.y.ToString("0.###") + "|" + size.z.ToString("0.###") + "|"
                + tileMeters.ToString("0.###") + "|" + uvOffset.x.ToString("0.###") + "|" + uvOffset.y.ToString("0.###");
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3>(24);
            var norms = new List<Vector3>(24);
            var tangs = new List<Vector4>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);

            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            // origen, vector u (ancho), vector v (alto): cross(u,v) = normal exterior
            AddFace(new Vector3(-hx, -hy, hz), new Vector3(2 * hx, 0, 0), new Vector3(0, 2 * hy, 0), tileMeters, uvOffset, verts, norms, tangs, uvs, tris);   // +Z
            AddFace(new Vector3(hx, -hy, -hz), new Vector3(-2 * hx, 0, 0), new Vector3(0, 2 * hy, 0), tileMeters, uvOffset, verts, norms, tangs, uvs, tris);  // -Z
            AddFace(new Vector3(hx, -hy, hz), new Vector3(0, 0, -2 * hz), new Vector3(0, 2 * hy, 0), tileMeters, uvOffset, verts, norms, tangs, uvs, tris);  // +X
            AddFace(new Vector3(-hx, -hy, -hz), new Vector3(0, 0, 2 * hz), new Vector3(0, 2 * hy, 0), tileMeters, uvOffset, verts, norms, tangs, uvs, tris);  // -X
            AddFace(new Vector3(-hx, hy, hz), new Vector3(2 * hx, 0, 0), new Vector3(0, 0, -2 * hz), tileMeters, uvOffset, verts, norms, tangs, uvs, tris);   // +Y
            AddFace(new Vector3(-hx, -hy, -hz), new Vector3(2 * hx, 0, 0), new Vector3(0, 0, 2 * hz), tileMeters, uvOffset, verts, norms, tangs, uvs, tris);  // -Y

            var mesh = new Mesh { name = "Box " + key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTangents(tangs);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        private static void AddFace(Vector3 origin, Vector3 u, Vector3 v, float tile, Vector2 offset,
            List<Vector3> verts, List<Vector3> norms, List<Vector4> tangs, List<Vector2> uvs, List<int> tris)
        {
            int start = verts.Count;
            Vector3 normal = Vector3.Cross(u, v).normalized;
            Vector3 tangent = u.normalized;
            float w = u.magnitude / tile, h = v.magnitude / tile;

            verts.Add(origin);
            verts.Add(origin + u);
            verts.Add(origin + u + v);
            verts.Add(origin + v);
            uvs.Add(offset);
            uvs.Add(offset + new Vector2(w, 0f));
            uvs.Add(offset + new Vector2(w, h));
            uvs.Add(offset + new Vector2(0f, h));
            for (int i = 0; i < 4; i++)
            {
                norms.Add(normal);
                tangs.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
            }
            tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();
    }
}

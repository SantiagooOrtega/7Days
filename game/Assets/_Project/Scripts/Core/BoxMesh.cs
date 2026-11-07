using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Core
{
    /// <summary>
    /// Convierte la geometría de caja (SevenDays.Procedural.BoxGeometry) en una Mesh de Unity.
    /// La geometría trae bisel, UV por cara en metros y tangentes definidas (para que el mapa de normales
    /// se vea bien desde cualquier ángulo), y se cachea por tamaño/UV/bisel.
    /// </summary>
    public static class BoxMesh
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static Mesh Get(Vector3 size, float tileMeters, Vector2 uvOffset, float bevel)
        {
            string key = string.Join("|", size.x.ToString("0.###"), size.y.ToString("0.###"), size.z.ToString("0.###"),
                tileMeters.ToString("0.###"), uvOffset.x.ToString("0.###"), uvOffset.y.ToString("0.###"), bevel.ToString("0.###"));
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var data = BoxGeometry.Build(new V3(size.x, size.y, size.z), tileMeters, new V2(uvOffset.x, uvOffset.y), bevel);
            var mesh = ToMesh(data);
            mesh.name = "Box " + key;
            Cache[key] = mesh;
            return mesh;
        }

        public static Mesh ToMesh(MeshData d)
        {
            int n = d.Positions.Count;
            var verts = new Vector3[n];
            var norms = new Vector3[n];
            var tangs = new Vector4[n];
            var uvs = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var p = d.Positions[i]; var nr = d.Normals[i]; var t = d.Tangents[i]; var uv = d.Uvs[i];
                verts[i] = new Vector3(p.X, p.Y, p.Z);
                norms[i] = new Vector3(nr.X, nr.Y, nr.Z);
                tangs[i] = new Vector4(t.X, t.Y, t.Z, d.WOf(i)); // -1 con u a la derecha y v arriba (convención de Unity)
                uvs[i] = new Vector2(uv.X, uv.Y);
            }
            var mesh = new Mesh();
            if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.tangents = tangs;
            mesh.uv = uvs;
            if (d.SubmeshSplit >= 0 && d.SubmeshSplit < d.Triangles.Count)
            {
                // dos submeshes: frente (0) y dorso/cantos (1)
                mesh.subMeshCount = 2;
                mesh.SetTriangles(d.Triangles.GetRange(0, d.SubmeshSplit), 0);
                mesh.SetTriangles(d.Triangles.GetRange(d.SubmeshSplit, d.Triangles.Count - d.SubmeshSplit), 1);
            }
            else
            {
                mesh.triangles = d.Triangles.ToArray();
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();
    }
}

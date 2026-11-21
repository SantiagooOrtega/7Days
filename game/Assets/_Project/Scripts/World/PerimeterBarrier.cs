using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Dibuja la barrera del perímetro (PerimeterLayout) con GPU instancing: una malla y un material compartidos por tipo de pieza y Graphics.RenderMeshInstanced
    /// por tramos de 8 segmentos (matrices preparadas una vez: sin memoria por frame). LOD por distancia: el alambre, los banderines y los caballos de frisa solo
    /// se dibujan a menos de 45 m del tramo; los sacos y el concreto siempre (la niebla los funde). Sin sombras en el alambre. Los muros lisos del límite se quedan
    /// como collider invisible (bloquean jugador, zombis y balas y no se saltan). Focos con luz tenue (pocos, sin sombras) y algún barril como objetos sueltos.
    /// </summary>
    public class PerimeterBarrier : MonoBehaviour
    {
        public const float WireLodDistance = 45f;
        public const int SegmentsPerChunk = 8;

        private class Batch { public Mesh Mesh; public Material Mat; public bool Lod; public ShadowCastingMode Shadows; public readonly List<Matrix4x4> List = new List<Matrix4x4>(); public Matrix4x4[] Data; public int[] ChunkStart, ChunkCount; }
        private readonly Dictionary<PerimeterPiece, Batch> _batches = new Dictionary<PerimeterPiece, Batch>();
        private readonly List<Batch> _order = new List<Batch>();
        private Vector3[] _chunkCenter;
        private int _chunks;

        public int Parts { get; private set; }
        public int DrawCalls { get; private set; }
        public int Instances { get; private set; }
        public float LastMs { get; private set; }
        public static PerimeterBarrier Instance { get; private set; }

        public static PerimeterBarrier Build(Transform root, MapData map)
        {
            string style = map.ambience != null ? map.ambience.perimeter : "";
            if (string.IsNullOrEmpty(style)) return null;
            HideWalls(root, map);
            // auditoría: nada que no sea la valla a menos de 3 m del perímetro, y ninguna pieza de la valla flotando
            var intruders = PerimeterLayout.Intruders(map);
            foreach (var s in intruders) Debug.LogWarning("[7Days] Perímetro: prop a menos de " + PerimeterLayout.AuditDistance + " m del límite que no es la valla: " + s);
            Debug.Log("[7Days] Auditoría del perímetro (" + map.id + "): " + intruders.Count + " props ajenos a menos de " + PerimeterLayout.AuditDistance + " m.");
            var go = new GameObject("Perimeter " + style);
            go.transform.SetParent(root, false);
            var b = go.AddComponent<PerimeterBarrier>();
            b.Make(map, style);
            Instance = b;
            return b;
        }

        /// <summary>Los muros / límites del borde siguen como collider, sin dibujarse.</summary>
        private static void HideWalls(Transform root, MapData map)
        {
            foreach (var p in map.props)
            {
                if ((p.kind != "wall" && p.kind != "boundary") || Mathf.Max(Mathf.Abs(p.pos[0]), Mathf.Abs(p.pos[2])) < map.halfSize - 0.5f) continue;
                var t = root.Find(p.name);
                if (t == null) foreach (Transform c in root) if (c.name == p.name) { t = c; break; }
                if (t != null) foreach (var r in t.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
        }

        private Batch B(PerimeterPiece k, Mesh mesh, Material mat, bool lod, ShadowCastingMode sh = ShadowCastingMode.Off)
        {
            if (_batches.TryGetValue(k, out var b)) return b;
            b = new Batch { Mesh = mesh, Mat = mat, Lod = lod, Shadows = sh };
            if (!mat.enableInstancing) mat.enableInstancing = true;
            _batches[k] = b; _order.Add(b);
            return b;
        }

        private void Make(MapData map, string style)
        {
            var parts = PerimeterLayout.Build(map.halfSize, style, map.seed + 101);
            Parts = parts.Count;
            var box = BoxMesh.Get(Vector3.one, 1f, Vector2.zero, 0.12f);          // sacos y concreto: bisel redondeado
            var flat = BoxMesh.Get(Vector3.one, 1f, Vector2.zero, 0f);            // piezas finas: sin bisel (escalado no uniforme deformaba el bisel en picos oscuros)
            var cyl = PrimitiveMesh(PrimitiveType.Cylinder);
            float span = map.halfSize * 2f + PerimeterLayout.Depth * 2f;
            int segs = Mathf.CeilToInt(span / PerimeterLayout.SegmentLength);
            var wire = WireMesh(span / segs, PerimeterLayout.WireRadius);
            var bagMat = Prim.GetTextured(new Color(0.55f, 0.48f, 0.34f), SurfaceLibrary.Cardboard, Vector2.one, 0.05f, 0f, true, 0.85f);
            var woodMat = Prim.GetTextured(new Color(0.34f, 0.25f, 0.17f), SurfaceLibrary.Bark, Vector2.one, 0.1f, 0f, true);
            var concreteMat = Prim.GetTextured(new Color(0.42f, 0.42f, 0.41f), SurfaceLibrary.Concrete, Vector2.one, 0.1f, 0f, true);
            var metalMat = Prim.GetMaterial(new Color(0.22f, 0.23f, 0.24f), true);
            var wireMat = Prim.GetMaterial(new Color(0.12f, 0.12f, 0.12f), true);
            var clothMat = Prim.GetMaterial(new Color(0.45f, 0.12f, 0.1f), true);
            var logMat = Prim.GetTextured(new Color(0.38f, 0.29f, 0.2f), SurfaceLibrary.Bark, Vector2.one, 0.08f, 0f, true);

            // tramos para el LOD: SegmentsPerChunk segmentos seguidos de un mismo lado
            _chunks = 4 * Mathf.CeilToInt(segs / (float)SegmentsPerChunk);
            _chunkCenter = new Vector3[_chunks];
            var perChunk = new List<(int chunk, PerimeterPart p)>();
            int lamps = 0;
            foreach (var p in parts)
            {
                float along = p.Side < 2 ? p.Center.X : p.Center.Z;
                int idx = Mathf.Clamp(Mathf.FloorToInt((along + span / 2f) / PerimeterLayout.SegmentLength / SegmentsPerChunk), 0, _chunks / 4 - 1);
                int chunk = p.Side * (_chunks / 4) + idx;
                if (p.Kind == PerimeterPiece.Lamp) { Lamp(p, lamps++ % 2 == 0 && lamps <= 12); continue; }
                if (p.Kind == PerimeterPiece.Barrel) { Barrel(p); continue; }
                perChunk.Add((chunk, p));
            }
            perChunk.Sort((a, b) => a.chunk.CompareTo(b.chunk));
            var counts = new int[_chunks];
            foreach (var (chunk, p) in perChunk)
            {
                counts[chunk]++;
                _chunkCenter[chunk] += new Vector3(p.Center.X, 0f, p.Center.Z);
                var pos = new Vector3(p.Center.X, p.Center.Y, p.Center.Z);
                var rot = Quaternion.Euler(0f, p.Yaw, 0f);
                // la inclinación es "hacia fuera": el lado norte/este se inclina hacia +, el sur/oeste hacia -
                float tiltSign = p.Side == 0 || p.Side == 2 ? 1f : -1f;
                var tilt = Quaternion.Euler(p.Tilt * tiltSign * (p.Side < 2 ? 1f : 0f), 0f, p.Side < 2 ? 0f : -p.Tilt * tiltSign);
                var size = new Vector3(p.Size.X, p.Size.Y, p.Size.Z);
                switch (p.Kind)
                {
                    case PerimeterPiece.Sandbag: Add(B(p.Kind, box, bagMat, false), pos, rot, size, chunk); break;
                    case PerimeterPiece.Plank: Add(B(p.Kind, flat, woodMat, false), pos, tilt * rot, size, chunk); break;
                    case PerimeterPiece.Concrete: Add(B(p.Kind, box, concreteMat, false, ShadowCastingMode.On), pos, tilt * rot, size, chunk); break;
                    case PerimeterPiece.FencePanel: Add(B(p.Kind, flat, metalMat, true), pos, tilt * rot, size, chunk); break;
                    case PerimeterPiece.Post: Add(B(p.Kind, flat, woodMat, false), pos, tilt * rot, size, chunk); break;
                    case PerimeterPiece.Log: Add(B(p.Kind, cyl, logMat, false), pos, rot * Quaternion.Euler(0f, 0f, 90f), new Vector3(size.y, size.x / 2f, size.z), chunk); break;
                    case PerimeterPiece.Wire: Add(B(p.Kind, wire, wireMat, true), pos, rot, Vector3.one, chunk); break;
                    case PerimeterPiece.Banner: Add(B(p.Kind, flat, clothMat, true), pos, tilt * rot, size, chunk); break;
                    case PerimeterPiece.Cheval:
                    {
                        // caballo de frisa: un travesaño y dos aspas cruzadas
                        var bb = B(p.Kind, flat, woodMat, true);
                        Add(bb, pos, rot, new Vector3(size.x, 0.1f, 0.1f), chunk);
                        foreach (float x in new[] { -0.5f, 0.5f })
                            foreach (float a in new[] { 45f, -45f })
                                Add(bb, pos + rot * new Vector3(x * size.x * 0.8f, 0f, 0f), rot * Quaternion.Euler(a, 0f, 0f), new Vector3(0.08f, size.y * 1.3f, 0.08f), chunk);
                        break;
                    }
                }
            }
            for (int c = 0; c < _chunks; c++) if (counts[c] > 0) _chunkCenter[c] /= counts[c];
            // matrices finales por lote, con el rango de cada tramo
            foreach (var b in _order)
            {
                b.Data = new Matrix4x4[b.List.Count];
                b.ChunkStart = new int[_chunks]; b.ChunkCount = new int[_chunks];
                var tmp = new List<(int c, Matrix4x4 m)>(b.List.Count);
                for (int i = 0; i < b.List.Count; i++) tmp.Add((_tmpChunk[b][i], b.List[i]));
                tmp.Sort((x, y) => x.c.CompareTo(y.c));
                for (int i = 0; i < tmp.Count; i++) { b.Data[i] = tmp[i].m; if (b.ChunkCount[tmp[i].c]++ == 0) b.ChunkStart[tmp[i].c] = i; }
                Instances += b.Data.Length;
            }
            _tmpChunk.Clear();
            AtmosphereBudget.RegisterGroup(gameObject, 4);
        }

        private readonly Dictionary<Batch, List<int>> _tmpChunk = new Dictionary<Batch, List<int>>();
        private void Add(Batch b, Vector3 pos, Quaternion rot, Vector3 scale, int chunk)
        {
            b.List.Add(Matrix4x4.TRS(pos, rot, scale));
            if (!_tmpChunk.TryGetValue(b, out var l)) _tmpChunk[b] = l = new List<int>();
            l.Add(chunk);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float t0 = Time.realtimeSinceStartup;
            Vector3 cp = cam.transform.position; cp.y = 0f;
            int calls = 0;
            foreach (var b in _order)
            {
                var rp = new RenderParams(b.Mat) { shadowCastingMode = b.Shadows, receiveShadows = !b.Lod, layer = 0 };
                if (!b.Lod)
                {
                    // sin LOD: todo el lote de una vez (el instancing parte en grupos de 1023 solo)
                    if (b.Data.Length > 0) { Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.Data, b.Data.Length, 0); calls++; }
                    continue;
                }
                for (int c = 0; c < _chunks; c++)
                {
                    int n = b.ChunkCount[c]; if (n == 0) continue;
                    if ((_chunkCenter[c] - cp).sqrMagnitude > WireLodDistance * WireLodDistance * 1.6f) continue;   // tramo lejano: sin alambre
                    Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.Data, n, b.ChunkStart[c]); calls++;
                }
            }
            DrawCalls = calls;
            LastMs = Mathf.Lerp(LastMs, (Time.realtimeSinceStartup - t0) * 1000f, 0.1f);
        }

        public static string Describe() => Instance == null ? "" : "Perímetro: " + Instance.Parts + " piezas, " + Instance.Instances + " instancias, " + Instance.DrawCalls + " lotes dibujados, " + Instance.LastMs.ToString("0.00") + " ms CPU";

        private void Lamp(PerimeterPart p, bool light)
        {
            var pos = new Vector3(p.Center.X, p.Center.Y, p.Center.Z);
            var body = Prim.Create(PrimitiveType.Cube, "Foco", pos, new Vector3(p.Size.X, p.Size.Y, p.Size.Z), Prim.GetMaterial(new Color(0.15f, 0.15f, 0.16f)), transform, false);
            body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lens = Prim.Create(PrimitiveType.Cube, "Lente", pos + Vector3.down * 0.05f, new Vector3(p.Size.X * 0.8f, 0.04f, p.Size.Z * 0.8f), Prim.GetEmissive(new Color(1f, 0.82f, 0.55f), 2.2f), transform, false);
            lens.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (!light) return;
            var l = new GameObject("Luz foco").AddComponent<Light>();
            l.transform.SetParent(transform, false); l.transform.position = pos + Vector3.down * 0.3f;
            l.type = LightType.Point; l.range = 6f; l.intensity = 0.7f; l.color = new Color(1f, 0.8f, 0.55f); l.shadows = LightShadows.None;
        }

        private void Barrel(PerimeterPart p)
        {
            var go = Prim.Create(PrimitiveType.Cylinder, "Barril", new Vector3(p.Center.X, p.Center.Y, p.Center.Z), new Vector3(p.Size.X, p.Size.Y / 2f, p.Size.Z), Prim.GetMaterial(new Color(0.3f, 0.2f, 0.12f)), transform, false);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var g = GameObject.CreatePrimitive(t); var m = g.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(g); return m;
        }

        /// <summary>Espiral de alambre de espino de "length" metros: dos cintas cruzadas finas por tramo (se ven desde cualquier lado) y púas cada 0.25 m.</summary>
        private static Mesh WireMesh(float length, float radius)
        {
            var pts = PerimeterLayout.Coil(length, radius);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            const float w = 0.012f;
            void Ribbon(Vector3 a, Vector3 b, Vector3 side)
            {
                int i = v.Count;
                v.Add(a - side * w); v.Add(a + side * w); v.Add(b + side * w); v.Add(b - side * w);
                var nn = Vector3.Cross(b - a, side).normalized; for (int k = 0; k < 4; k++) n.Add(nn);
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
            }
            for (int i = 1; i < pts.Count; i++)
            {
                var a = new Vector3(pts[i - 1].X, pts[i - 1].Y, pts[i - 1].Z); var b = new Vector3(pts[i].X, pts[i].Y, pts[i].Z);
                var dir = (b - a).normalized; var radial = new Vector3(0f, a.y, a.z).normalized;
                Ribbon(a, b, radial); Ribbon(a, b, Vector3.Cross(dir, radial).normalized);
                if (i % 4 == 0) Ribbon(a - radial * 0.035f, a + radial * 0.035f, dir);   // púa
            }
            // dos hilos rectos a lo largo (arriba y abajo de la espiral)
            foreach (float y in new[] { radius, -radius * 0.6f }) { var a = new Vector3(-length / 2f, y, 0f); var b = new Vector3(length / 2f, y, 0f); Ribbon(a, b, Vector3.up); Ribbon(a, b, Vector3.forward); }
            var m = new Mesh { name = "Alambre de espino" };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}

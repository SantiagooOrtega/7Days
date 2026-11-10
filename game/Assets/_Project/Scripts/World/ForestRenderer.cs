using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Árboles del bosque con GPU instancing y 3 niveles de detalle (LOD 0 completo, LOD 1 medio y LOD 2 una cruz de 4 caras), sin un GameObject por árbol.
    /// Cada frame reparte los árboles por distancia a la cámara: lo que queda detrás o más allá de la niebla no se dibuja, hay un tope de árboles visibles (si se
    /// pasa, la distancia de corte se encoge sola), y solo los cercanos del LOD 0 echan sombra. El gobernador de calidad (AtmosphereBudget.Level 0-3) recorta
    /// distancias, tope y sombras por niveles. Cifras para el panel F6 en <see cref="Stats"/>.
    /// </summary>
    public class ForestRenderer : MonoBehaviour
    {
        /// <summary>Distancias y topes por nivel de calidad (0 = máximo).</summary>
        public static readonly float[] CullByLevel = { 1f, 0.85f, 0.7f, 0.55f }, ShadowByLevel = { 22f, 15f, 9f, 0f };
        public static readonly int[] CapByLevel = { 420, 340, 260, 180 };
        public const float Lod1At = 16f, Lod2At = 30f;

        public struct ForestStats { public int Trees, Visible, Lod0, Lod1, Lod2, Shadowed; public float CullDistance, Ms; }
        public static ForestStats Stats;
        public static ForestRenderer Instance { get; private set; }

        private sealed class Kind
        {
            public Mesh[] Trunk = new Mesh[3], Crown = new Mesh[3];
            public readonly List<Matrix4x4> All = new List<Matrix4x4>();
            public readonly List<Vector3> Pos = new List<Vector3>();
            public Matrix4x4[][] Bins = new Matrix4x4[4][];   // LOD 0 con sombra, LOD 0, LOD 1, LOD 2
            public int[] Counts = new int[4];
        }

        private readonly List<Kind> _kinds = new List<Kind>();
        private Material _bark, _needles, _dry;
        private float _baseCull = 50f, _cull = 50f;

        /// <summary>Distancia máxima de dibujo: un poco más allá de donde la niebla ya lo tapa (niebla exponencial al cuadrado: ~1.9 / densidad).</summary>
        public static float CullForFog(float density) => density <= 0f ? 90f : Mathf.Clamp(2.1f / density, 25f, 90f);

        public static ForestRenderer Build(Transform parent, IList<PropEntry> trees, float fogDensity)
        {
            var go = new GameObject("Forest");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<ForestRenderer>();
            r._baseCull = r._cull = CullForFog(fogDensity);
            r._bark = Prim.GetTextured(new Color(0.42f, 0.36f, 0.3f), SurfaceLibrary.Bark, Vector2.one, 0.05f, 0f, false, 1f, true);
            r._needles = Prim.GetMaterial(new Color(0.16f, 0.24f, 0.17f), true);
            r._dry = Prim.GetTextured(new Color(0.36f, 0.31f, 0.26f), SurfaceLibrary.Bark, Vector2.one, 0.05f, 0f, false, 1f, true);
            var index = new Dictionary<string, Kind>();
            foreach (var (type, v) in ForestGeometry.TreeKinds())
            {
                var k = new Kind();
                for (int lod = 0; lod < 3; lod++)
                {
                    var t = ForestGeometry.Tree(type, v, lod);
                    k.Trunk[lod] = BoxMesh.ToMesh(t.Trunk); k.Trunk[lod].name = type + v + " tronco LOD" + lod;
                    k.Crown[lod] = BoxMesh.ToMesh(t.Crown); k.Crown[lod].name = type + v + " copa LOD" + lod;
                }
                index[type + ":" + v] = k;
                r._kinds.Add(k);
            }
            foreach (var e in trees)
            {
                if (!index.TryGetValue(e.type + ":" + e.variant, out var k)) continue;
                var p = new Vector3(e.x, 0f, e.z);
                k.All.Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, e.yaw, 0f), Vector3.one * e.scale));
                k.Pos.Add(p);
            }
            foreach (var k in r._kinds) for (int b = 0; b < 4; b++) k.Bins[b] = new Matrix4x4[Mathf.Max(1, k.All.Count)];
            Stats = default; Stats.Trees = trees.Count;
            Instance = r;
            return r;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int level = Mathf.Clamp(AtmosphereBudget.Level, 0, 3);
            float limit = _baseCull * CullByLevel[level], shadowAt = ShadowByLevel[level];
            int cap = CapByLevel[level];
            Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
            float cull2 = _cull * _cull, l1 = Lod1At * Lod1At, l2 = Lod2At * Lod2At, sh2 = shadowAt * shadowAt;
            int visible = 0, c0 = 0, c1 = 0, c2 = 0, cs = 0;
            foreach (var k in _kinds)
            {
                for (int b = 0; b < 4; b++) k.Counts[b] = 0;
                for (int i = 0; i < k.Pos.Count; i++)
                {
                    Vector3 d = k.Pos[i] - eye; d.y = 0f;
                    float dist2 = d.sqrMagnitude;
                    if (dist2 > cull2) continue;
                    // detrás de la cámara (con margen para la copa de los que están al lado) no se dibuja
                    if (dist2 > 36f && Vector3.Dot(d, fwd) < -6f) continue;
                    int bin = dist2 < l1 ? (dist2 < sh2 ? 0 : 1) : (dist2 < l2 ? 2 : 3);
                    k.Bins[bin][k.Counts[bin]++] = k.All[i];
                    visible++;
                }
                c0 += k.Counts[0] + k.Counts[1]; c1 += k.Counts[2]; c2 += k.Counts[3]; cs += k.Counts[0];
                Draw(k, 0, 0, true); Draw(k, 1, 0, false); Draw(k, 2, 1, false); Draw(k, 3, 2, false);
            }
            // tope de árboles visibles: si se pasa, la distancia de corte baja; si sobra margen, vuelve poco a poco hasta la de la niebla
            if (visible > cap) _cull = Mathf.Max(18f, _cull * 0.96f);
            else if (visible < cap * 0.85f) _cull = Mathf.Min(limit, _cull + 0.5f);
            if (_cull > limit) _cull = limit;
            Stats.Visible = visible; Stats.Lod0 = c0; Stats.Lod1 = c1; Stats.Lod2 = c2; Stats.Shadowed = cs; Stats.CullDistance = _cull;
            Stats.Ms = Mathf.Lerp(Stats.Ms, (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000f / System.Diagnostics.Stopwatch.Frequency, 0.1f);
        }

        private void Draw(Kind k, int bin, int lod, bool shadows)
        {
            int n = k.Counts[bin];
            if (n == 0) return;
            var crownMat = k.Trunk[0].name.StartsWith(PropTypes.Pine) ? _needles : _dry;
            var shadow = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var rpT = new RenderParams(_bark) { shadowCastingMode = shadow, receiveShadows = true, layer = gameObject.layer };
            var rpC = new RenderParams(crownMat) { shadowCastingMode = shadow, receiveShadows = true, layer = gameObject.layer };
            for (int start = 0; start < n; start += 1000)
            {
                int count = Mathf.Min(1000, n - start);
                Graphics.RenderMeshInstanced(rpT, k.Trunk[lod], 0, k.Bins[bin], count, start);
                Graphics.RenderMeshInstanced(rpC, k.Crown[lod], 0, k.Bins[bin], count, start);
            }
        }

        /// <summary>Resumen para el panel Render de F6.</summary>
        public static string Describe() => Instance == null ? "" : "Bosque: " + Stats.Visible + "/" + Stats.Trees + " árboles (LOD0 " + Stats.Lod0 + ", LOD1 " + Stats.Lod1 + ", cruz " + Stats.Lod2 + ", con sombra " + Stats.Shadowed + "), corte " + Stats.CullDistance.ToString("0") + " m, " + Stats.Ms.ToString("0.00") + " ms CPU";
    }
}

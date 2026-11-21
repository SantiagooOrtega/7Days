using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Luna y estrellas baratas: todo va a 105 m de la cámara y la sigue (como un cielo infinito), con un shader sin niebla (Sprites/Default) y la atenuación
    /// calculada aparte (NightSky.Visibility: niebla, horizonte y nubes). Luna con cráteres y halo donde está la luz principal (sus sombras cuadran),
    /// una luz direccional fría muy tenue sin sombras, estrellas en UNA malla con parpadeo por grupos, fugaces ocasionales y vía láctea opcional.
    /// </summary>
    public class NightSkyRenderer : MonoBehaviour
    {
        public const float Distance = 105f;
        private Transform _moon, _halo, _stars, _milky, _shoot;
        private Renderer _moonR, _haloR, _shootR;
        private Renderer[] _starGroups;
        private float[] _starPhase;
        private MaterialPropertyBlock _mpb;
        private float _vis, _clouds, _moonBright, _shootT = -1f;
        private Vector3 _moonDir, _shootFrom, _shootTo;
        private int _seed, _lastSecond = -1;
        private readonly List<Object> _owned = new List<Object>();

        public float Visible => _vis;
        public Vector3 MoonDirection => _moonDir;
        public int StarCount { get; private set; }
        public static NightSkyRenderer Instance { get; private set; }

        public static NightSkyRenderer Build(Transform root, MapAmbience a, Light sun)
        {
            if (a == null || (a.moon <= 0f && a.stars <= 0f)) return null;
            var go = new GameObject("Night Sky");
            go.transform.SetParent(root, false);
            var n = go.AddComponent<NightSkyRenderer>();
            n.Make(a, sun);
            Instance = n;
            return n;
        }

        private Material Mat(Texture2D tex, string name, bool additive = false)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = tex, renderQueue = 1990 };   // detrás de todo lo opaco del nivel (cielo)
            _owned.Add(m); if (tex != null) _owned.Add(tex);
            return m;
        }

        private Transform Quad(string name, Material m, float size, out Renderer r)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); DestroyImmediate(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(transform, false); q.transform.localScale = Vector3.one * size;
            r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return q.transform;
        }

        private void Make(MapAmbience a, Light sun)
        {
            _mpb = new MaterialPropertyBlock();
            _seed = 13;
            // la luna está donde está la luz principal (de ahí vienen las sombras)
            _moonDir = sun != null ? -sun.transform.forward : Quaternion.Euler(a.sunPitch, a.sunYaw, 0f) * Vector3.back;
            float elev = Mathf.Asin(Mathf.Clamp(_moonDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            _vis = NightSky.Visibility(a.fogDensity, elev, a.moonClouds);
            _clouds = a.moonClouds; _moonBright = a.moon;

            var moonTex = new Texture2D(NightSky.MoonSize, NightSky.MoonSize, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Luna" };
            moonTex.SetPixelData(NightSky.MoonTexture(_seed), 0); moonTex.Apply(true, true);
            if (a.moon > 0f)
            {
                _moon = Quad("Luna", Mat(moonTex, "Luna"), 7f * Mathf.Max(0.3f, a.moonSize), out _moonR);
                _halo = Quad("Halo luna", Mat(Glow(64, 2.2f), "Halo luna"), 26f * Mathf.Max(0.3f, a.moonSize), out _haloR);
                // luz de luna: fría, muy tenue, sin sombras (no cambia el balance de visibilidad)
                var l = new GameObject("Luz de luna").AddComponent<Light>();
                l.transform.SetParent(transform, false); l.transform.rotation = Quaternion.LookRotation(-_moonDir);
                l.type = LightType.Directional; l.color = new Color(0.62f, 0.72f, 1f); l.intensity = 0.08f * a.moon; l.shadows = LightShadows.None;
            }
            if (a.stars > 0f) BuildStars(a.stars);
            if (a.milkyWay > 0f)
            {
                _milky = Quad("Vía láctea", Mat(Band(128, 32), "Vía láctea"), 1f, out var mr);
                _milky.localScale = new Vector3(190f, 34f, 1f);
                _mpb.Clear(); _mpb.SetColor("_Color", new Color(0.7f, 0.75f, 1f, 0.16f * a.milkyWay * _vis)); mr.SetPropertyBlock(_mpb);
            }
            _shoot = Quad("Estrella fugaz", Mat(Streak(64, 8), "Fugaz"), 1f, out _shootR);
            _shoot.localScale = new Vector3(6f, 0.18f, 1f); _shoot.gameObject.SetActive(false);
            AtmosphereBudget.RegisterGroup(gameObject, 4);
        }

        private void BuildStars(float density)
        {
            int count = Mathf.RoundToInt(700 * density);
            var stars = NightSky.Stars(count, _seed + 1);
            StarCount = count;
            var mat = Mat(Glow(16, 1.6f), "Estrellas");
            const int groups = 4;   // cuatro mallas con fases distintas: parpadeo sin tocar vértices
            _starGroups = new Renderer[groups]; _starPhase = new float[groups];
            for (int g = 0; g < groups; g++)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
                for (int i = g; i < stars.Count; i += groups)
                {
                    var s = stars[i]; var dir = new Vector3(s[0], s[1], s[2]); var c = dir * 100f;
                    var right = Vector3.Cross(Vector3.up, dir).normalized; if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                    var up = Vector3.Cross(dir, right);
                    float size = 0.25f + s[3] * 0.55f;
                    int b = v.Count;
                    v.Add(c - right * size - up * size); v.Add(c - right * size + up * size); v.Add(c + right * size + up * size); v.Add(c + right * size - up * size);
                    uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                    var cc = new Color(0.85f + 0.15f * s[3], 0.88f + 0.1f * s[3], 1f, 0.35f + 0.65f * s[3]);
                    for (int k = 0; k < 4; k++) col.Add(cc);
                    tri.Add(b); tri.Add(b + 1); tri.Add(b + 2); tri.Add(b); tri.Add(b + 2); tri.Add(b + 3);
                    tri.Add(b); tri.Add(b + 2); tri.Add(b + 1); tri.Add(b); tri.Add(b + 3); tri.Add(b + 2);   // dos caras
                }
                var mesh = new Mesh { name = "Estrellas " + g }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 220f);
                _owned.Add(mesh);
                var go = new GameObject("Estrellas " + g); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                _starGroups[g] = r; _starPhase[g] = g * 0.37f;
            }
        }

        private void LateUpdate()
        {
            var cam = Camera.main; if (cam == null) return;
            var cp = cam.transform.position;
            transform.position = cp;   // el cielo sigue a la cámara
            double t = Time.timeAsDouble;
            if (_moon != null)
            {
                _moon.position = cp + _moonDir * Distance; _moon.rotation = Quaternion.LookRotation(_moonDir);
                _halo.position = cp + _moonDir * (Distance + 1f); _halo.rotation = _moon.rotation;
                float cover = NightSky.CloudCover(t, _clouds);
                float k = _vis * _moonBright * (1f - 0.75f * cover);
                _mpb.Clear(); _mpb.SetColor("_Color", new Color(1.6f * k + 0.05f, 1.6f * k + 0.05f, 1.7f * k + 0.06f, Mathf.Clamp01(0.25f + k))); _moonR.SetPropertyBlock(_mpb);
                _mpb.Clear(); _mpb.SetColor("_Color", new Color(0.55f, 0.65f, 0.9f, 0.22f * k)); _haloR.SetPropertyBlock(_mpb);
            }
            if (_starGroups != null)
                for (int g = 0; g < _starGroups.Length; g++)
                {
                    _mpb.Clear(); _mpb.SetColor("_Color", new Color(1f, 1f, 1f, _vis * NightSky.Twinkle(t, _starPhase[g]))); _starGroups[g].SetPropertyBlock(_mpb);
                }
            if (_milky != null) { _milky.position = cp + Quaternion.Euler(-50f, 30f, 0f) * Vector3.forward * (Distance + 5f); _milky.rotation = Quaternion.LookRotation(_milky.position - cp) * Quaternion.Euler(0f, 0f, 35f); }
            // estrellas fugaces: una cada ~25 s, 0.8 s cruzando
            int sec = (int)t;
            if (sec != _lastSecond) { _lastSecond = sec; if (_starGroups != null && _shootT < 0f && NightSky.ShootingStarAt(sec, _seed)) StartShoot(sec); }
            if (_shootT >= 0f)
            {
                _shootT += Time.deltaTime / 0.8f;
                if (_shootT >= 1f) { _shootT = -1f; _shoot.gameObject.SetActive(false); }
                else
                {
                    var p = cp + Vector3.Lerp(_shootFrom, _shootTo, _shootT) * Distance;
                    _shoot.position = p; _shoot.rotation = Quaternion.LookRotation(p - cp, Vector3.Cross(_shootTo - _shootFrom, p - cp));
                    _mpb.Clear(); _mpb.SetColor("_Color", new Color(1f, 1f, 1f, _vis * Mathf.Sin(_shootT * Mathf.PI))); _shootR.SetPropertyBlock(_mpb);
                }
            }
        }

        private void StartShoot(int sec)
        {
            var r = new System.Random(sec);
            float a = (float)(r.NextDouble() * Mathf.PI * 2f);
            _shootFrom = new Vector3(Mathf.Cos(a) * 0.6f, 0.7f, Mathf.Sin(a) * 0.6f).normalized;
            _shootTo = new Vector3(Mathf.Cos(a + 0.5f) * 0.8f, 0.45f, Mathf.Sin(a + 0.5f) * 0.8f).normalized;
            _shootT = 0f; _shoot.gameObject.SetActive(true);
        }

        private static Texture2D Glow(int n, float pow)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Brillo" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f, d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(d, pow) * 255));
            }
            t.SetPixels32(px); t.Apply(false); return t;
        }

        private static Texture2D Band(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vía láctea" };
            var px = new Color32[w * h]; var rng = new System.Random(5);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f), u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float a = Mathf.Clamp01(1f - v) * Mathf.Clamp01(1f - u * u) * (0.6f + 0.4f * (float)rng.NextDouble());
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px); t.Apply(false); return t;
        }

        private static Texture2D Streak(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Fugaz" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(u * u) * Mathf.Clamp01(1f - v) * 255));
            }
            t.SetPixels32(px); t.Apply(false); return t;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; foreach (var o in _owned) if (o != null) Destroy(o); }
    }
}

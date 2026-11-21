using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Ventanas iluminadas de los edificios lejanos: una textura procedural (WindowGrid) en UN material emisivo compartido; cada fachada es un quad que toma su
    /// trozo de la textura y su brillo por MaterialPropertyBlock. Sin luces reales ni sombras: solo emisión + bloom. Cada 1.3 s cambian un par de ventanas
    /// (alguna se apaga, otra se enciende). Algunos edificios llevan una baliza roja que parpadea en la azotea.
    /// </summary>
    public class SkylineLights : MonoBehaviour
    {
        private static Material _beacon;
        private Material _windows;
        private Texture2D _tex;
        private WindowGrid.Cell[] _cells;
        private System.Random _rng;
        private float _next;
        private readonly List<Renderer> _beacons = new List<Renderer>();
        private readonly List<float> _phases = new List<float>();
        private MaterialPropertyBlock _mpb;
        private readonly bool[] _beaconState = new bool[64];

        public int Facades { get; private set; }
        public int Beacons => _beacons.Count;

        public static SkylineLights Create(Transform parent)
        {
            var go = new GameObject("SkylineLights");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<SkylineLights>();
            s._mpb = new MaterialPropertyBlock();
            s._rng = new System.Random(11);
            s._cells = WindowGrid.Generate(11);
            s._tex = new Texture2D(WindowGrid.Size, WindowGrid.Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SkylineWindows" };
            s._tex.SetPixelData(WindowGrid.Pixels(s._cells), 0);
            s._tex.Apply(false);
            s._windows = new Material(Prim.GetEmissive(Color.white, 1f)) { name = "SkylineWindows" };
            var m = s._windows;
            m.SetTexture("_BaseMap", s._tex); m.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.36f));
            m.SetTexture("_EmissionMap", s._tex); m.SetColor("_EmissionColor", Color.white);
            m.SetFloat("_Smoothness", 0.5f);
            m.SetShaderPassEnabled("ShadowCaster", false);
            if (_beacon == null) _beacon = Prim.GetEmissive(new Color(1f, 0.08f, 0.05f), 4f);
            AtmosphereBudget.RegisterGroup(go, 4);
            return s;
        }

        /// <summary>Ventanas en las tres fachadas que dan al mapa de un edificio (cuerpo w x h x d con giro "yaw", centro en la base "c").</summary>
        public void AddBuilding(Vector3 c, float w, float h, float d, float yaw, int seed, bool beacon)
        {
            var pivot = new GameObject("Windows").transform;
            pivot.SetParent(transform, false);
            pivot.position = c; pivot.rotation = Quaternion.Euler(0f, yaw, 0f);
            float bright = 0.7f + (seed % 7) * 0.1f;   // tenues (antes 1.6-3.1), cada edificio con su brillo
            float bottom = 1.2f, top = h - 0.8f, fh = top - bottom;
            // el quad de Unity mira a -Z: cara interior (-z), y laterales (+x y -x)
            Facade(pivot, new Vector3(0f, bottom + fh / 2f, -d / 2f - 0.04f), Quaternion.identity, w - 0.8f, fh, seed * 3, bright);
            Facade(pivot, new Vector3(w / 2f + 0.04f, bottom + fh / 2f, 0f), Quaternion.Euler(0f, -90f, 0f), d - 0.8f, fh, seed * 3 + 1, bright);
            Facade(pivot, new Vector3(-w / 2f - 0.04f, bottom + fh / 2f, 0f), Quaternion.Euler(0f, 90f, 0f), d - 0.8f, fh, seed * 3 + 2, bright);
            if (beacon && _beacons.Count < _beaconState.Length)
            {
                var b = Prim.Create(PrimitiveType.Sphere, "Baliza", new Vector3(0f, h + 0.4f, 0f), Vector3.one * 0.55f, _beacon, pivot, false);
                b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                _beacons.Add(b.GetComponent<Renderer>()); _phases.Add((seed % 13) * 0.17f);
            }
        }

        private void Facade(Transform pivot, Vector3 pos, Quaternion rot, float width, float height, int seed, float bright)
        {
            if (width < 1f || height < 2f) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Fachada"; DestroyImmediate(q.GetComponent<Collider>());   // sin collider (la auditoría los rechaza)
            q.transform.SetParent(pivot, false);
            q.transform.localPosition = pos; q.transform.localRotation = rot; q.transform.localScale = new Vector3(width, height, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = _windows; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var st = WindowGrid.FacadeST(width, height, seed);
            _mpb.Clear();
            _mpb.SetVector("_BaseMap_ST", new Vector4(st[0], st[1], st[2], st[3]));
            _mpb.SetColor("_EmissionColor", Color.white * bright);
            r.SetPropertyBlock(_mpb);
            Facades++;
        }

        private void Update()
        {
            if (Time.time >= _next)
            {
                _next = Time.time + 1.3f;
                foreach (int i in WindowGrid.Flicker(_cells, _rng, 2))
                {
                    var rgb = WindowGrid.ColorOf(_cells[i]);
                    _tex.SetPixel((i % WindowGrid.Cols) * 2, (i / WindowGrid.Cols) * 2, new Color32(rgb[0], rgb[1], rgb[2], 255));
                }
                _tex.Apply(false);
            }
            for (int i = 0; i < _beacons.Count; i++)
            {
                bool on = WindowGrid.BeaconOn(Time.timeAsDouble, _phases[i]);
                if (on == _beaconState[i]) continue;   // solo cambia el bloque al encender/apagar: sin trabajo por frame
                _beaconState[i] = on;
                _mpb.Clear(); _mpb.SetColor("_EmissionColor", on ? new Color(1f, 0.08f, 0.05f) * 5f : new Color(0.25f, 0.02f, 0.01f));
                _beacons[i].SetPropertyBlock(_mpb);
            }
        }

        private void OnDestroy() { if (_tex != null) Destroy(_tex); if (_windows != null) Destroy(_windows); }
    }
}

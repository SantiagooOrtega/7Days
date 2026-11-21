using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Luces lejanas en la niebla del Bosque: la ventana de una cabaña y una torre vigía distante (luz cálida y baliza roja), fuera del perímetro.
    /// La niebla del Bosque taparía cualquier objeto a 60 m, así que son halos con un shader sin niebla (Sprites/Default) y un brillo bajo fijo: se ven
    /// como luces difusas detrás de la bruma. Sin luces reales ni sombras; miran a la cámara.
    /// </summary>
    public class FogBeacons : MonoBehaviour
    {
        private readonly List<Transform> _halos = new List<Transform>();
        private Renderer _red;
        private MaterialPropertyBlock _mpb;
        private bool _redOn;
        private Material _mat;
        private Texture2D _tex;

        public int Count => _halos.Count;

        public static FogBeacons Build(Transform parent, float halfSize)
        {
            var go = new GameObject("FogBeacons");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<FogBeacons>();
            f._mpb = new MaterialPropertyBlock();
            f._tex = Halo(64);
            var sh = Shader.Find("Sprites/Default");
            f._mat = new Material(sh) { name = "FogHalo", mainTexture = f._tex, renderQueue = 3000 };
            float r = halfSize + 16f;
            Vector3 At(float deg, float y) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * r, y, Mathf.Cos(deg * Mathf.Deg2Rad) * r);
            // cabaña: dos ventanas cálidas juntas
            f.Add(At(40f, 2.2f), 3.2f, new Color(1f, 0.72f, 0.35f, 0.55f));
            f.Add(At(41.5f, 2.2f), 2.6f, new Color(1f, 0.6f, 0.25f, 0.45f));
            // torre vigía: luz cálida arriba y baliza roja en lo alto
            f.Add(At(205f, 14f), 4f, new Color(1f, 0.8f, 0.5f, 0.5f));
            f._red = f.Add(At(205f, 18f), 3f, new Color(1f, 0.1f, 0.06f, 0.7f)).GetComponent<Renderer>();
            // otra luz pálida, muy lejos
            f.Add(At(300f, 6f), 3.5f, new Color(0.65f, 0.8f, 1f, 0.35f));
            AtmosphereBudget.RegisterGroup(go, 4);
            return f;
        }

        private Transform Add(Vector3 pos, float size, Color c)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Halo"; DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.position = pos; q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = _mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            _mpb.Clear(); _mpb.SetColor("_Color", c); r.SetPropertyBlock(_mpb);
            _halos.Add(q.transform);
            return q.transform;
        }

        private static Texture2D Halo(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FogHalo" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d); a = a * a * (0.6f + 0.4f * Mathf.Clamp01(1f - d * 4f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cr = cam.transform.rotation;
            for (int i = 0; i < _halos.Count; i++) _halos[i].rotation = cr;   // miran a la cámara (son 5)
            bool on = WindowGrid.BeaconOn(Time.timeAsDouble, 0f);
            if (on == _redOn || _red == null) return;
            _redOn = on;
            _mpb.Clear(); _mpb.SetColor("_Color", on ? new Color(1f, 0.1f, 0.06f, 0.7f) : new Color(1f, 0.1f, 0.06f, 0.08f));
            _red.SetPropertyBlock(_mpb);
        }

        private void OnDestroy() { if (_mat != null) Destroy(_mat); if (_tex != null) Destroy(_tex); }
    }
}

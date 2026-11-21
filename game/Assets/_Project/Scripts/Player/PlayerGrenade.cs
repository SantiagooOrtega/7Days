using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Core;
using SevenDays.Combat;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// Lanzar granadas con F (G ya suelta armas): mantener = ver la trayectoria (línea punteada en arco y marca de impacto), soltar = lanzar.
    /// La trayectoria se calcula con GrenadeRules.Arc y se corta en el primer obstáculo con raycasts; los buffers se reservan una vez (sin memoria por frame).
    /// </summary>
    public class PlayerGrenade : MonoBehaviour
    {
        public const int ArcPoints = 40;
        public const float ArcStep = 0.05f;

        private PlayerInventory _inventory;
        private Health _health;
        private LineRenderer _line;
        private Transform _marker;
        private readonly V3[] _arc = new V3[ArcPoints];
        private readonly Vector3[] _points = new Vector3[ArcPoints];
        private bool _aiming;
        private float _next;
        private static Material _lineMat, _markerMat;

        public bool Aiming => _aiming;
        public int LastArcCount { get; private set; }
        public Vector3 LastImpact { get; private set; }

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<Health>();
            var lg = new GameObject("Trayectoria granada");
            lg.transform.SetParent(transform, false);
            _line = lg.AddComponent<LineRenderer>();
            _line.useWorldSpace = true; _line.widthMultiplier = 0.045f; _line.numCapVertices = 0;
            _line.textureMode = LineTextureMode.Tile;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _line.receiveShadows = false;
            if (_lineMat == null) _lineMat = new Material(Shader.Find("Sprites/Default")) { name = "Trayectoria", mainTexture = Dashes(), color = new Color(1f, 0.9f, 0.5f, 0.9f) };
            _line.sharedMaterial = _lineMat;
            _line.positionCount = 0;
            _line.enabled = false;
            var mk = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mk.name = "Marca de impacto"; DestroyImmediate(mk.GetComponent<Collider>());
            mk.transform.SetParent(transform, false); mk.transform.localScale = Vector3.one * (GrenadeRules.Radius * 0.5f);
            if (_markerMat == null) _markerMat = new Material(Shader.Find("Sprites/Default")) { name = "Marca granada", mainTexture = Ring(), color = new Color(1f, 0.45f, 0.2f, 0.75f) };
            var mr = mk.GetComponent<MeshRenderer>(); mr.sharedMaterial = _markerMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _marker = mk.transform; mk.SetActive(false);
        }

        private void Update() { SevenDays.Core.PerfProbe.Begin(SevenDays.Core.PerfSection.Otros); UpdateMedido(); SevenDays.Core.PerfProbe.End(); }


        private void UpdateMedido()
        {
            var kb = Keyboard.current;
            bool canUse = kb != null && Time.timeScale > 0f && (_health == null || !_health.IsDead) && _inventory != null && GrenadeManager.Instance != null;
            if (!canUse) { Show(false); _aiming = false; return; }
            if (kb.fKey.wasPressedThisFrame && _inventory.Grenades.Count > 0 && Time.time >= _next) _aiming = true;
            if (_aiming && kb.fKey.isPressed) { Predict(out _, out _); Show(true); return; }
            if (_aiming && kb.fKey.wasReleasedThisFrame) ThrowNow();
            _aiming = false;
            Show(false);
        }

        /// <summary>Origen (mano derecha) y velocidad de salida hacia donde mira la cámara.</summary>
        public void Aim(out Vector3 origin, out Vector3 velocity)
        {
            var cam = Camera.main != null ? Camera.main.transform : transform;
            Vector3 fwd = cam.forward;
            origin = transform.position + Vector3.up * 1.55f + cam.right * 0.3f + Vector3.ProjectOnPlane(fwd, Vector3.up).normalized * 0.45f;
            var v = GrenadeRules.ThrowVelocity(new V3(fwd.x, fwd.y, fwd.z));
            velocity = new Vector3(v.X, v.Y, v.Z);
        }

        /// <summary>Calcula el arco hasta el primer obstáculo. Devuelve los puntos en _points y el punto de impacto.</summary>
        public int Predict(out Vector3 impact, out Vector3 normal)
        {
            Aim(out var o, out var vel);
            int n = GrenadeRules.Arc(new V3(o.x, o.y, o.z), new V3(vel.x, vel.y, vel.z), ArcStep, _arc);
            int mask = ~((1 << 2) | HitboxLayer.Mask);
            impact = o; normal = Vector3.up;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(_arc[i].X, _arc[i].Y, _arc[i].Z);
                if (i > 0)
                {
                    var a = _points[count - 1];
                    if (Physics.Linecast(a, p, out var hit, mask, QueryTriggerInteraction.Ignore))
                    {
                        _points[count++] = hit.point; impact = hit.point; normal = hit.normal;
                        break;
                    }
                }
                _points[count++] = p; impact = p;
            }
            LastArcCount = count; LastImpact = impact;
            _line.positionCount = count;
            _line.SetPositions(_points);
            _marker.position = impact + normal * 0.03f;
            _marker.rotation = Quaternion.LookRotation(-normal);
            return count;
        }

        private void Show(bool on)
        {
            if (_line.enabled != on) _line.enabled = on;
            if (_marker.gameObject.activeSelf != on) _marker.gameObject.SetActive(on);
        }

        /// <summary>Lanza ya (también lo usan las pruebas). false si no quedan granadas o están todas en el aire.</summary>
        public bool ThrowNow()
        {
            if (_inventory == null || GrenadeManager.Instance == null || _inventory.Grenades.Count <= 0) return false;
            Aim(out var o, out var v);
            if (!GrenadeManager.Instance.Throw(o, v)) return false;
            _inventory.Grenades.TryUse();
            _inventory.NotifyChanged();
            _next = Time.time + GrenadeRules.Cooldown;
            SevenDays.Vigia.VigiaVoice.Say("granada_fuera");
            return true;
        }

        private static Texture2D Dashes()
        {
            var t = new Texture2D(16, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Rayas" };
            var px = new Color32[64];
            for (int y = 0; y < 4; y++) for (int x = 0; x < 16; x++) px[y * 16 + x] = new Color32(255, 255, 255, (byte)(x < 9 ? 255 : 0));
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        private static Texture2D Ring()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Anillo" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * 18f), dot = Mathf.Clamp01(1f - d * 6f), fill = d < 1f ? 0.12f : 0f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(ring, Mathf.Max(dot, fill)) * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }
    }
}

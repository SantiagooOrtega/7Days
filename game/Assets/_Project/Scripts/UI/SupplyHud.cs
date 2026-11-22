using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;
using SevenDays.World.Loot;

namespace SevenDays.UI
{
    /// <summary>
    /// Ayudas para encontrar suministros: flechas en el borde de la pantalla hacia los 4 más cercanos que quedan fuera de la vista (a menos de 25 m), un "ping" suave
    /// la primera vez que uno entra a menos de 15 m, y al mirar uno de cerca su nombre, cantidad, rareza y "E recoger". Solo informa: no cambia qué ni cuánto hay.
    /// </summary>
    public class SupplyHud : MonoBehaviour
    {
        private void Awake() { useGUILayout = false; }   // sin GUILayout: Unity se salta la pasada de Layout (la mitad del coste de IMGUI)

        private const int MaxArrows = 4;
        private readonly List<(LootPickup p, float d)> _near = new List<(LootPickup, float)>(32);
        private Texture2D _arrow;
        private GUIStyle _label, _small;
        private AudioSource _audio;
        private AudioClip _ping;
        private float _nextPing;
        private LootPickup _looked;

        private void Start()
        {
            _arrow = ArrowTexture();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 0f;
            AudioMix.Route(_audio, AudioGroup.UI, 0.5f);
            _ping = PingClip();
        }

        private void Update() { SevenDays.Core.PerfProbe.Begin(SevenDays.Core.PerfSection.Loot); UpdateMedido(); SevenDays.Core.PerfProbe.End(); }


        private void UpdateMedido()
        {
            var mgr = LootManager.Instance; var cam = Camera.main;
            _near.Clear(); _looked = null;
            if (mgr == null || cam == null) return;
            Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
            float bestAngle = float.MaxValue;
            var act = mgr.Active;
            for (int i = 0; i < act.Count; i++)   // con índice: foreach sobre IReadOnlyList reservaba un enumerador cada frame
            {
                var p = act[i];
                if (p == null || !p.gameObject.activeInHierarchy || !p.Reachable) continue;   // solo lo alcanzable
                Vector3 c = p.GlintPosition;
                float d = Vector3.Distance(eye, c);
                if (d <= SupplyVisibility.CompassRange) _near.Add((p, d));
                if (d <= SupplyVisibility.PingRange && !p.Pinged)
                {
                    p.Pinged = true;
                    if (Time.time >= _nextPing && _ping != null) { _nextPing = Time.time + 0.8f; _audio.PlayOneShot(_ping, 0.6f); }
                }
                float angle = Vector3.Angle(fwd, c - eye);
                if (SupplyVisibility.LookedAt(d, angle) && angle < bestAngle) { bestAngle = angle; _looked = p; }
            }
            _near.Sort((a, b) => a.d.CompareTo(b.d));
        }

        private void OnGUI() { if (SevenDays.Core.HudGate.Hidden) return; SevenDays.Core.PerfProbe.Begin(SevenDays.Core.PerfSection.UI); OnGUIMedido(); SevenDays.Core.PerfProbe.End(); }


        private void OnGUIMedido()
        {
            if (SevenDays.UI.StatsScreen.Blocking) return;   // pantalla de muerte/victoria: sin HUD de juego
            var cam = Camera.main;
            if (cam == null || Event.current.type != EventType.Repaint) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _small ??= new GUIStyle(_label) { fontSize = 14, fontStyle = FontStyle.Normal };
            int arrows = 0;
            foreach (var (p, d) in _near)
            {
                if (arrows >= MaxArrows) break;
                if (p == null) continue;   // recogido o destruido desde el último Update (recarga, reintentar, cambio de mapa)
                Vector3 v = cam.WorldToViewportPoint(p.GlintPosition);
                if (SupplyVisibility.OnScreen(v.x, v.y, v.z)) continue;   // a la vista: ya lo marca su icono flotante
                arrows++;
                var dir = new V2(v.x - 0.5f, v.y - 0.5f);
                if (v.z < 0f) dir = new V2(-dir.X, -dir.Y);
                var e = SupplyVisibility.EdgePoint(dir, v.z < 0f);
                float x = e.X * Screen.width, y = (1f - e.Y) * Screen.height;
                float ang = Mathf.Atan2(-(e.Y - 0.5f), e.X - 0.5f) * Mathf.Rad2Deg;
                var col = TintOf(p); col.a = Mathf.Lerp(0.95f, 0.45f, d / SupplyVisibility.CompassRange);
                var prev = GUI.color; var m = GUI.matrix;
                GUI.color = col;
                GUIUtility.RotateAroundPivot(ang, new Vector2(x, y));
                GUI.DrawTexture(new Rect(x - 14, y - 14, 28, 28), _arrow);
                GUI.matrix = m;
                var icon = SupplyArt.IconMaterial(p.Kind, p.Rarity).mainTexture;
                Vector2 inward = new Vector2(0.5f * Screen.width - x, 0.5f * Screen.height - y).normalized * 30f;
                GUI.color = new Color(1f, 1f, 1f, col.a);
                if (icon != null) GUI.DrawTexture(new Rect(x + inward.x - 14, y + inward.y - 14, 28, 28), icon);
                GUI.Label(new Rect(x + inward.x - 30, y + inward.y + 12, 60, 18), Meters(Mathf.RoundToInt(d)), _small);
                GUI.color = prev;
            }
            if (_looked != null)
            {
                Vector3 v = cam.WorldToScreenPoint(_looked.GlintPosition + Vector3.up * 0.75f);
                if (v.z > 0f)
                {
                    var item = _looked.Item;
                    if (!ReferenceEquals(_lookedShown, _looked) || _lookedAmount != item.amount)
                    {
                        // la etiqueta se compone al mirar otro objeto, no cada frame
                        _lookedShown = _looked; _lookedAmount = item.amount;
                        _lookedName = Capital(LootPickup.ItemName(item)) + (item.kind == "weapon" ? "" : " x" + item.amount);
                        _lookedSub = WeaponCatalog.RarityName(_looked.Rarity) + "  ·  E recoger";
                    }
                    var r = new Rect(v.x - 170, Screen.height - v.y - 40, 340, 26);
                    var prev = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 46), Texture2D.whiteTexture);
                    GUI.color = TintOf(_looked);
                    GUI.Label(r, _lookedName, _label);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(r.x, r.y + 22, r.width, 22), _lookedSub, _small);
                    GUI.color = prev;
                }
            }
        }

        private LootPickup _lookedShown;
        private int _lookedAmount;
        private string _lookedName = "", _lookedSub = "";
        private static readonly string[] MeterLabels = new string[201];
        private static string Meters(int m) { m = Mathf.Clamp(m, 0, 200); return MeterLabels[m] ??= m + " m"; }   // una vez por distancia
        private static readonly System.Collections.Generic.Dictionary<SupplyKind, Color> KindColors = new System.Collections.Generic.Dictionary<SupplyKind, Color>();

        private static Color TintOf(LootPickup p)
        {
            if (p.Kind == SupplyKind.Weapon) { var w = WeaponCatalog.RarityColor(p.Rarity); return new Color(w[0], w[1], w[2], 1f); }
            if (!KindColors.TryGetValue(p.Kind, out var col)) { var c = SupplyModel.TypeColor(p.Kind); col = new Color(c[0], c[1], c[2], 1f); KindColors[p.Kind] = col; }   // una vez por tipo
            return col;
        }

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Flecha (triángulo con borde oscuro) que apunta a la derecha.</summary>
        private static Texture2D ArrowTexture()
        {
            const int n = 32; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n - 0.5f;
                    float inside = u > 0.15f && Mathf.Abs(v) < (0.95f - u) * 0.55f ? 1f : 0f;
                    float edge = u > 0.08f && Mathf.Abs(v) < (1.02f - u) * 0.6f ? 1f : 0f;
                    t.SetPixel(x, y, inside > 0f ? Color.white : new Color(0f, 0f, 0f, edge * 0.7f));
                }
            t.Apply();
            return t;
        }

        /// <summary>"Ping" suave de dos notas (sintetizado).</summary>
        private static AudioClip PingClip()
        {
            const int rate = 22050; int n = (int)(0.32f * rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = t < 0.11f ? 1320f : 1760f, a = t < 0.11f ? Mathf.Exp(-t * 30f) : Mathf.Exp(-(t - 0.11f) * 14f);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * a * Mathf.Min(1f, i / 60f) * 0.35f;
            }
            var c = AudioClip.Create("ping suministro", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}

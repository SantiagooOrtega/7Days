using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SevenDays.Core;
using SevenDays.Procedural;
using SevenDays.UI.Tactical;

namespace SevenDays.UI
{
    /// <summary>
    /// Pantalla de muerte, victoria y resumen del día con el estilo táctico común (UI.Tactical): fondo en llamas, cabecera "HAS CAÍDO" con glitch, día y frase de
    /// VIGÍA a máquina de escribir; lista de estadísticas con la fila activa resaltada (flechas o ratón) y su explicación, barras del director, gráfica por día
    /// (nunca vacía), insignias, silueta del personaje y pie con teclas (Enter Reintentar día · Esc Menú · Tab Estadísticas). Oculta todo el HUD y bloquea los
    /// ajustes mientras se ve. Contadores animados y entrada escalonada sin memoria por frame (cadenas en caché).
    /// </summary>
    public class StatsScreen : MonoBehaviour
    {
        public static StatsScreen Instance { get; private set; }
        public static bool Active => Instance != null && Instance._root != null && Instance._root.gameObject.activeSelf;
        public static bool Blocking => Active && Instance._mode != StatsScreenMode.DaySummary;

        public const float Stagger = 0.12f, FadeIn = 0.35f, CountSeconds = 1.2f, SummarySeconds = 10f, TypeSpeed = 28f;
        public static string[] StatNames => StatsReport.StatNames;

        private StatsScreenMode _mode;
        private RectTransform _root, _table;
        private readonly List<CanvasGroup> _sections = new List<CanvasGroup>();
        private readonly List<(Text text, int target, int shown, bool pct)> _counters = new List<(Text, int, int, bool)>();
        private float _shownAt, _hideAt;
        private Text _title, _ghostA, _ghostB, _typed, _explain;
        private string _typeText = ""; private int _typedChars = -1;
        private MenuList _stats;
        private string[] _statExplain = new string[0];
        private AudioSource _audio;
        private static readonly string[] Ints = BuildInts(5000), Percents = BuildPercents();
        private readonly List<Object> _owned = new List<Object>();

        public StatsScreenMode Mode => _mode;
        public int MedalCount { get; private set; }
        public int Rows { get; private set; }
        public int StatRows => _stats != null ? _stats.Count : 0;

        private static string[] BuildInts(int n) { var a = new string[n + 1]; for (int i = 0; i <= n; i++) a[i] = i.ToString(); return a; }
        private static string[] BuildPercents() { var a = new string[101]; for (int i = 0; i <= 100; i++) a[i] = i + " %"; return a; }

        public static StatsScreen Show(StatsScreenMode mode, int day, IList<DayStats> days, IList<string> reasons)
        {
            if (Instance == null) Instance = new GameObject("StatsScreen").AddComponent<StatsScreen>();
            Instance.Build(mode, day, days, reasons);
            return Instance;
        }

        public static void Hide() { if (Instance != null && Instance._root != null) Instance._root.gameObject.SetActive(false); GameInput.EscapeBlocked = false; }

        private void OnDestroy() { if (Instance == this) Instance = null; GameInput.EscapeBlocked = false; foreach (var o in _owned) if (o != null) Destroy(o); }

        // ---------------------------------------------------------------- construcción
        private void Build(StatsScreenMode mode, int day, IList<DayStats> days, IList<string> reasons)
        {
            _mode = mode;
            if (_root != null) Destroy(_root.gameObject);
            _sections.Clear(); _counters.Clear(); _stats = null; _title = _ghostA = _ghostB = _typed = _explain = null; _typedChars = -1;
            _root = UiKit.Canvas(transform, "Canvas estadísticas", 500);
            var list = new List<DayStats>(days);
            var today = list.Count > 0 ? list[list.Count - 1] : new DayStats { day = day };
            bool compact = mode == StatsScreenMode.DaySummary;
            if (compact) BuildCompact(day, today); else BuildFull(mode, day, list, today, reasons);
            _shownAt = Time.unscaledTime;
            _hideAt = compact ? Time.unscaledTime + SummarySeconds : float.MaxValue;
            GameInput.EscapeBlocked = !compact;
            if (!compact) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            PlayThump(mode);
        }

        private RectTransform Section(string name, Vector2 pos, Vector2 size)
        {
            var r = UiKit.Rect(_root, name, new Vector2(0.5f, 0.5f), pos, size);
            var cg = r.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f; _sections.Add(cg);
            return r;
        }

        private void Counter(Text t, int target, bool pct) { t.text = pct ? "0 %" : "0"; _counters.Add((t, target, 0, pct)); }

        private void BuildFull(StatsScreenMode mode, int day, List<DayStats> list, DayStats today, IList<string> reasons)
        {
            bool dead = mode == StatsScreenMode.Defeat;
            FlameBackdrop.Create(_root, dead ? new Color(0.75f, 0.55f, 0.5f) : new Color(0.62f, 0.7f, 0.52f));
            UiKit.Image(_root, "Velo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), dead ? new Color(0.2f, 0f, 0f, 0.35f) : new Color(0f, 0.05f, 0f, 0.3f));

            // cabecera con glitch, día y VIGÍA a máquina de escribir
            var head = Section("Cabecera", new Vector2(0f, 410f), new Vector2(1600f, 220f));
            string title = dead ? "HAS CAÍDO" : "HAS SOBREVIVIDO";
            _ghostA = UiKit.Text(head, title, 120, new Color(0f, 0.9f, 1f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(1500f, 140f), true);
            _ghostB = UiKit.Text(head, title, 120, new Color(1f, 0.1f, 0.1f, 0.45f), TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(1500f, 140f), true);
            _title = UiKit.Text(head, title, 120, dead ? UiKit.Red : UiKit.Orange, TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(1500f, 140f), true);
            UiKit.Image(head, "Línea", new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(900f, 3f), UiKit.Cream);
            UiKit.Text(head, dead ? "DÍA " + day + " DE " + DayPlans.Days : DayPlans.Days + " DÍAS · VALLE HONDO", 34, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0f, -64f), new Vector2(900f, 40f), true);
            _typeText = dead ? "VIGÍA: «Señal vital perdida.»" : "VIGÍA: «Siete días. Sigues respirando.»";
            _typed = UiKit.Text(head, "", 26, UiKit.CreamDim, TextAnchor.MiddleCenter, new Vector2(0f, -100f), new Vector2(1200f, 34f), false, false);

            // lista de estadísticas con la fila activa resaltada y su explicación
            var left = Section("Estadísticas", new Vector2(-520f, -40f), new Vector2(720f, 560f));
            UiKit.Panel(left, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 560f));
            UiKit.Header(left, "Estadísticas", new Vector2(0f, 245f), 680f);
            var values = StatsReport.StatValues(list, out int kills, out int heads, out int acc, out int dmg);
            _stats = MenuList.Create(left, new Vector2(0f, 20f), 640f, StatNames, values);
            _stats.SetValue(0, "0"); _stats.SetValue(1, "0"); _stats.SetValue(2, "0 %"); _stats.SetValue(3, "0");
            _statExplain = new[] {
                "Zombis abatidos en toda la partida.", "Bajas rematadas a la cabeza (la puntuación las premia).", "Disparos que acertaron sobre los disparados.",
                "Vida perdida por golpes, ácido y explosiones.", "Tiempo total de las oleadas jugadas.", "Armas con las que disparaste.", "Bajas seguidas a menos de 4 s entre sí." };
            _explain = UiKit.Text(left, _statExplain[0], 20, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(0f, -240f), new Vector2(640f, 30f), false, false);
            _stats.Selected += i => { if (_explain != null) _explain.text = _statExplain[i]; };
            _stats.Confirmed += _ => { };
            _countRows = new[] { (0, kills, false), (1, heads, false), (2, acc, true), (3, dmg, false) };

            // derecha: barras del director, gráfica, insignias y silueta
            var right = Section("Rendimiento", new Vector2(390f, -40f), new Vector2(1000f, 560f));
            UiKit.Panel(right, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f));
            UiKit.Header(right, "Rendimiento del día " + today.day + " · " + Mathf.RoundToInt(today.rawScore) + "/100", new Vector2(0f, 245f), 960f);
            var bars = StatsReport.Bars(today);
            for (int i = 0; i < bars.Length; i++)
            {
                float y = 180f - i * 52f;
                UiKit.Text(right, bars[i].Name.ToUpperInvariant(), 20, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(-330f, y), new Vector2(200f, 26f), true);
                UiKit.Image(right, "Fondo barra", new Vector2(0.5f, 0.5f), new Vector2(-70f, y), new Vector2(300f, 12f), new Color(1f, 1f, 1f, 0.08f));
                var f = UiKit.Image(right, "Barra", new Vector2(0.5f, 0.5f), new Vector2(-220f, y), new Vector2(3f * bars[i].Value, 12f), Color.white, UiKit.HighlightSprite);
                f.rectTransform.pivot = new Vector2(0f, 0.5f);
                Counter(UiKit.Text(right, "", 22, UiKit.Cream, TextAnchor.MiddleRight, new Vector2(110f, y), new Vector2(60f, 26f), true), Mathf.RoundToInt(bars[i].Value), false);
                UiKit.Text(right, bars[i].Explain, 16, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(-200f, y - 20f), new Vector2(560f, 20f), false, false);
            }
            UiKit.Text(right, "BAJAS Y PRECISIÓN POR DÍA", 18, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(-260f, -110f), new Vector2(400f, 24f), true);
            var chart = UiKit.Image(right, "Gráfica", new Vector2(0.5f, 0.5f), new Vector2(-220f, -175f), new Vector2(460f, 110f), Color.white);
            var ctex = UiKit.Make(230, 56, StatsReport.Chart(list, 230, 56), "Gráfica", TextureWrapMode.Clamp); _owned.Add(ctex);
            chart.sprite = Sprite.Create(ctex, new Rect(0, 0, 230, 56), Vector2.one * 0.5f);
            var medals = StatsReport.Medals(list); MedalCount = medals.Count;
            UiKit.Text(right, "INSIGNIAS", 18, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(160f, -110f), new Vector2(200f, 24f), true);
            if (medals.Count == 0) UiKit.Text(right, "Ninguna esta vez", 18, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(170f, -160f), new Vector2(260f, 30f), false, false);
            for (int i = 0; i < medals.Count && i < 4; i++)
            {
                var m = UiKit.Image(right, "Insignia", new Vector2(0.5f, 0.5f), new Vector2(110f + i * 72f, -165f), new Vector2(56f, 56f), Color.white); m.sprite = Badge(medals[i]);
                UiKit.Text(right, StatsReport.MedalName(medals[i]), 13, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(110f + i * 72f, -206f), new Vector2(80f, 18f), true);
            }
            var sil = UiKit.Image(right, "Silueta", new Vector2(0.5f, 0.5f), new Vector2(400f, 70f), new Vector2(150f, 250f), dead ? new Color(1f, 0.6f, 0.55f, 0.7f) : new Color(0.9f, 0.9f, 0.8f, 0.85f));
            var stex = UiKit.Make(60, 100, MenuArt.Soldier(60, 100, new[] { 0.9f, 0.84f, 0.7f }), "Silueta", TextureWrapMode.Clamp); _owned.Add(stex);
            sil.sprite = Sprite.Create(stex, new Rect(0, 0, 60, 100), Vector2.one * 0.5f);

            // tabla por día (Tab) con la explicación del director
            var rows = StatsReport.DayTable(list); Rows = rows.Count - 1;
            _table = UiKit.Panel(_root, "Tabla", new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1240f, 520f));
            UiKit.Header(_table, "Resumen por día", new Vector2(0f, 230f), 1200f);
            for (int r = 0; r < rows.Count; r++)
            {
                if (r == rows.Count - 1 && r > 0) UiKit.Image(_table, "Fila activa", new Vector2(0.5f, 0.5f), new Vector2(0f, 170f - r * 40f), new Vector2(1160f, 36f), Color.white, UiKit.HighlightSprite);
                for (int c = 0; c < rows[r].Length; c++) UiKit.Text(_table, rows[r][c], r == 0 ? 18 : 22, r == 0 ? UiKit.CreamDim : UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(-500f + c * 165f, 170f - r * 40f), new Vector2(160f, 32f), r == 0);
            }
            if (reasons != null && reasons.Count > 0) UiKit.Text(_table, "Director: " + string.Join(" · ", reasons), 16, UiKit.CreamDim, TextAnchor.UpperLeft, new Vector2(0f, -200f), new Vector2(1160f, 60f), false, false).horizontalOverflow = HorizontalWrapMode.Wrap;
            _table.gameObject.SetActive(false);

            // pie con teclas (y botones para el ratón)
            UiKit.Footer(_root, (dead ? "Enter Reintentar día" : "Enter Jugar otra vez") + "   ·   Esc Menú   ·   Tab Estadísticas   ·   ↑↓ Ver dato");
            KeyButton(dead ? "REINTENTAR DÍA" : "JUGAR OTRA VEZ", new Vector2(520f, -484f), Retry);
            KeyButton("MENÚ", new Vector2(720f, -484f), ToMenu);
            KeyButton("ESTADÍSTICAS", new Vector2(880f, -484f), ToggleTable);
        }

        private (int row, int target, bool pct)[] _countRows = new (int, int, bool)[0];

        private void KeyButton(string text, Vector2 pos, UnityEngine.Events.UnityAction a)
        {
            var img = UiKit.Image(_root, "Botón " + text, new Vector2(0.5f, 0.5f), pos, new Vector2(190f, 44f), Color.white, UiKit.FrameSprite, true);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(() => { UiSounds.Accept(); a(); });
            UiKit.Text(img.transform, text, 20, UiKit.Cream, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(200f, 40f), true);
        }

        private void Retry() { bool dead = _mode == StatsScreenMode.Defeat; Hide(); if (dead) SevenDays.World.DayLoop.RetryDay(); else SevenDays.World.DayLoop.Retry(); }
        private void ToMenu() { Hide(); SevenDays.World.DayLoop.ToMenu(); }
        private void ToggleTable() { if (_table != null) _table.gameObject.SetActive(!_table.gameObject.activeSelf); }

        private void BuildCompact(int day, DayStats d)
        {
            var p = Section("Resumen del día", new Vector2(640f, 250f), new Vector2(600f, 380f));
            UiKit.Panel(p, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 380f));
            UiKit.Header(p, "Día " + day + " superado", new Vector2(0f, 160f), 560f);
            string[] n = { "BAJAS", "CABEZA", "PRECISIÓN" }; int[] v = { d.kills, d.headshotKills, Mathf.RoundToInt(d.Accuracy * 100f) };
            for (int i = 0; i < 3; i++)
            {
                UiKit.Text(p, n[i], 17, UiKit.CreamDim, TextAnchor.MiddleCenter, new Vector2(-180f + i * 180f, 100f), new Vector2(170f, 22f), true);
                Counter(UiKit.Text(p, "", 38, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(-180f + i * 180f, 64f), new Vector2(170f, 44f), true), v[i], i == 2);
            }
            var bars = StatsReport.Bars(d);
            for (int i = 0; i < bars.Length; i++)
            {
                float y = 12f - i * 32f;
                UiKit.Text(p, bars[i].Name.ToUpperInvariant(), 15, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(-180f, y), new Vector2(170f, 20f), true);
                var f = UiKit.Image(p, "Barra", new Vector2(0.5f, 0.5f), new Vector2(-80f, y), new Vector2(3.2f * bars[i].Value, 10f), Color.white, UiKit.HighlightSprite);
                f.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
        }

        // ---------------------------------------------------------------- animación y teclas (sin memoria por frame)
        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            float t = Time.unscaledTime - _shownAt;
            for (int i = 0; i < _sections.Count; i++) { float a = Mathf.Clamp01((t - i * Stagger) / FadeIn); if (_sections[i].alpha != a) _sections[i].alpha = a; }
            float ct = Mathf.Clamp01((t - 0.2f) / CountSeconds);
            for (int i = 0; i < _counters.Count; i++)
            {
                var c = _counters[i]; int v = StatsReport.CountUp(c.target, ct);
                if (v == c.shown) continue;
                c.shown = v; c.text.text = c.pct ? (v >= 0 && v <= 100 ? Percents[v] : v + " %") : (v >= 0 && v < Ints.Length ? Ints[v] : v.ToString()); _counters[i] = c;
            }
            if (_stats != null && ct <= 1f)
                foreach (var (row, target, pct) in _countRows) { int v = StatsReport.CountUp(target, ct); _stats.SetValue(row, pct ? (v >= 0 && v <= 100 ? Percents[v] : v + " %") : (v >= 0 && v < Ints.Length ? Ints[v] : v.ToString())); }
            // glitch: cortes y desplazamientos de color al entrar y de vez en cuando
            if (_title != null)
            {
                bool g = t < 1.2f || Mathf.Repeat(t, 4.3f) < 0.12f;
                float jx = g ? Random.Range(-14f, 14f) : 0f;
                _ghostA.rectTransform.anchoredPosition = new Vector2(jx - (g ? 6f : 0f), 40f + (g ? Random.Range(-4f, 4f) : 0f));
                _ghostB.rectTransform.anchoredPosition = new Vector2(-jx + (g ? 6f : 0f), 40f);
                _ghostA.enabled = _ghostB.enabled = g;
            }
            // VIGÍA a máquina de escribir
            if (_typed != null)
            {
                int n = Mathf.Clamp(Mathf.FloorToInt((t - 0.6f) * TypeSpeed), 0, _typeText.Length);
                if (n != _typedChars) { _typedChars = n; _typed.text = _typeText.Substring(0, n); }
            }
            if (Blocking)
            {
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) { UiSounds.Accept(); Retry(); return; }
                    if (k.escapeKey.wasPressedThisFrame) { UiSounds.Back(); ToMenu(); return; }
                    if (k.tabKey.wasPressedThisFrame) { UiSounds.Move(); ToggleTable(); }
                }
                if (_stats != null) _stats.Focused = false;   // Enter/Esc los lleva la pantalla; flechas a mano
                if (_stats != null && k != null) { if (k.downArrowKey.wasPressedThisFrame) _stats.Select(_stats.Index + 1, true); else if (k.upArrowKey.wasPressedThisFrame) _stats.Select(_stats.Index - 1, true); }
            }
            if (Time.unscaledTime >= _hideAt) _root.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- texturas
        /// <summary>Insignia: escudo con borde y símbolo propio.</summary>
        private Sprite Badge(Medal m)
        {
            const int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Insignia " + m }; _owned.Add(t); var px = new Color32[n * n];
            Color body = m == Medal.Francotirador ? new Color(0.35f, 0.45f, 0.6f) : m == Medal.SinCobertura ? new Color(0.6f, 0.25f, 0.2f) : m == Medal.Racha ? new Color(0.75f, 0.5f, 0.15f) : m == Medal.Granadero ? new Color(0.4f, 0.48f, 0.25f) : m == Medal.Intocable ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.55f, 0.45f, 0.3f);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float shield = Mathf.Max(Mathf.Abs(u) - 0.8f, v > 0f ? v - 0.85f : Mathf.Abs(u) - 0.8f * (1f + v / 0.95f));
                bool inside = shield < 0f, rim = shield > -0.1f && inside;
                float r = Mathf.Sqrt(u * u + (v - 0.05f) * (v - 0.05f)), a = Mathf.Atan2(v - 0.05f, u);
                bool sym = m == Medal.Francotirador ? (Mathf.Abs(r - 0.35f) < 0.06f || (Mathf.Abs(u) < 0.04f && r < 0.5f) || (Mathf.Abs(v - 0.05f) < 0.04f && r < 0.5f))
                    : m == Medal.Racha ? r < 0.2f + 0.22f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 3f)
                    : m == Medal.Granadero ? (r < 0.3f || (Mathf.Abs(u) < 0.08f && v > 0.3f && v < 0.55f))
                    : m == Medal.Intocable ? Mathf.Abs(r - 0.32f) < 0.07f
                    : (Mathf.Abs(u) < 0.06f && Mathf.Abs(v) < 0.45f) || (Mathf.Abs(v - 0.15f) < 0.06f && Mathf.Abs(u) < 0.3f);
                px[y * n + x] = !inside ? new Color(0, 0, 0, 0) : rim ? new Color(0.9f, 0.82f, 0.55f, 1f) : sym ? new Color(0.95f, 0.92f, 0.82f, 1f) : body;
            }
            t.SetPixels32(px); t.Apply(false);
            return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * 0.5f);
        }

        private void PlayThump(StatsScreenMode mode)
        {
            if (_audio == null) { _audio = gameObject.AddComponent<AudioSource>(); _audio.spatialBlend = 0f; _audio.playOnAwake = false; _audio.ignoreListenerPause = true; }
            const int rate = 44100; int n = (int)(rate * 1.1f);
            var clip = AudioClip.Create("Golpe estadísticas", n, 1, rate, false); _owned.Add(clip);
            var data = new float[n]; var rng = new System.Random(3);
            float f0 = mode == StatsScreenMode.Defeat ? 62f : 98f;
            for (int i = 0; i < n; i++) { float tt = i / (float)rate; float env = Mathf.Exp(-tt * 4.2f); data[i] = env * (0.55f * Mathf.Sin(2f * Mathf.PI * f0 * tt * (1f - 0.25f * tt)) + 0.05f * (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-tt * 18f)); }
            clip.SetData(data, 0);
            _audio.PlayOneShot(clip, mode == StatsScreenMode.DaySummary ? 0.35f : 0.7f);
        }
    }
}

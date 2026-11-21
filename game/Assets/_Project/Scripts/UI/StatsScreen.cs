using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// Pantalla de muerte, victoria y resumen del día (uGUI, estilo militar táctico oscuro, diseño propio). Canvas Scale With Screen Size (1920x1080):
    /// fundido rojo oscuro con viñeta, título "HAS CAÍDO — DÍA N", frase de VIGÍA, rejilla de 6 datos con icono, barras 0-100 del director con su explicación,
    /// gráfica de bajas y precisión por día, medallas y mejor racha, tabla por día y botones grandes. Entrada escalonada con contadores que suben y un golpe
    /// de sonido sobrio. Los datos los calcula StatsReport (puro). Sin memoria por frame: los textos de los números salen de una caché y solo cambian al subir.
    /// Mientras se ve (muerte/victoria) Esc no abre los ajustes.
    /// </summary>
    public class StatsScreen : MonoBehaviour
    {
        public static StatsScreen Instance { get; private set; }
        public static bool Active => Instance != null && Instance._root != null && Instance._root.activeSelf;
        public static bool Blocking => Active && Instance._mode != StatsScreenMode.DaySummary;

        public const float Stagger = 0.12f, FadeIn = 0.35f, CountSeconds = 1.2f, SummarySeconds = 10f;
        private static readonly Color Red = new Color(0.86f, 0.2f, 0.16f), Amber = new Color(1f, 0.72f, 0.25f), Olive = new Color(0.55f, 0.66f, 0.38f), Ink = new Color(0.05f, 0.055f, 0.06f, 0.92f), Line = new Color(0.82f, 0.78f, 0.66f), Muted = new Color(0.7f, 0.7f, 0.66f);

        private StatsScreenMode _mode;
        private GameObject _root, _tablePanel;
        private Font _font;
        private readonly List<CanvasGroup> _sections = new List<CanvasGroup>();
        private readonly List<(Text text, int target, int shown, string suffix)> _counters = new List<(Text, int, int, string)>();
        private float _shownAt, _hideAt;
        private AudioSource _audio;
        private static readonly string[] Ints = BuildInts(5000);
        private readonly List<Object> _owned = new List<Object>();

        public StatsScreenMode Mode => _mode;
        public int MedalCount { get; private set; }
        public int Rows { get; private set; }

        private static string[] BuildInts(int n) { var a = new string[n + 1]; for (int i = 0; i <= n; i++) a[i] = i.ToString(); return a; }
        private static string Int(int v) => v >= 0 && v < Ints.Length ? Ints[v] : v.ToString();

        /// <summary>Muestra la pantalla. Muerte y victoria bloquean (botones); el resumen del día es un panel compacto que se va solo y no bloquea.</summary>
        public static StatsScreen Show(StatsScreenMode mode, int day, IList<DayStats> days, IList<string> reasons)
        {
            if (Instance == null) { var go = new GameObject("StatsScreen"); Instance = go.AddComponent<StatsScreen>(); }
            Instance.Build(mode, day, days, reasons);
            return Instance;
        }

        public static void Hide() { if (Instance != null && Instance._root != null) Instance._root.SetActive(false); GameInput.EscapeBlocked = false; }

        private void OnDestroy() { if (Instance == this) Instance = null; GameInput.EscapeBlocked = false; foreach (var o in _owned) if (o != null) Destroy(o); }

        // ---------------------------------------------------------------- construcción
        private void Build(StatsScreenMode mode, int day, IList<DayStats> days, IList<string> reasons)
        {
            _mode = mode;
            if (_root != null) Destroy(_root);
            _sections.Clear(); _counters.Clear();
            _font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            _root = new GameObject("Canvas estadísticas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)_root.transform;

            var list = new List<DayStats>(days);
            var today = list.Count > 0 ? list[list.Count - 1] : new DayStats { day = day };
            bool compact = mode == StatsScreenMode.DaySummary;
            if (compact) BuildCompact(rt, day, today);
            else BuildFull(rt, mode, day, list, today, reasons);

            _shownAt = Time.unscaledTime;
            _hideAt = compact ? Time.unscaledTime + SummarySeconds : float.MaxValue;
            GameInput.EscapeBlocked = !compact;
            if (!compact) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            PlayThump(mode);
        }

        private void BuildFull(RectTransform rt, StatsScreenMode mode, int day, List<DayStats> list, DayStats today, IList<string> reasons)
        {
            bool dead = mode == StatsScreenMode.Defeat;
            // fondo: rojo oscuro desaturado (o verde oliva en la victoria) con viñeta
            Img(rt, "Fondo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, dead ? new Color(0.16f, 0.03f, 0.03f, 0.82f) : new Color(0.05f, 0.08f, 0.05f, 0.82f));
            var vig = Img(rt, "Viñeta", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.85f)); vig.sprite = Sprite.Create(Vignette(128), new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            // título condensado con línea decorativa y frase de VIGÍA
            var head = Section(rt, "Cabecera", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1500f, 210f));
            var title = Label(head, dead ? "HAS CAÍDO — DÍA " + day : "HAS SOBREVIVIDO — " + DayPlans.Days + " DÍAS", 86, FontStyle.Bold, dead ? Red : Amber, TextAnchor.MiddleCenter, new Vector2(0f, 30f), new Vector2(1760f, 110f));
            title.rectTransform.localScale = new Vector3(0.84f, 1f, 1f);   // tipografía "condensada"
            Img(head, "Línea", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(980f, 3f), Line);
            Img(head, "Marca izq", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500f, -32f), new Vector2(14f, 14f), dead ? Red : Amber).rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Img(head, "Marca der", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500f, -32f), new Vector2(14f, 14f), dead ? Red : Amber).rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Label(head, dead ? "VIGÍA: «Señal vital perdida.»" : "VIGÍA: «Siete días. Lo has conseguido.»", 30, FontStyle.Italic, Muted, TextAnchor.MiddleCenter, new Vector2(0f, -72f), new Vector2(1400f, 44f));

            // rejilla de 6 datos
            var grid = Section(rt, "Datos", new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(1500f, 150f));
            int kills = 0, heads = 0, fired = 0, hit = 0; float dmg = 0f, wave = 0f;
            foreach (var d in list) { kills += d.kills; heads += d.headshotKills; fired += d.shotsFired; hit += Mathf.Min(d.shotsHit, d.shotsFired); dmg += d.damageTaken; wave += d.waveSeconds; }
            string[] names = { "BAJAS", "A LA CABEZA", "PRECISIÓN", "DAÑO RECIBIDO", "TIEMPO DE OLEADA", "ARMAS USADAS" };
            for (int i = 0; i < 6; i++)
            {
                var cell = Box(grid, "Celda " + i, new Vector2(-625f + i * 250f, 0f), new Vector2(236f, 146f));
                var ic = Img(cell, "Icono", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(40f, 40f), Line); ic.sprite = Icon(i);
                Label(cell, names[i], 17, FontStyle.Bold, Muted, TextAnchor.MiddleCenter, new Vector2(0f, -14f), new Vector2(230f, 26f));
                var v = Label(cell, "", i == 5 ? 18 : 38, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, -50f), new Vector2(226f, 50f));
                switch (i)
                {
                    case 0: Counter(v, kills, ""); break;
                    case 1: Counter(v, heads, ""); break;
                    case 2: Counter(v, fired <= 0 ? 0 : Mathf.RoundToInt(hit * 100f / fired), " %"); break;
                    case 3: Counter(v, Mathf.RoundToInt(dmg), ""); break;
                    case 4: v.text = StatsReport.Time(wave); break;
                    default: v.text = StatsReport.Weapons(list); v.horizontalOverflow = HorizontalWrapMode.Wrap; break;
                }
            }

            // barras del director (día en curso) + gráfica + medallas
            var mid = Section(rt, "Rendimiento", new Vector2(0.5f, 1f), new Vector2(0f, -610f), new Vector2(1500f, 330f));
            var bars = Box(mid, "Barras", new Vector2(-330f, 0f), new Vector2(830f, 320f));
            Label(bars, "RENDIMIENTO DEL DÍA " + today.day + " · " + Mathf.RoundToInt(today.rawScore) + "/100 (director)", 20, FontStyle.Bold, Amber, TextAnchor.MiddleLeft, new Vector2(0f, 136f), new Vector2(790f, 30f));
            var cats = StatsReport.Bars(today);
            for (int i = 0; i < cats.Length; i++)
            {
                float y = 92f - i * 54f;
                Label(bars, cats[i].Name.ToUpperInvariant(), 17, FontStyle.Bold, Muted, TextAnchor.MiddleLeft, new Vector2(-280f, y + 8f), new Vector2(220f, 24f));
                Img(bars, "Fondo barra", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, y + 8f), new Vector2(500f, 14f), new Color(1f, 1f, 1f, 0.08f));
                var fill = Img(bars, "Barra", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-170f, y + 8f), new Vector2(5f * cats[i].Value, 14f), cats[i].Value >= 60f ? Olive : (cats[i].Value >= 35f ? Amber : Red));
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                var val = Label(bars, "", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleRight, new Vector2(370f, y + 8f), new Vector2(60f, 24f)); Counter(val, Mathf.RoundToInt(cats[i].Value), "");
                Label(bars, cats[i].Explain, 15, FontStyle.Normal, Muted, TextAnchor.MiddleLeft, new Vector2(80f, y - 12f), new Vector2(500f, 22f));
            }
            var chart = Box(mid, "Gráfica", new Vector2(430f, 70f), new Vector2(600f, 180f));
            Label(chart, "BAJAS (barras) Y PRECISIÓN (línea) POR DÍA", 15, FontStyle.Bold, Muted, TextAnchor.MiddleLeft, new Vector2(0f, 74f), new Vector2(570f, 22f));
            var cimg = Img(chart, "Curva", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(560f, 130f), Color.white);
            var ctex = Chart(list, 280, 64); cimg.sprite = Sprite.Create(ctex, new Rect(0, 0, ctex.width, ctex.height), Vector2.one * 0.5f);
            var medals = StatsReport.Medals(list); MedalCount = medals.Count;
            var mbox = Box(mid, "Medallas", new Vector2(430f, -110f), new Vector2(600f, 120f));
            Label(mbox, "MEJOR RACHA: " + StatsReport.BestStreak(list) + "   ·   INSIGNIAS", 15, FontStyle.Bold, Muted, TextAnchor.MiddleLeft, new Vector2(0f, 44f), new Vector2(570f, 22f));
            if (medals.Count == 0) Label(mbox, "Ninguna esta vez", 18, FontStyle.Italic, Muted, TextAnchor.MiddleCenter, new Vector2(0f, -8f), new Vector2(560f, 40f));
            for (int i = 0; i < medals.Count && i < 5; i++)
            {
                var m = Img(mbox, "Insignia " + medals[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230f + i * 115f, -2f), new Vector2(54f, 54f), Color.white); m.sprite = Badge(medals[i]);
                Label(mbox, StatsReport.MedalName(medals[i]), 13, FontStyle.Bold, Line, TextAnchor.MiddleCenter, new Vector2(-230f + i * 115f, -42f), new Vector2(112f, 20f));
            }

            // tabla por día (se abre con "Ver estadísticas") y explicación del director para mañana
            var rows = StatsReport.DayTable(list); Rows = rows.Count - 1;
            _tablePanel = Box(rt, "Tabla", new Vector2(0f, -40f), new Vector2(1100f, 460f)).gameObject;
            Label(_tablePanel.transform, "RESUMEN POR DÍA", 22, FontStyle.Bold, Amber, TextAnchor.MiddleLeft, new Vector2(0f, 200f), new Vector2(1040f, 30f));
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Length; c++)
                    Label(_tablePanel.transform, rows[r][c], r == 0 ? 17 : 19, r == 0 ? FontStyle.Bold : FontStyle.Normal, r == 0 ? Muted : Color.white, TextAnchor.MiddleLeft, new Vector2(-470f + c * 150f, 160f - r * 34f), new Vector2(150f, 30f));
            if (reasons != null && reasons.Count > 0) Label(_tablePanel.transform, "Director: " + string.Join(" · ", reasons), 15, FontStyle.Normal, Muted, TextAnchor.UpperLeft, new Vector2(0f, -190f), new Vector2(1040f, 60f)).horizontalOverflow = HorizontalWrapMode.Wrap;
            _tablePanel.SetActive(false);

            // botones grandes
            var btns = Section(rt, "Botones", new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1500f, 100f));
            Button(btns, dead ? "REINTENTAR DÍA" : "JUGAR OTRA VEZ", new Vector2(-480f, 0f), dead ? Red : Olive, () => { Hide(); if (dead) SevenDays.World.DayLoop.RetryDay(); else SevenDays.World.DayLoop.Retry(); });
            Button(btns, "MENÚ", new Vector2(0f, 0f), new Color(0.3f, 0.3f, 0.3f), () => { Hide(); SevenDays.World.DayLoop.ToMenu(); });
            Button(btns, "VER ESTADÍSTICAS", new Vector2(480f, 0f), new Color(0.32f, 0.3f, 0.22f), () => _tablePanel.SetActive(!_tablePanel.activeSelf));
        }

        private void BuildCompact(RectTransform rt, int day, DayStats d)
        {
            var p = Section(rt, "Resumen del día", new Vector2(1f, 1f), new Vector2(-340f, -320f), new Vector2(620f, 380f));
            var box = Box(p, "Panel", Vector2.zero, new Vector2(620f, 380f));
            Label(box, "DÍA " + day + " SUPERADO", 40, FontStyle.Bold, Olive, TextAnchor.MiddleLeft, new Vector2(0f, 150f), new Vector2(580f, 50f)).rectTransform.localScale = new Vector3(0.86f, 1f, 1f);
            Img(box, "Línea", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 118f), new Vector2(580f, 2f), Line);
            string[] n = { "BAJAS", "CABEZA", "PRECISIÓN" }; int[] v = { d.kills, d.headshotKills, Mathf.RoundToInt(d.Accuracy * 100f) };
            for (int i = 0; i < 3; i++)
            {
                Label(box, n[i], 15, FontStyle.Bold, Muted, TextAnchor.MiddleCenter, new Vector2(-190f + i * 190f, 88f), new Vector2(180f, 22f));
                Counter(Label(box, "", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, new Vector2(-190f + i * 190f, 54f), new Vector2(180f, 44f)), v[i], i == 2 ? " %" : "");
            }
            var cats = StatsReport.Bars(d);
            for (int i = 0; i < cats.Length; i++)
            {
                float y = 4f - i * 34f;
                Label(box, cats[i].Name, 14, FontStyle.Bold, Muted, TextAnchor.MiddleLeft, new Vector2(-200f, y), new Vector2(180f, 20f));
                var f = Img(box, "Barra", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-100f, y), new Vector2(3.4f * cats[i].Value, 10f), cats[i].Value >= 60f ? Olive : (cats[i].Value >= 35f ? Amber : Red));
                f.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
        }

        // ---------------------------------------------------------------- animación (sin memoria por frame)
        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            float t = Time.unscaledTime - _shownAt;
            for (int i = 0; i < _sections.Count; i++) { float a = Mathf.Clamp01((t - i * Stagger) / FadeIn); if (_sections[i].alpha != a) _sections[i].alpha = a; }
            float ct = Mathf.Clamp01((t - 0.2f) / CountSeconds);
            for (int i = 0; i < _counters.Count; i++)
            {
                var c = _counters[i]; int v = StatsReport.CountUp(c.target, ct);
                if (v == c.shown) continue;
                c.shown = v; c.text.text = c.suffix.Length == 0 ? Int(v) : PercentText(v); _counters[i] = c;
            }
            if (Time.unscaledTime >= _hideAt) _root.SetActive(false);
        }

        private static readonly string[] Percents = BuildPercents();
        private static string[] BuildPercents() { var a = new string[101]; for (int i = 0; i <= 100; i++) a[i] = i + " %"; return a; }
        private static string PercentText(int v) => v >= 0 && v <= 100 ? Percents[v] : v + " %";

        private void Counter(Text t, int target, string suffix) { t.text = suffix.Length == 0 ? "0" : "0 %"; _counters.Add((t, target, 0, suffix)); }

        // ---------------------------------------------------------------- piezas de interfaz
        private RectTransform Section(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
            var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; _sections.Add(cg);
            return r;
        }

        private RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var img = Img(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, Ink);
            var r = img.rectTransform;
            // esquinas marcadas (estilo táctico)
            foreach (var (ax, ay) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
            {
                var c = Img(r, "Esquina", new Vector2(ax, ay), new Vector2(ax, ay), new Vector2(ax == 0 ? 9f : -9f, ay == 0 ? 2f : -2f), new Vector2(18f, 3f), Line);
                var c2 = Img(r, "Esquina", new Vector2(ax, ay), new Vector2(ax, ay), new Vector2(ax == 0 ? 2f : -2f, ay == 0 ? 9f : -9f), new Vector2(3f, 18f), Line);
            }
            return r;
        }

        private Image Img(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.anchoredPosition = pos; r.sizeDelta = size;
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        private Text Label(Transform parent, string text, int size, FontStyle style, Color color, TextAnchor anchor, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Texto", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = pos; r.sizeDelta = box;
            var t = go.AddComponent<Text>(); t.font = _font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            go.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            return t;
        }

        private void Button(Transform parent, string text, Vector2 pos, Color c, UnityEngine.Events.UnityAction onClick)
        {
            var img = Img(parent, "Botón " + text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(440f, 86f), c);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(onClick);
            var cols = b.colors; cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f); cols.pressedColor = new Color(0.8f, 0.8f, 0.8f); b.colors = cols;
            Label(img.transform, text, 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(430f, 80f)).rectTransform.localScale = new Vector3(0.88f, 1f, 1f);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ---------------------------------------------------------------- texturas por código
        private Texture2D Tex(int w, int h, string name) { var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp }; _owned.Add(t); return t; }

        private Texture2D Vignette(int n)
        {
            var t = Tex(n, n, "Viñeta"); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f; float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.9f; px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0f, 1f, d) * 255)); }
            t.SetPixels32(px); t.Apply(false); return t;
        }

        /// <summary>Iconos sencillos: calavera, mira, diana, gota, reloj, bala.</summary>
        private Sprite Icon(int kind)
        {
            const int n = 48; var t = Tex(n, n, "Icono " + kind); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(u * u + v * v); bool on;
                switch (kind)
                {
                    case 0: on = (r < 0.62f && !(Mathf.Abs(u) > 0.12f && Mathf.Abs(u) < 0.42f && v > -0.05f && v < 0.25f)) || (Mathf.Abs(u) < 0.36f && v < -0.5f && v > -0.85f && (Mathf.RoundToInt((u + 1f) * 6f) % 2 == 0)); break;
                    case 1: on = Mathf.Abs(r - 0.7f) < 0.09f || (Mathf.Abs(u) < 0.06f && Mathf.Abs(v) > 0.25f) || (Mathf.Abs(v) < 0.06f && Mathf.Abs(u) > 0.25f); break;
                    case 2: on = Mathf.Abs(r - 0.8f) < 0.08f || Mathf.Abs(r - 0.45f) < 0.08f || r < 0.14f; break;
                    case 3: on = (r < 0.5f && v < 0.15f) || (v >= 0.15f && Mathf.Abs(u) < 0.5f * (1f - (v - 0.15f) / 0.8f)); break;
                    case 4: on = Mathf.Abs(r - 0.75f) < 0.09f || (Mathf.Abs(u) < 0.06f && v > 0f && v < 0.55f) || (Mathf.Abs(v) < 0.06f && u > 0f && u < 0.4f); break;
                    default: on = (Mathf.Abs(u) < 0.2f && v < 0.35f && v > -0.85f) || (v >= 0.35f && Mathf.Abs(u) < 0.2f * (1f - (v - 0.35f) / 0.55f)); break;
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
            }
            t.SetPixels32(px); t.Apply(false);
            return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * 0.5f);
        }

        /// <summary>Insignia: escudo con borde y un símbolo propio por medalla.</summary>
        private Sprite Badge(Medal m)
        {
            const int n = 64; var t = Tex(n, n, "Insignia " + m); var px = new Color32[n * n];
            Color body = m == Medal.Francotirador ? new Color(0.35f, 0.45f, 0.6f) : m == Medal.SinCobertura ? new Color(0.6f, 0.25f, 0.2f) : m == Medal.Racha ? new Color(0.75f, 0.5f, 0.15f) : m == Medal.Granadero ? new Color(0.4f, 0.48f, 0.25f) : m == Medal.Intocable ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.55f, 0.45f, 0.3f);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float shield = Mathf.Max(Mathf.Abs(u) - 0.8f, v > 0f ? v - 0.85f : Mathf.Abs(u) - 0.8f * (1f + v / 0.95f));   // < 0 dentro
                bool inside = shield < 0f, rim = shield > -0.1f && inside;
                float r = Mathf.Sqrt(u * u + (v - 0.05f) * (v - 0.05f)), a = Mathf.Atan2(v - 0.05f, u);
                bool sym;
                switch (m)
                {
                    case Medal.Francotirador: sym = Mathf.Abs(r - 0.35f) < 0.06f || (Mathf.Abs(u) < 0.04f && r < 0.5f) || (Mathf.Abs(v - 0.05f) < 0.04f && r < 0.5f); break;
                    case Medal.Racha: sym = r < 0.2f + 0.22f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 3f); break;   // estrella
                    case Medal.SinCobertura: sym = Mathf.Abs(u + v * 0.2f) < 0.07f && Mathf.Abs(v) < 0.5f || (Mathf.Abs(v - 0.3f) < 0.06f && Mathf.Abs(u) < 0.3f); break;
                    case Medal.Granadero: sym = r < 0.3f || (Mathf.Abs(u) < 0.08f && v > 0.3f && v < 0.55f); break;
                    case Medal.Intocable: sym = Mathf.Abs(r - 0.32f) < 0.07f; break;
                    default: sym = Mathf.Abs(u) < 0.06f && Mathf.Abs(v) < 0.45f || Mathf.Abs(v - 0.15f) < 0.06f && Mathf.Abs(u) < 0.3f; break;
                }
                Color c = !inside ? new Color(0, 0, 0, 0) : rim ? new Color(0.9f, 0.82f, 0.55f, 1f) : sym ? new Color(0.95f, 0.92f, 0.82f, 1f) : body;
                px[y * n + x] = c;
            }
            t.SetPixels32(px); t.Apply(false);
            return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * 0.5f);
        }

        /// <summary>Gráfica pequeña: barras de bajas por día y línea de precisión.</summary>
        private Texture2D Chart(List<DayStats> days, int w, int h)
        {
            var t = Tex(w, h, "Gráfica"); var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
            for (int x = 0; x < w; x++) px[x] = new Color32(200, 190, 160, 160);
            int n = Mathf.Max(1, days.Count), max = 1; foreach (var d in days) max = Mathf.Max(max, d.kills);
            float bw = w / (float)DayPlans.Days;
            int prevY = -1, prevX = -1;
            for (int i = 0; i < days.Count; i++)
            {
                int bh = Mathf.RoundToInt((h - 6) * days[i].kills / (float)max);
                for (int x = Mathf.RoundToInt(i * bw + 3); x < Mathf.RoundToInt((i + 1) * bw - 3) && x < w; x++)
                    for (int y = 1; y <= bh && y < h; y++) px[y * w + x] = days[i].died ? new Color32(200, 60, 45, 230) : new Color32(150, 170, 100, 230);
                int cx = Mathf.RoundToInt((i + 0.5f) * bw), cy = Mathf.Clamp(Mathf.RoundToInt(days[i].Accuracy * (h - 4)) + 2, 1, h - 2);
                if (prevX >= 0) for (int s = 0; s <= 40; s++) { int lx = Mathf.RoundToInt(Mathf.Lerp(prevX, cx, s / 40f)), ly = Mathf.RoundToInt(Mathf.Lerp(prevY, cy, s / 40f)); for (int dy = -1; dy <= 1; dy++) if (ly + dy > 0 && ly + dy < h) px[(ly + dy) * w + lx] = new Color32(255, 190, 70, 255); }
                prevX = cx; prevY = cy;
            }
            t.SetPixels32(px); t.Apply(false); return t;
        }

        private void PlayThump(StatsScreenMode mode)
        {
            if (_audio == null) { _audio = gameObject.AddComponent<AudioSource>(); _audio.spatialBlend = 0f; _audio.playOnAwake = false; _audio.ignoreListenerPause = true; }
            const int rate = 44100; int n = (int)(rate * 1.1f);
            var clip = AudioClip.Create("Golpe estadísticas", n, 1, rate, false); _owned.Add(clip);
            var data = new float[n]; var rng = new System.Random(3);
            float f0 = mode == StatsScreenMode.Defeat ? 62f : 98f;
            for (int i = 0; i < n; i++) { float t = i / (float)rate; float env = Mathf.Exp(-t * 4.2f); data[i] = env * (0.55f * Mathf.Sin(2f * Mathf.PI * f0 * t * (1f - 0.25f * t)) + 0.05f * (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 18f)); }
            clip.SetData(data, 0);
            _audio.PlayOneShot(clip, mode == StatsScreenMode.DaySummary ? 0.35f : 0.7f);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// Vista uGUI del selector de armas (diseño propio): barra compacta abajo a la derecha con el icono de cada arma y su número de tecla; la activa se agranda, se
    /// resalta con el color de su rareza y un leve brillo, las demás quedan semitransparentes y las vacías con un marco tenue. Debajo de la activa, solo la
    /// munición (cargador / reserva) y el modo de disparo; el nombre aparece 1.5 s al cambiar. La rueda radial (Tab) muestra los iconos grandes en círculo.
    /// Canvas con Scale With Screen Size (referencia 1920x1080): resolución independiente. Sin alocaciones por frame: los textos solo se reasignan si cambian y
    /// todo lo demás son colores y rectángulos. No sabe nada del juego: lo alimenta WeaponHud (o las pruebas).
    /// </summary>
    public class WeaponHudView : MonoBehaviour
    {
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        public const float MarginRight = 48f, BarBottom = 150f, WheelRadius = 250f;

        public WeaponBarState Bar { get; } = new WeaponBarState();
        public Canvas Canvas { get; private set; }
        public RectTransform BarRoot { get; private set; }

        private sealed class Slot
        {
            public RectTransform Rect, IconRect;
            public Image Back, Glow, Stripe;
            public RawImage Icon;
            public Text Number;
        }

        private sealed class WheelSlot
        {
            public RectTransform Rect;
            public Image Back;
            public RawImage Icon;
            public Text Number;
        }

        private readonly Slot[] _slots = new Slot[WeaponBarState.Slots];
        private readonly WheelSlot[] _wheel = new WheelSlot[WeaponBarState.Slots];
        private readonly SlotRect[] _rects = new SlotRect[WeaponBarState.Slots];
        private readonly Color[] _rarity = new Color[WeaponBarState.Slots];
        private readonly bool[] _occupied = new bool[WeaponBarState.Slots];
        private Text _ammo, _mode, _name, _wheelName, _wheelAmmo;
        private RectTransform _ammoRect, _modeRect;
        private CanvasGroup _wheelGroup;
        private Image _dim;
        private Font _font;
        private string _ammoShown, _modeShown, _nameShown, _wheelNameShown, _wheelAmmoShown;
        private int _wheelSelected = -1;
        private float _wheelAlpha;

        public static WeaponHudView Create(Transform parent = null)
        {
            var go = new GameObject("HUD de armas");
            if (parent != null) go.transform.SetParent(parent, false);
            var v = go.AddComponent<WeaponHudView>();
            v.Build();
            return v;
        }

        // ------------------------------------------------------------------ construcción
        private void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // como el resto del HUD: manda la altura

            BarRoot = Rect("Barra", transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            BarRoot.anchoredPosition = new Vector2(-MarginRight, BarBottom);
            for (int i = 0; i < _slots.Length; i++) _slots[i] = BuildSlot(i);

            _name = Label("Nombre", BarRoot, 30, FontStyle.Bold, TextAnchor.LowerRight);
            var nr = _name.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(1f, 0f); nr.pivot = new Vector2(1f, 0f);
            nr.sizeDelta = new Vector2(560f, 40f);
            _ammo = Label("Munición", transform, 46, FontStyle.Bold, TextAnchor.UpperCenter);
            _ammoRect = _ammo.rectTransform; _ammoRect.anchorMin = _ammoRect.anchorMax = new Vector2(1f, 0f); _ammoRect.pivot = new Vector2(0.5f, 1f); _ammoRect.sizeDelta = new Vector2(260f, 56f);
            _mode = Label("Modo", transform, 22, FontStyle.Normal, TextAnchor.UpperCenter);
            _modeRect = _mode.rectTransform; _modeRect.anchorMin = _modeRect.anchorMax = new Vector2(1f, 0f); _modeRect.pivot = new Vector2(0.5f, 1f); _modeRect.sizeDelta = new Vector2(260f, 30f);

            BuildWheel();
            ApplyEmpty();
        }

        private Slot BuildSlot(int i)
        {
            var s = new Slot();
            s.Rect = Rect("Ranura " + (i + 1), BarRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            s.Back = Img("Fondo", s.Rect, new Color(0f, 0f, 0f, 0.38f));
            s.Glow = Img("Brillo", s.Rect, new Color(1f, 1f, 1f, 0f));
            s.Stripe = Img("Franja", s.Rect, Color.white);
            var sr = s.Stripe.rectTransform; sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(0.5f, 0f); sr.sizeDelta = new Vector2(0f, 4f);
            var go = new GameObject("Icono", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(s.Rect, false);
            s.Icon = go.GetComponent<RawImage>(); s.Icon.raycastTarget = false;
            s.IconRect = s.Icon.rectTransform; Stretch(s.IconRect, 4f);
            s.Number = Label("Número", s.Rect, 20, FontStyle.Bold, TextAnchor.UpperLeft);
            Stretch(s.Number.rectTransform, 6f);
            s.Number.text = (i + 1).ToString();
            return s;
        }

        private void BuildWheel()
        {
            var root = Rect("Rueda", transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = root.offsetMax = Vector2.zero;
            _wheelGroup = root.gameObject.AddComponent<CanvasGroup>();
            _wheelGroup.alpha = 0f; _wheelGroup.blocksRaycasts = false; _wheelGroup.interactable = false;
            _dim = Img("Oscurecer", root, new Color(0f, 0f, 0f, 0.45f));
            Stretch(_dim.rectTransform, 0f);
            for (int i = 0; i < _wheel.Length; i++)
            {
                var w = new WheelSlot();
                w.Rect = Rect("Sector " + (i + 1), root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                w.Rect.sizeDelta = new Vector2(220f, 110f);
                w.Back = Img("Fondo", w.Rect, new Color(0f, 0f, 0f, 0.5f));
                var go = new GameObject("Icono", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(w.Rect, false);
                w.Icon = go.GetComponent<RawImage>(); w.Icon.raycastTarget = false; Stretch(w.Icon.rectTransform, 6f);
                w.Number = Label("Número", w.Rect, 24, FontStyle.Bold, TextAnchor.UpperLeft);
                Stretch(w.Number.rectTransform, 8f);
                w.Number.text = (i + 1).ToString();
                _wheel[i] = w;
            }
            _wheelName = Label("Nombre", root, 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            _wheelName.rectTransform.sizeDelta = new Vector2(500f, 56f); _wheelName.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            _wheelAmmo = Label("Munición", root, 30, FontStyle.Normal, TextAnchor.MiddleCenter);
            _wheelAmmo.rectTransform.sizeDelta = new Vector2(500f, 44f); _wheelAmmo.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ datos
        /// <summary>Pone los iconos y colores de rareza de las ranuras (cuando cambia el inventario).</summary>
        public void SetSlots(System.Collections.Generic.IList<WeaponData> weapons)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                bool has = weapons != null && i < weapons.Count && weapons[i] != null;
                _occupied[i] = has;
                Texture2D icon = has ? WeaponIconCache.Get(weapons[i]) : WeaponIconCache.Empty;
                _slots[i].Icon.texture = icon; _wheel[i].Icon.texture = icon;
                if (has) { var c = WeaponCatalog.RarityColor(weapons[i].rarity); _rarity[i] = new Color(c[0], c[1], c[2]); }
                else _rarity[i] = Color.white;
                _wheel[i].Rect.gameObject.SetActive(has);
            }
        }

        public Texture IconOf(int slot) => _slots[slot].Icon.texture;
        public RectTransform SlotRect(int slot) => _slots[slot].Rect;
        public Color RarityOf(int slot) => _rarity[slot];
        public float GlowAlpha(int slot) => _slots[slot].Glow.color.a;
        public float IconAlpha(int slot) => _slots[slot].Icon.color.a;
        public string AmmoText => _ammo.text;
        public string ModeText => _mode.text;
        public string NameText => _name.text;
        public float NameAlpha => _name.color.a;
        public bool WheelVisible => _wheelGroup.gameObject.activeSelf && _wheelGroup.alpha > 0.01f;
        public int WheelSelected => _wheelSelected;

        private void ApplyEmpty()
        {
            SetSlots(null);
            SetTexts("", "", "");
        }

        /// <summary>Munición y modo debajo de la activa, y el nombre del arma (que se funde solo). Reasigna el texto solo si cambió.</summary>
        public void SetTexts(string ammo, string mode, string name)
        {
            if (ammo != _ammoShown) { _ammoShown = ammo; _ammo.text = ammo; }
            if (mode != _modeShown) { _modeShown = mode; _mode.text = mode; }
            if (name != _nameShown) { _nameShown = name; _name.text = name; }
        }

        /// <summary>Muestra el nombre del arma activa otra vez (al cambiarla).</summary>
        public void FlashName() => Bar.ShowName();

        // ------------------------------------------------------------------ animación y colocación (cada frame, sin alocaciones)
        public void Tick(float dt)
        {
            Bar.Tick(dt);
            Bar.Layout(_rects);
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i]; var r = _rects[i];
                float hl = Mathf.Clamp01(Bar.Highlight[i]);
                s.Rect.anchoredPosition = new Vector2(-r.Right, 0f);
                s.Rect.sizeDelta = new Vector2(r.Width, r.Height);
                Color rc = _rarity[i];
                bool has = _occupied[i];
                s.Icon.color = new Color(1f, 1f, 1f, r.Alpha);
                // el activo se tiñe con su rareza (algo mezclada con el blanco para que se siga leyendo la silueta)
                if (has && hl > 0.01f) s.Icon.color = new Color(Mathf.Lerp(1f, Mathf.Lerp(1f, rc.r, 0.55f), hl), Mathf.Lerp(1f, Mathf.Lerp(1f, rc.g, 0.55f), hl), Mathf.Lerp(1f, Mathf.Lerp(1f, rc.b, 0.55f), hl), r.Alpha);
                s.Back.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.3f, 0.55f, hl));
                float pulse = 1f + 0.5f * Bar.Pulse * hl;
                s.Glow.color = new Color(rc.r, rc.g, rc.b, has ? hl * 0.28f * pulse : 0f);
                s.Stripe.color = new Color(rc.r, rc.g, rc.b, has ? Mathf.Lerp(0.25f, 1f, hl) * (hl > 0.01f || has ? 1f : 0f) : 0f);
                s.Number.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, hl) * (has ? 1f : 0.45f));
                s.Number.fontSize = hl > 0.5f ? 24 : 20;
            }
            // munición y modo debajo del icono activo
            if (Bar.Count > 0)
            {
                var a = _rects[Bar.Active];
                float cx = -(MarginRight + a.CenterFromRight);
                _ammoRect.anchoredPosition = new Vector2(cx, BarBottom - 8f);
                _modeRect.anchoredPosition = new Vector2(cx, BarBottom - 62f);
            }
            _ammo.color = Color.white;
            _mode.color = new Color(1f, 1f, 1f, 0.75f);
            Color nc = Bar.Count > 0 ? _rarity[Bar.Active] : Color.white;
            _name.color = new Color(nc.r, nc.g, nc.b, Bar.NameAlpha);
            _name.rectTransform.anchoredPosition = new Vector2(0f, _rects[Bar.Active].Height + 14f);   // sobre la ranura activa, alineado a la derecha de la barra

            // rueda radial: se funde al abrir y cerrar
            float target = _wheelOpen ? 1f : 0f;
            _wheelAlpha = Mathf.MoveTowards(_wheelAlpha, target, dt * 7f);
            _wheelGroup.alpha = _wheelAlpha;
            bool show = _wheelAlpha > 0.005f;
            if (_wheelGroup.gameObject.activeSelf != show) _wheelGroup.gameObject.SetActive(show);
            if (show) LayoutWheel(dt);
        }

        private bool _wheelOpen;
        private readonly float[] _wheelScale = new float[WeaponBarState.Slots];

        /// <summary>Abre o cierra la rueda. "selected" = sector resaltado (-1 ninguno).</summary>
        public void SetWheel(bool open, int selected, int count)
        {
            _wheelOpen = open;
            _wheelSelected = selected;
            _wheelCount = count;
        }

        private int _wheelCount;

        public void SetWheelTexts(string name, string ammo)
        {
            if (name != _wheelNameShown) { _wheelNameShown = name; _wheelName.text = name; }
            if (ammo != _wheelAmmoShown) { _wheelAmmoShown = ammo; _wheelAmmo.text = ammo; }
        }

        private void LayoutWheel(float dt)
        {
            for (int i = 0; i < _wheel.Length; i++)
            {
                var w = _wheel[i];
                if (i >= _wheelCount) continue;
                var c = RadialWheel.SlotCenter(i, _wheelCount, WheelRadius);
                bool sel = i == _wheelSelected, cur = i == Bar.Active;
                _wheelScale[i] = Mathf.MoveTowards(_wheelScale[i], sel ? 1.35f : 1f, dt * 6f);
                w.Rect.anchoredPosition = new Vector2(c.X, c.Y);
                w.Rect.localScale = new Vector3(_wheelScale[i], _wheelScale[i], 1f);
                Color rc = _rarity[i];
                w.Back.color = sel ? new Color(rc.r, rc.g, rc.b, 0.55f) : new Color(0f, 0f, 0f, cur ? 0.7f : 0.5f);
                w.Icon.color = new Color(1f, 1f, 1f, sel || cur ? 1f : 0.7f);
            }
        }

        // ------------------------------------------------------------------ utilidades de construcción
        private static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot;
            return r;
        }

        private static Image Img(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
            Stretch(i.rectTransform, 0f);
            return i;
        }

        private Text Label(string name, Transform parent, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style; t.alignment = anchor; t.color = Color.white;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.8f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }
    }
}

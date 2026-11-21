using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using SevenDays.Procedural;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Sistema de UI común (estilo militar táctico oscuro, propio): colores, tipografía "condensada" (fuente del sistema estrechada), marcos desgastados en 9-slice,
    /// cabeceras angulosas con marcas de stencil, filas con resaltado rojo/naranja, pie con ayudas de teclas, Canvas Scale With Screen Size (1920x1080).
    /// Las texturas se generan una vez (MenuArt) y se reutilizan; no se pierden al recargar la escena.
    /// </summary>
    public static class UiKit
    {
        // paleta del menú: negro carbón #0A0808, rojo #8E1B12 (Maroon) y #B3261A (Red), naranja fuego #FF7A1F (Orange) y #FFB347 (Amber), crema #D9CDB4, gris acero #3A3D40 (Grey)
        public static readonly Color Cream = new Color(0.851f, 0.804f, 0.706f), CreamDim = new Color(0.6f, 0.565f, 0.5f), Red = new Color(0.702f, 0.149f, 0.102f),
            Orange = new Color(1f, 0.478f, 0.122f), Amber = new Color(1f, 0.702f, 0.278f), Ink = new Color(0.039f, 0.031f, 0.031f, 0.75f), Olive = new Color(0.55f, 0.66f, 0.38f),
            White = new Color(0.94f, 0.925f, 0.9f), Grey = new Color(0.227f, 0.239f, 0.251f), Maroon = new Color(0.557f, 0.106f, 0.071f), Charcoal = new Color(0.039f, 0.031f, 0.031f);
        public const float Condensed = 0.84f;
        public static readonly Vector2 Reference = new Vector2(1920, 1080);

        private static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        private static Texture2D _backdrop, _frame, _highlight, _grain, _smoke;
        private static Sprite _frameSprite, _highlightSprite;
        public static Texture2D Backdrop => _backdrop != null ? _backdrop : (_backdrop = Make(1024, 576, MenuArt.Backdrop(1024, 576, 7), "Fondo menú", TextureWrapMode.Clamp));
        public static Texture2D GrainTex => _grain != null ? _grain : (_grain = Make(128, 128, MenuArt.Grain(128, 3), "Grano", TextureWrapMode.Repeat));
        private static readonly Texture2D[] _flames = new Texture2D[2];
        /// <summary>Atlas de llamas del plano 0 (grandes, al fondo) o 1 (medias): distinta semilla, otras lenguas.</summary>
        public static Texture2D FlameTex(int plane) => _flames[plane] != null ? _flames[plane] : (_flames[plane] = Make(SevenDays.Procedural.MenuArt.FlameW * SevenDays.Procedural.MenuArt.FlameCols,
            SevenDays.Procedural.MenuArt.FlameH * (SevenDays.Procedural.MenuArt.FlameFrames / SevenDays.Procedural.MenuArt.FlameCols), SevenDays.Procedural.MenuArt.FlameAtlas(plane == 0 ? 3 : 8), "Llamas " + plane, TextureWrapMode.Clamp));
        private static Material _additive;
        /// <summary>Material aditivo para las llamas (shader en Resources/Look); si no se encuentra, null = mezcla normal.</summary>
        public static Material Additive { get { if (_additive == null) { var sh = Resources.Load<Shader>("Look/UIAdditive"); if (sh != null) _additive = new Material(sh) { name = "UI aditivo", hideFlags = HideFlags.DontSave }; } return _additive; } }
        public static Texture2D SmokeTex => _smoke != null ? _smoke : (_smoke = Make(256, 128, MenuArt.Smoke(256, 128, 5), "Humo", TextureWrapMode.Repeat));
        public static Sprite FrameSprite { get { if (_frameSprite == null) { _frame = Make(64, 64, MenuArt.Frame(64, 12, 2), "Marco", TextureWrapMode.Clamp); _frameSprite = Sprite.Create(_frame, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14)); } return _frameSprite; } }
        public static Sprite HighlightSprite { get { if (_highlightSprite == null) { _highlight = Make(128, 16, MenuArt.Highlight(128, 16, 4), "Resaltado", TextureWrapMode.Clamp); _highlightSprite = Sprite.Create(_highlight, new Rect(0, 0, 128, 16), Vector2.one * 0.5f); } return _highlightSprite; } }

        public static Texture2D Make(int w, int h, byte[] rgba, string name, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            t.SetPixelData(rgba, 0); t.Apply(false, true);
            return t;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Canvas(Transform parent, string name, int order)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = Reference; s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        public static Image Image(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c, Sprite s = null, bool sliced = false)
        {
            var r = Rect(parent, name, anchor, pos, size);
            var img = r.gameObject.AddComponent<Image>(); img.color = c; img.sprite = s; img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        /// <summary>Panel oscuro semitransparente con marco fino desgastado.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
            => Image(parent, name, anchor, pos, size, Color.white, FrameSprite, true).rectTransform;

        public static Text Text(Transform parent, string text, int size, Color c, TextAnchor align, Vector2 pos, Vector2 box, bool bold = false, bool condensed = true)
        {
            var r = Rect(parent, "Texto", new Vector2(0.5f, 0.5f), pos, box);
            var t = r.gameObject.AddComponent<Text>(); t.font = Font; t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (condensed) r.localScale = new Vector3(Condensed, 1f, 1f);
            r.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            return t;
        }

        /// <summary>Cabecera angulosa con marcas de stencil: banda oscura con corte en diagonal, barra roja y "///" a la izquierda.</summary>
        public static RectTransform Header(Transform parent, string title, Vector2 pos, float width)
        {
            var r = Rect(parent, "Cabecera " + title, new Vector2(0.5f, 0.5f), pos, new Vector2(width, 44f));
            Image(r, "Banda", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 44f), new Color(0.02f, 0.02f, 0.02f, 0.85f));
            var cut = Image(r, "Corte", new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(40f, 60f), new Color(0.02f, 0.02f, 0.02f, 0.85f)); cut.rectTransform.localRotation = Quaternion.Euler(0, 0, 35f);
            Image(r, "Barra", new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(8f, 44f), Red);
            Text(r, "///", 26, Red, TextAnchor.MiddleLeft, new Vector2(-width / 2f + 48f, 0f), new Vector2(60f, 40f), true);
            Text(r, SevenDays.Procedural.SelectContent.Tracked(title.ToUpperInvariant()), 28, Cream, TextAnchor.MiddleLeft, new Vector2(-width / 2f + 90f + (width - 120f) / 2f * Condensed, 0f), new Vector2(width - 120f, 40f), true);   // borde izquierdo visual en -w/2+90 tras condensar
            return r;
        }

        /// <summary>Pie con ayudas de teclas.</summary>
        public static Text Footer(Transform parent, string hints) => FooterKeys(parent, KeyHints(hints), 24);

        /// <summary>"↑↓ Elegir · Enter Aceptar" -> la tecla en blanco y entre corchetes, la acción en crema apagado.</summary>
        public static string KeyHints(string hints)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var raw in hints.Split('·'))
            {
                var seg = raw.Trim(); if (seg.Length == 0) continue;
                int sp = seg.IndexOf(' ');
                string key = sp > 0 ? seg.Substring(0, sp) : seg, act = sp > 0 ? seg.Substring(sp + 1) : "";
                if (sb.Length > 0) sb.Append("        ");
                sb.Append("<color=#F0ECE6><b>[").Append(key).Append("]</b></color> <color=#9A938A>").Append(act).Append("</color>");
            }
            return sb.ToString();
        }

        /// <summary>Pie grande y centrado con teclas resaltadas ("[Enter] Empezar"): la tecla en naranja y la acción en crema.</summary>
        public static Text FooterKeys(Transform parent, string hints, int size)
        {
            var bar = Image(parent, "Pie", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1920f, 64f), new Color(0f, 0f, 0f, 0.78f));
            Image(bar.transform, "Filo", new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1920f, 2f), new Color(Red.r, Red.g, Red.b, 0.8f));
            var t = Text(bar.transform, hints, size, Cream, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1800f / Condensed, 50f));
            t.supportRichText = true;
            return t;
        }
    }
}

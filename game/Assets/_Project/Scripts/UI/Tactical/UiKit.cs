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
        public static readonly Color Cream = new Color(0.9f, 0.84f, 0.7f), CreamDim = new Color(0.72f, 0.66f, 0.55f), Red = new Color(0.86f, 0.18f, 0.12f),
            Orange = new Color(1f, 0.55f, 0.15f), Ink = new Color(0.04f, 0.035f, 0.03f, 0.82f), Olive = new Color(0.55f, 0.66f, 0.38f);
        public const float Condensed = 0.84f;
        public static readonly Vector2 Reference = new Vector2(1920, 1080);

        private static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        private static Texture2D _backdrop, _frame, _highlight, _grain, _smoke;
        private static Sprite _frameSprite, _highlightSprite;
        public static Texture2D Backdrop => _backdrop != null ? _backdrop : (_backdrop = Make(1024, 576, MenuArt.Backdrop(1024, 576, 7), "Fondo menú", TextureWrapMode.Clamp));
        public static Texture2D GrainTex => _grain != null ? _grain : (_grain = Make(128, 128, MenuArt.Grain(128, 3), "Grano", TextureWrapMode.Repeat));
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
            Text(r, title.ToUpperInvariant(), 28, Cream, TextAnchor.MiddleLeft, new Vector2(-width / 2f + 90f + (width - 120f) / 2f * Condensed, 0f), new Vector2(width - 120f, 40f), true);   // borde izquierdo visual en -w/2+90 tras condensar
            return r;
        }

        /// <summary>Pie con ayudas de teclas.</summary>
        public static Text Footer(Transform parent, string hints)
        {
            var bar = Image(parent, "Pie", new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1920f, 56f), new Color(0f, 0f, 0f, 0.7f));
            return Text(bar.transform, hints, 22, CreamDim, TextAnchor.MiddleLeft, new Vector2(-200f, 0f), new Vector2(1500f, 40f));
        }
    }
}

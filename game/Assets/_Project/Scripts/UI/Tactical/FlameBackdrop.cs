using UnityEngine;
using UnityEngine.UI;
using SevenDays.Procedural;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Fondo compartido del menú y de las pantallas, por capas (de atrás adelante): cielo en degradado con humo, humo denso que se mueve despacio, ciudad lejana,
    /// columnas de humo negro, ciudad media con 25 ventanas que parpadean, calavera roja, llamas grandes y medias, ciudad cercana (las llamas quedan detrás),
    /// brasas que suben, humo sobre el fuego, resplandor naranja desde abajo y grano del 4 %. Paralaje suave de las tres ciudades. Sin memoria por frame.
    /// </summary>
    public class FlameBackdrop : MonoBehaviour
    {
        public const int Sparks = 40, Windows = 25, Columns = 3, FlameLayers = 2, Tiles = 5;
        public const float FlameFps = 24f, CityWidth = 2040f;
        public static readonly float[] CityHeight = { 560f, 440f, 210f }, ParallaxPx = { 8f, 16f, 28f };
        public static readonly Color WindowColor = new Color(1f, 0x8A / 255f, 0x2A / 255f);
        private RawImage _smokeA, _smokeB, _smokeTop, _grain;
        private RawImage[] _columns, _flames;
        private readonly RectTransform[] _city = new RectTransform[3];
        private Image[] _windows;
        private RawImage _flicker, _glow;
        private RectTransform[] _spark;
        private float[] _sparkSpeed;
        public SkullBackdrop Skull { get; private set; }

        private static readonly Texture2D[] _cityTex = new Texture2D[3];
        private static Texture2D _columnTex;
        private static float[] _windowUv;
        private static Texture2D CityTex(int layer)
        {
            if (_cityTex[layer] == null) _cityTex[layer] = UiKit.Make(1024, 256, MenuArt.CityLayer(1024, 256, layer, 21), "Ciudad " + layer, TextureWrapMode.Clamp);
            return _cityTex[layer];
        }

        public static FlameBackdrop Create(Transform parent, Color tint)
        {
            var root = UiKit.Stretch(parent, "Fondo en llamas");
            var b = root.gameObject.AddComponent<FlameBackdrop>();
            Raw(root, "Cielo", UiKit.Backdrop, tint);
            b._smokeA = Raw(root, "Humo cielo", UiKit.SmokeTex, new Color(0.35f, 0.3f, 0.3f, 0.9f));
            b._smokeB = Raw(root, "Humo cielo 2", UiKit.SmokeTex, new Color(0.2f, 0.16f, 0.16f, 0.8f));
            b._city[0] = b.City(root, 0, tint);
            if (_columnTex == null) _columnTex = UiKit.Make(128, 256, MenuArt.SmokeColumn(128, 256, 4), "Columna humo", TextureWrapMode.Repeat);
            b._columns = new RawImage[Columns];
            float[] cx = { -620f, 120f, 760f };
            for (int i = 0; i < Columns; i++)
            {
                var c = new GameObject("Columna humo " + i, typeof(RectTransform)).AddComponent<RawImage>(); c.transform.SetParent(root, false);
                c.texture = _columnTex; c.raycastTarget = false; c.color = new Color(1f, 1f, 1f, 0.75f);
                var r = c.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(320f + 60f * i, 1000f); r.anchoredPosition = new Vector2(cx[i], 180f);
                b._columns[i] = c;
            }
            b._city[1] = b.City(root, 1, tint);
            // ventanas encendidas en la ciudad media
            _windowUv ??= MenuArt.CityWindows(1024, 21, Windows);
            b._windows = new Image[Windows];
            for (int i = 0; i < Windows; i++)
            {
                var w = UiKit.Image(b._city[1], "Ventana", Vector2.zero, Vector2.zero, new Vector2(7f, 10f), WindowColor);
                w.rectTransform.anchorMin = w.rectTransform.anchorMax = new Vector2(_windowUv[i * 2], _windowUv[i * 2 + 1]);
                b._windows[i] = w;
            }
            b.Skull = SkullBackdrop.Create(root);
            // parpadeo del fuego: resplandor suave abajo, sin bordes (en espacio lineal un velo naranja a pantalla completa aclara todo el cielo)
            b._flicker = Raw(root, "Fuego", UiKit.GlowTex, new Color(1f, 1f, 1f, 0f));
            var fr = b._flicker.rectTransform; fr.anchorMax = new Vector2(1f, 0.55f);
            b.BuildFlames(root);
            b._city[2] = b.City(root, 2, tint);
            b._spark = new RectTransform[Sparks]; b._sparkSpeed = new float[Sparks];
            for (int i = 0; i < Sparks; i++)
            {
                float sz = Random.Range(2f, 6f);
                var s = UiKit.Image(root, "Brasa", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(sz, sz), new Color(1f, Random.Range(0.45f, 0.8f), 0.2f, 0.95f));
                b._spark[i] = s.rectTransform; b.Respawn(i, Random.value * 1000f);
            }
            b._smokeTop = Raw(root, "Humo sobre el fuego", UiKit.SmokeTex, new Color(0.12f, 0.08f, 0.07f, 0.4f));   // pantalla completa: sin borde recto
            b._glow = Raw(root, "Resplandor", UiKit.GlowTex, new Color(1f, 1f, 1f, 0.2f));
            b._glow.rectTransform.anchorMax = new Vector2(1f, 0.35f);
            b._grain = Raw(root, "Grano", UiKit.GrainTex, Color.white); b._grain.uvRect = new Rect(0, 0, 12f, 7f);
            root.SetAsFirstSibling();
            return b;
        }

        private static RawImage Raw(Transform parent, string name, Texture tex, Color c)
        {
            var r = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>(); r.transform.SetParent(parent, false); Fill(r.rectTransform);
            r.texture = tex; r.color = c; r.raycastTarget = false; return r;
        }

        private RectTransform City(Transform root, int layer, Color tint)
        {
            var c = new GameObject("Ciudad " + layer, typeof(RectTransform)).AddComponent<RawImage>(); c.transform.SetParent(root, false);
            c.texture = CityTex(layer); c.raycastTarget = false; c.color = tint;
            var r = c.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(CityWidth, CityHeight[layer]); r.anchoredPosition = Vector2.zero;
            return r;
        }

        // llamas: dos planos (grandes al fondo, medias delante) de tramos seguidos; el tercer plano son las brasas
        private void BuildFlames(Transform root)
        {
            _flames = new RawImage[FlameLayers * Tiles];
            for (int i = 0; i < FlameLayers; i++)
                for (int k = 0; k < Tiles; k++)
                {
                    var f = new GameObject("Llamas " + i + "." + k, typeof(RectTransform)).AddComponent<RawImage>(); f.transform.SetParent(root, false);
                    var r = f.rectTransform; r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
                    float h = i == 0 ? 430f : 300f, w = h * 2f;
                    r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(k * w - i * 170f, 30f);
                    f.texture = UiKit.FlameTex(i); f.raycastTarget = false; f.material = UiKit.Additive;   // brillo aditivo
                    f.color = i == 0 ? new Color(0.9f, 0.6f, 0.5f, 0.85f) : new Color(1f, 0.95f, 0.9f, 0.9f);
                    _flames[i * Tiles + k] = f;
                }
        }

        private static void Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        private void Respawn(int i, float y)
        {
            _spark[i].anchoredPosition = new Vector2(Random.Range(-960f, 960f), y);
            _sparkSpeed[i] = Random.Range(60f, 220f);
        }

        private void Update()
        {
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            var c = _flicker.color; c.a = 0.25f + 0.2f * Mathf.PerlinNoise(t * 3.1f, 0.3f); _flicker.color = c;
            _smokeA.uvRect = new Rect(t * 0.004f, -t * 0.006f, 1f, 1f);
            _smokeB.uvRect = new Rect(-t * 0.007f + 0.37f, -t * 0.009f + 0.21f, 1.4f, 1.2f);
            _smokeTop.uvRect = new Rect(t * 0.01f, -t * 0.03f, 1.5f, 1f);
            for (int i = 0; i < Columns; i++)
            {
                _columns[i].uvRect = new Rect(0f, -t * (0.02f + 0.008f * i), 1f, 1f);
                _columns[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.2f + i) * 2.5f);
            }
            for (int l = 0; l < 3; l++) _city[l].anchoredPosition = new Vector2(Mathf.Sin(t * 0.07f) * ParallaxPx[l], 0f);
            for (int i = 0; i < Windows; i++) { var w = _windows[i].color; w.a = 0.45f + 0.55f * Mathf.PerlinNoise(t * 0.35f, i * 1.7f); _windows[i].color = w; }
            const int rows = MenuArt.FlameFrames / MenuArt.FlameCols;
            for (int i = 0; i < FlameLayers; i++)
            {
                int fr = ((int)(t * (i == 0 ? FlameFps * 0.75f : FlameFps)) + i * 11) % MenuArt.FlameFrames;   // las grandes, más lentas
                float fw = 1f / MenuArt.FlameCols, fh = 1f / rows;
                const float tx = 1f / (MenuArt.FlameW * MenuArt.FlameCols), ty = 1f / (MenuArt.FlameH * rows);   // medio texel hacia dentro: sin sangrado del fotograma vecino
                var uv = new Rect((fr % MenuArt.FlameCols) * fw + tx, (rows - 1 - fr / MenuArt.FlameCols) * fh + ty, fw - 2f * tx, fh - 2f * ty);
                for (int k = 0; k < Tiles; k++) _flames[i * Tiles + k].uvRect = uv;
            }
            var gc = _glow.color; gc.a = 0.16f + 0.12f * Mathf.PerlinNoise(t * 2.3f, 0.7f); _glow.color = gc;
            _grain.uvRect = new Rect(Random.value, Random.value, 12f, 7f);
            for (int i = 0; i < Sparks; i++)
            {
                var p = _spark[i].anchoredPosition; p.y += _sparkSpeed[i] * dt; p.x += Mathf.Sin(t * 2f + i) * 12f * dt;
                if (p.y > 1100f) Respawn(i, 0f); else _spark[i].anchoredPosition = p;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Fondo compartido del menú y de las pantallas: la textura cacheada (ciudad ardiendo, calavera propia, pintura desgastada) y encima, animados sin memoria
    /// por frame: parpadeo del fuego, humo que sube despacio, grano que salta, 40 chispas recicladas y una pared de llamas animadas abajo
    /// (atlas de 32 fotogramas en bucle, tres capas con distinto tamaño y desfase para que no se note la repetición).
    /// </summary>
    public class FlameBackdrop : MonoBehaviour
    {
        public const int Sparks = 40, FlameLayers = 3, Tiles = 5;
        public const float FlameFps = 24f;
        private RawImage[] _flames;
        private Image _glow;
        private RawImage _smoke, _grain;
        private Image _flicker;
        private RectTransform[] _spark;
        private Image[] _sparkImg;
        private float[] _sparkSpeed;
        private Color _tint = Color.white;

        public static FlameBackdrop Create(Transform parent, Color tint)
        {
            var root = UiKit.Stretch(parent, "Fondo en llamas");
            var b = root.gameObject.AddComponent<FlameBackdrop>(); b._tint = tint;
            var bg = new GameObject("Base", typeof(RectTransform)).AddComponent<RawImage>(); bg.transform.SetParent(root, false); Fill(bg.rectTransform);
            bg.texture = UiKit.Backdrop; bg.color = tint; bg.raycastTarget = false;
            b._flicker = new GameObject("Fuego", typeof(RectTransform)).AddComponent<Image>(); b._flicker.transform.SetParent(root, false); Fill(b._flicker.rectTransform);
            b._flicker.color = new Color(1f, 0.4f, 0.1f, 0.05f); b._flicker.raycastTarget = false;
            b._smoke = new GameObject("Humo", typeof(RectTransform)).AddComponent<RawImage>(); b._smoke.transform.SetParent(root, false); Fill(b._smoke.rectTransform);
            b._smoke.texture = UiKit.SmokeTex; b._smoke.raycastTarget = false;
            // resplandor bajo que late con el fuego y tres capas de llamas (la del fondo más alta y oscura, la de delante más baja y brillante)
            b._glow = UiKit.Image(root, "Resplandor", new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1920f, 400f), new Color(1f, 0.35f, 0.05f, 0.18f));
            b._glow.sprite = UiKit.HighlightSprite; b._glow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f); b._glow.rectTransform.sizeDelta = new Vector2(400f, 1920f);
            // cada capa: 4 tramos seguidos (la llama se repite sin costura en horizontal), desplazada para que las capas no coincidan
            b._flames = new RawImage[FlameLayers * Tiles];
            for (int i = 0; i < FlameLayers; i++)
                for (int k = 0; k < Tiles; k++)
                {
                    var f = new GameObject("Llamas " + i + "." + k, typeof(RectTransform)).AddComponent<RawImage>(); f.transform.SetParent(root, false);
                    var r = f.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0f, 0f); r.pivot = Vector2.zero;
                    float h = i == 0 ? 430f : (i == 1 ? 320f : 220f), w = h * 2f;   // proporción del fotograma (2:1)
                    r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(k * w - i * 170f, -16f);
                    f.texture = UiKit.FlameTex; f.raycastTarget = false;
                    f.color = i == 0 ? new Color(0.7f, 0.4f, 0.3f, 0.7f) : (i == 1 ? new Color(1f, 0.82f, 0.7f, 0.88f) : Color.white);
                    b._flames[i * Tiles + k] = f;
                }
            b._spark = new RectTransform[Sparks]; b._sparkImg = new Image[Sparks]; b._sparkSpeed = new float[Sparks];
            for (int i = 0; i < Sparks; i++)
            {
                float sz = Random.Range(2f, 6f);
                var s = UiKit.Image(root, "Chispa", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(sz, sz), new Color(1f, Random.Range(0.45f, 0.8f), 0.2f, 0.95f));
                b._spark[i] = s.rectTransform; b._sparkImg[i] = s; b.Respawn(i, Random.value * 900f);
            }
            b._grain = new GameObject("Grano", typeof(RectTransform)).AddComponent<RawImage>(); b._grain.transform.SetParent(root, false); Fill(b._grain.rectTransform);
            b._grain.texture = UiKit.GrainTex; b._grain.raycastTarget = false; b._grain.uvRect = new Rect(0, 0, 12f, 7f);
            root.SetAsFirstSibling();
            return b;
        }

        private static void Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        private void Respawn(int i, float y)
        {
            _spark[i].anchoredPosition = new Vector2(Random.Range(-900f, 900f), y);
            _sparkSpeed[i] = Random.Range(60f, 220f);
        }

        private void Update()
        {
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float f = 0.04f + 0.035f * Mathf.PerlinNoise(t * 3.1f, 0.3f);
            var c = _flicker.color; c.a = f; _flicker.color = c;
            _smoke.uvRect = new Rect(t * 0.006f, -t * 0.012f, 1f, 1f);
            _grain.uvRect = new Rect(Random.value, Random.value, 12f, 7f);
            const int rows = SevenDays.Procedural.MenuArt.FlameFrames / SevenDays.Procedural.MenuArt.FlameCols;
            for (int i = 0; i < FlameLayers; i++)
            {
                int fr = ((int)(t * FlameFps) + i * 11) % SevenDays.Procedural.MenuArt.FlameFrames;
                float w = 1f / SevenDays.Procedural.MenuArt.FlameCols, h = 1f / rows;
                var uv = new Rect((fr % SevenDays.Procedural.MenuArt.FlameCols) * w, (rows - 1 - fr / SevenDays.Procedural.MenuArt.FlameCols) * h, w, h);
                for (int k = 0; k < Tiles; k++) _flames[i * Tiles + k].uvRect = uv;
            }
            var gc = _glow.color; gc.a = 0.14f + 0.1f * Mathf.PerlinNoise(t * 2.3f, 0.7f); _glow.color = gc;
            for (int i = 0; i < Sparks; i++)
            {
                var p = _spark[i].anchoredPosition; p.y += _sparkSpeed[i] * dt; p.x += Mathf.Sin(t * 2f + i) * 12f * dt;
                if (p.y > 1100f) Respawn(i, 0f); else _spark[i].anchoredPosition = p;
            }
        }
    }
}

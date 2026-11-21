using UnityEngine;
using UnityEngine.UI;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Fondo compartido del menú y de las pantallas: la textura cacheada (ciudad ardiendo, calavera propia, pintura desgastada) y encima, animados sin memoria
    /// por frame: parpadeo del fuego, humo que sube despacio, grano que salta y 14 chispas recicladas.
    /// </summary>
    public class FlameBackdrop : MonoBehaviour
    {
        public const int Sparks = 14;
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
            b._spark = new RectTransform[Sparks]; b._sparkImg = new Image[Sparks]; b._sparkSpeed = new float[Sparks];
            for (int i = 0; i < Sparks; i++)
            {
                var s = UiKit.Image(root, "Chispa", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4f, 4f), new Color(1f, 0.7f, 0.3f, 0.9f));
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
            _sparkSpeed[i] = Random.Range(40f, 120f);
        }

        private void Update()
        {
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float f = 0.04f + 0.035f * Mathf.PerlinNoise(t * 3.1f, 0.3f);
            var c = _flicker.color; c.a = f; _flicker.color = c;
            _smoke.uvRect = new Rect(t * 0.006f, -t * 0.012f, 1f, 1f);
            _grain.uvRect = new Rect(Random.value, Random.value, 12f, 7f);
            for (int i = 0; i < Sparks; i++)
            {
                var p = _spark[i].anchoredPosition; p.y += _sparkSpeed[i] * dt; p.x += Mathf.Sin(t * 2f + i) * 12f * dt;
                if (p.y > 700f) Respawn(i, 0f); else _spark[i].anchoredPosition = p;
            }
        }
    }
}

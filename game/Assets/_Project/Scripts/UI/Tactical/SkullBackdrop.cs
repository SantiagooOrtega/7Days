using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using SevenDays.Procedural;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Calavera roja del fondo del menú: la procedural detallada (MenuArt.DetailedSkull, 1024 px), a ~65 % del alto, centrada en la mitad derecha, al 60 % y
    /// detrás del menú. Se genera una vez en segundo plano (no frena la carga) y queda en caché; aparece con un fundido. Respira despacio (1.00-1.02 en 6 s).
    /// No usa imágenes externas aunque haya alguna en Resources. Sin memoria por frame.
    /// </summary>
    public class SkullBackdrop : MonoBehaviour
    {
        public const float Height = 702f, CenterX = 470f, CenterY = 20f, BreathPeriod = 6f, BreathScale = 0.02f;
        private static Texture2D _tex;
        private static Task<byte[]> _job;
        private RawImage _img;
        private RectTransform _rt;
        private float _shownAt = -1f;

        public static SkullBackdrop Create(Transform parent)
        {
            var img = new GameObject("Calavera fondo", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(parent, false); img.raycastTarget = false;
            var r = img.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(Height, Height); r.anchoredPosition = new Vector2(CenterX, CenterY);
            var s = img.gameObject.AddComponent<SkullBackdrop>(); s._img = img; s._rt = r;
            if (_tex == null && _job == null) { int n = MenuArt.SkullSize; _job = Task.Run(() => MenuArt.DetailedSkull(n, 13)); }
            s.TryShow();
            return s;
        }

        /// <summary>Para las pruebas y la carga: genera la textura ya (bloqueando).</summary>
        public static Texture2D TextureNow()
        {
            if (_tex == null) _tex = UiKit.Make(MenuArt.SkullSize, MenuArt.SkullSize, _job != null ? _job.Result : MenuArt.DetailedSkull(MenuArt.SkullSize, 13), "Calavera roja", TextureWrapMode.Clamp);
            _job = null; return _tex;
        }

        private void TryShow()
        {
            if (_tex == null && _job != null && _job.IsCompleted) TextureNow();
            _img.texture = _tex; _img.enabled = _tex != null;
            if (_tex != null && _shownAt < 0f) _shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (_img.texture == null) { TryShow(); return; }
            float fade = Mathf.Clamp01((Time.unscaledTime - _shownAt) / 0.8f);
            _img.color = new Color(1f, 1f, 1f, fade);
            float s = 1f + BreathScale * 0.5f * (1f - Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / BreathPeriod)));
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using SevenDays.Procedural;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Calavera roja del fondo del menú, a la derecha y a ~50 % de la altura. Usa la imagen "calavera_fondo" de una carpeta Resources si existe (se recorta el
    /// fondo plano y se tiñe con MenuArt.RedSkull); si no, un cráneo procedural sin símbolos. La textura se procesa una vez y se cachea. Solo se muestra en el menú principal (en las demás pantallas taparía texto). Respira despacio
    /// (escala 1.00-1.02 en 6 s) sin memoria por frame.
    /// </summary>
    public class SkullBackdrop : MonoBehaviour
    {
        public const string ResourceName = "calavera_fondo";
        public const float Height = 540f, BreathPeriod = 6f, BreathScale = 0.02f;
        public const int MaxSize = 512;
        private static Texture2D _tex;
        private RectTransform _rt;

        public static bool UsingImportedImage { get; private set; }

        public static SkullBackdrop Create(Transform parent)
        {
            var img = new GameObject("Calavera fondo", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(parent, false); img.raycastTarget = false;
            img.texture = Texture;
            var r = img.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(Height * _tex.width / _tex.height, Height); r.anchoredPosition = new Vector2(500f, 40f);
            var s = img.gameObject.AddComponent<SkullBackdrop>(); s._rt = r;
            return s;
        }

        public static Texture2D Texture
        {
            get
            {
                if (_tex != null) return _tex;
                var src = Resources.Load<Texture2D>(ResourceName);
                float[] lum, alpha; int w, h;
                if (src != null && TryRead(src, out var rgba, out w, out h)) { MenuArt.Cutout(rgba, w, h, out lum, out alpha); UsingImportedImage = true; }
                else { w = h = MaxSize; MenuArt.ProceduralSkull(w, out lum, out alpha); UsingImportedImage = false; }
                _tex = UiKit.Make(w, h, MenuArt.RedSkull(lum, alpha, w, h, 13), "Calavera roja", TextureWrapMode.Clamp);
                return _tex;
            }
        }

        // lee los píxeles aunque la textura no sea legible (copia a la GPU y vuelta), reducida a MaxSize como mucho
        private static bool TryRead(Texture2D src, out byte[] rgba, out int w, out int h)
        {
            float k = Mathf.Min(1f, MaxSize / (float)Mathf.Max(src.width, src.height));
            w = Mathf.Max(8, Mathf.RoundToInt(src.width * k)); h = Mathf.Max(8, Mathf.RoundToInt(src.height * k)); rgba = null;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt); RenderTexture.active = rt;
                var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0); tmp.Apply(false);
                rgba = tmp.GetRawTextureData(); if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
                return rgba != null && rgba.Length == w * h * 4;
            }
            catch { return false; }
            finally { RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); }
        }

        private void Update()
        {
            float s = 1f + BreathScale * 0.5f * (1f - Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / BreathPeriod)));
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace SevenDays.Look
{
    /// <summary>
    /// Materiales de partículas creados por código con el shader de partículas de URP (usa el color por partícula,
    /// a diferencia del Lit). En una compilación (build) hay que incluir el shader: ver docs/UNITY_SETUP.md.
    /// </summary>
    public static class ParticleMaterials
    {
        private static Material _soft, _softAdd, _streakAdd, _sparkAdd;
        private static Texture2D _softTex, _streakTex;

        public static Material Soft => _soft != null ? _soft : _soft = Create(SoftTexture(), false, Color.white);
        public static Material SoftAdditive => _softAdd != null ? _softAdd : _softAdd = Create(SoftTexture(), true, Color.white);
        /// <summary>Haz alargado y difuso (rayos de luz).</summary>
        public static Material Streak => _streakAdd != null ? _streakAdd : _streakAdd = Create(StreakTexture(), true, Color.white);
        /// <summary>Chispas: aditivo con color HDR (>1) para que el Bloom las haga brillar.</summary>
        public static Material Spark => _sparkAdd != null ? _sparkAdd : _sparkAdd = Create(SoftTexture(), true, new Color(3.2f, 1.6f, 0.5f, 1f));

        private static Material Create(Texture2D tex, bool additive, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = additive ? "ParticleAdditive" : "ParticleSoft", enableInstancing = true };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f); // transparente
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_BlendOp", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", 1f);
            m.SetFloat("_DstBlendAlpha", additive ? 1f : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            return m;
        }

        private static Texture2D SoftTexture()
        {
            if (_softTex != null) return _softTex;
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smoothstep: borde totalmente transparente
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            _softTex = MakeTexture(px, n, n);
            return _softTex;
        }

        private static Texture2D StreakTexture()
        {
            if (_streakTex != null) return _streakTex;
            const int w = 32, h = 128;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);   // suave a los lados
                    float along = Mathf.Sin((y + 0.5f) / h * Mathf.PI);          // se desvanece en las puntas
                    float a = Mathf.SmoothStep(0f, 1f, across) * along;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            _streakTex = MakeTexture(px, w, h);
            return _streakTex;
        }

        private static Texture2D MakeTexture(Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _soft = _softAdd = _streakAdd = _sparkAdd = null;
            _softTex = _streakTex = null;
        }
    }
}

using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SevenDays.Procedural;

namespace SevenDays.Look
{
    public enum QualityMode { Auto, Alta, Media, Baja }

    /// <summary>
    /// Gobernador de calidad unificado. Cada nivel (QualityProfile: Alta, Media, Baja, Mínima) fija sombras (distancia, cascadas, suavidad), SSAO, bloom, grano,
    /// densidad atmosférica y el rango de la resolución dinámica. En "Auto":
    ///  1) la resolución dinámica (DynamicResolution, por tiempo de GPU, con FSR) baja o sube el render scale dentro del rango del nivel, rápido y con histéresis;
    ///  2) si aun en su mínimo el FPS sigue bajo, QualityGovernor baja un nivel; si el FPS se recupera holgado con la resolución al máximo, sube otro (4 s).
    /// Con Alta/Media/Baja el nivel queda fijo (la resolución dinámica sigue si está activada). F3 pasa al siguiente nivel a mano. La interfaz (IMGUI) se dibuja
    /// siempre a resolución nativa. Trabaja sobre una COPIA del URP Asset: el asset del proyecto no se toca (al salir se restaura todo).
    /// </summary>
    public class QualityDirector : MonoBehaviour
    {
        public const string ModeKey = "7d.quality.mode", DynResKey = "7d.quality.dynres";
        public static QualityDirector Instance { get; private set; }

        public QualityMode Mode { get; private set; } = QualityMode.Auto;
        public bool DynamicResolution { get; private set; } = true;
        public int Level => _gov.Level;
        public float RenderScale => _clone != null ? _clone.renderScale : 1f;
        public float GpuMs { get; private set; }
        public float Fps => _fps;

        private readonly QualityGovernor _gov = new QualityGovernor();
        private readonly Procedural.DynamicResolution _dyn = new Procedural.DynamicResolution();
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private UniversalRenderPipelineAsset _clone;
        private RenderPipelineAsset _prevQualityRp;
        private bool _hadQualityRp;
        private ScriptableRendererFeature _ssao;
        private object _ssaoSettings;
        private FieldInfo _ssaoDown;
        private bool _ssaoWasActive, _ssaoWasDown;
        private Light _sun;
        private UniversalAdditionalLightData _sunData;
        private float _fps = 60f;

        private void Start()
        {
            Instance = this;
            Mode = (QualityMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 3);
            DynamicResolution = PlayerPrefs.GetInt(DynResKey, 1) == 1;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                _clone = Instantiate(urp);
                _clone.name = urp.name + " (runtime)";
                _prevQualityRp = QualitySettings.renderPipeline; _hadQualityRp = true;
                QualitySettings.renderPipeline = _clone;
                _clone.upscalingFilter = UpscalingFilterSelection.FSR;   // al bajar el render scale, reescala con FSR 1 (la interfaz va aparte, nativa)
                _ssao = Feature(_clone, "ScreenSpaceAmbientOcclusion");
                if (_ssao != null)
                {
                    _ssaoWasActive = _ssao.isActive;
                    _ssaoSettings = _ssao.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_ssao);
                    _ssaoDown = _ssaoSettings?.GetType().GetField("Downsample", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (_ssaoDown != null) _ssaoWasDown = (bool)_ssaoDown.GetValue(_ssaoSettings);
                }
            }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { _sun = l; _sunData = l.GetUniversalAdditionalLightData(); break; }
            if (Mode != QualityMode.Auto) _gov.SetLevel((int)Mode - 1);
            Apply();
        }

        private void OnDestroy()
        {
            // el renderer (SSAO) es un asset compartido: se deja como estaba; el URP Asset del proyecto nunca se tocó (se usó una copia)
            if (_ssao != null) { _ssao.SetActive(_ssaoWasActive); if (_ssaoDown != null) _ssaoDown.SetValue(_ssaoSettings, _ssaoWasDown); }
            if (_hadQualityRp) QualitySettings.renderPipeline = _prevQualityRp;
            if (_clone != null) Destroy(_clone);
            if (Instance == this) Instance = null;
        }

        public void SetMode(QualityMode mode)
        {
            Mode = mode;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            _gov.SetLevel(mode == QualityMode.Auto ? _gov.Level : (int)mode - 1);
            Apply();
        }

        public void SetDynamicResolution(bool on)
        {
            DynamicResolution = on;
            PlayerPrefs.SetInt(DynResKey, on ? 1 : 0);
            Apply();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(1e-4f, dt), 0.08f);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0) GpuMs = Mathf.Lerp(GpuMs, (float)_timing[0].gpuFrameTime, 0.2f);

            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && k.f3Key.wasPressedThisFrame) { _gov.SetLevel((_gov.Level + 1) % (_gov.MaxLevel + 1)); Apply(); return; }

            bool changed = false;
            if (DynamicResolution && _clone != null && _dyn.Update(dt, GpuMs, dt * 1000f)) { _clone.renderScale = _dyn.Scale; }
            if (Mode == QualityMode.Auto)
                changed = _gov.Update(dt, _fps, !DynamicResolution || _dyn.AtMin, !DynamicResolution || _dyn.AtMax);
            if (changed)
            {
                Debug.Log("[7Days] Calidad automática: nivel " + QualityProfile.For(_gov.Level).Name + " (FPS " + Mathf.RoundToInt(_fps) + ", escala " + RenderScale.ToString("0.00") + ")");
                Apply();
            }
        }

        /// <summary>Aplica el nivel actual: sombras, SSAO, post-procesado, atmósfera y rango de resolución.</summary>
        private void Apply()
        {
            var p = QualityProfile.For(_gov.Level);
            if (_clone != null)
            {
                _clone.shadowDistance = p.ShadowDistance;
                _clone.shadowCascadeCount = p.Cascades;
                _dyn.SetRange(p.MinScale, p.MaxScale);
                if (!DynamicResolution) _dyn.Reset(p.MaxScale);
                _clone.renderScale = _dyn.Scale;
            }
            if (_sun != null)
            {
                _sun.shadows = p.Softness == ShadowSoftness.Hard ? LightShadows.Hard : LightShadows.Soft;
                if (_sunData != null) _sunData.softShadowQuality = p.Softness == ShadowSoftness.Low ? SoftShadowQuality.Low : SoftShadowQuality.Medium;
            }
            if (_ssao != null)
            {
                _ssao.SetActive(p.Ssao);
                if (_ssaoDown != null) _ssaoDown.SetValue(_ssaoSettings, p.SsaoHalf);
            }
            LookAndFeel.SetPostQuality(p.Bloom, p.BloomQuarter, p.Grain);
            AtmosphereBudget.SetLevel(p.Atmosphere);
        }

        public static string Describe()
        {
            var d = Instance;
            if (d == null) return "(sin gobernador)";
            return (d.Mode == QualityMode.Auto ? "Auto" : d.Mode.ToString()) + " · nivel " + QualityProfile.For(d.Level).Name + " · resolución dinámica " +
                   (d.DynamicResolution ? d.RenderScale.ToString("0.00") + " (" + d._dyn.Min.ToString("0.00") + "-" + d._dyn.Max.ToString("0.00") + ")" : "apagada");
        }

        /// <summary>Renderer feature por nombre del renderer por defecto (la lista no es pública).</summary>
        public static ScriptableRendererFeature Feature(UniversalRenderPipelineAsset urp, string name)
        {
            var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(urp) as ScriptableRendererData[];
            if (list == null || list.Length == 0 || list[0] == null) return null;
            foreach (var f in list[0].rendererFeatures) if (f != null && f.name.Contains(name)) return f;
            return null;
        }
    }
}

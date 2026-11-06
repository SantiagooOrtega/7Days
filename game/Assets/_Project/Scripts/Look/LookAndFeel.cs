using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SevenDays.Procedural;

namespace SevenDays.Look
{
    /// <summary>
    /// Aplica el "look" del juego por código: post-procesado URP (desaturado, contraste alto, viñeta,
    /// bloom suave, grano), niebla, luz de atardecer y ajustes de rendimiento. Todos los números están
    /// en LookSettings para ajustarlos mirando una captura.
    /// </summary>
    public static class LookAndFeel
    {
        public static void Apply(Camera cam, LookSettings s = null)
        {
            s ??= new LookSettings();
            ApplyQuality(s);
            ApplyAtmosphere(cam, s);
            ApplyPostProcessing(cam, s);
            ApplySun(s);
        }

        private static void ApplyQuality(LookSettings s)
        {
            // Si existe un nivel de calidad llamado "Demo" (ver docs/UNITY_SETUP.md) se usa.
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == "Demo") { QualitySettings.SetQualityLevel(i, true); break; }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = s.TargetFps;
        }

        private static void ApplyAtmosphere(Camera cam, LookSettings s)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = s.FogColor;
            RenderSettings.fogDensity = s.FogDensity;

            // Sin skybox: el fondo es del color de la niebla, así el horizonte se funde con ella.
            RenderSettings.skybox = null;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = s.FogColor;
            cam.farClipPlane = 120f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.AmbientSky;
            RenderSettings.ambientEquatorColor = s.AmbientEquator;
            RenderSettings.ambientGroundColor = s.AmbientGround;
        }

        private static void ApplySun(LookSettings s)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(s.SunPitch, s.SunYaw, 0f);
                l.color = s.SunColor;
                l.intensity = s.SunIntensity;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.92f;
                break;
            }
        }

        private static void ApplyPostProcessing(Camera cam, LookSettings s)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Low;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var tone = profile.Add<Tonemapping>(false);
            tone.mode.Override(TonemappingMode.ACES);

            var adj = profile.Add<ColorAdjustments>(false);
            adj.saturation.Override(s.Saturation);
            adj.contrast.Override(s.Contrast);
            adj.postExposure.Override(s.PostExposure);
            adj.colorFilter.Override(s.ColorFilter);

            var vig = profile.Add<Vignette>(false);
            vig.intensity.Override(s.VignetteIntensity);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Color.black);

            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(s.BloomThreshold);
            bloom.intensity.Override(s.BloomIntensity);
            bloom.scatter.Override(0.6f);

            var grain = profile.Add<FilmGrain>(false);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(s.GrainIntensity);
            grain.response.Override(0.8f);

            var go = new GameObject("PostProcessVolume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100; // por encima del Volume que trae la plantilla
            vol.weight = 1f;
            vol.sharedProfile = profile;
        }
    }

    /// <summary>Todos los valores del look en un solo sitio.</summary>
    public class LookSettings
    {
        public int TargetFps = 60;

        /// <summary>Texturas procedurales: tamaño (512 o 1024), colores, óxido, grietas y suciedad. Ver SurfaceParams.</summary>
        public SurfaceParams Surface = new SurfaceParams { Size = 512 };

        // Niebla y ambiente
        public Color FogColor = new Color(0.36f, 0.35f, 0.33f);
        public float FogDensity = 0.016f;
        public Color AmbientSky = new Color(0.34f, 0.37f, 0.42f);
        public Color AmbientEquator = new Color(0.24f, 0.24f, 0.25f);
        public Color AmbientGround = new Color(0.1f, 0.1f, 0.1f);

        // Sol (atardecer bajo, sombras largas)
        public float SunPitch = 28f;
        public float SunYaw = -38f;
        public Color SunColor = new Color(1f, 0.78f, 0.58f);
        public float SunIntensity = 1.15f;

        // Post-procesado
        public float Saturation = -38f;
        public float Contrast = 28f;
        public float PostExposure = -0.15f;
        public Color ColorFilter = new Color(0.9f, 0.95f, 1f);
        public float VignetteIntensity = 0.38f;
        public float BloomThreshold = 1.0f;
        public float BloomIntensity = 0.45f;
        public float GrainIntensity = 0.22f;
    }
}

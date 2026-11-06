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

            // Filtrado anisotrópico: cada textura pide 8x (ver SurfaceLibrary); aquí se fuerza a que se respete.
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = s.TargetFps;
        }

        private static void ApplyAtmosphere(Camera cam, LookSettings s)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = s.EffectiveFogColor;
            RenderSettings.fogDensity = s.FogDensity;

            // Sin skybox: el fondo es del color de la niebla, así el horizonte se funde con ella.
            RenderSettings.skybox = null;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = s.EffectiveSkyColor; // cielo apagado: más oscuro y gris que la niebla
            cam.farClipPlane = 120f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.AmbientSky * s.GlobalBrightness;
            RenderSettings.ambientEquatorColor = s.AmbientEquator * s.GlobalBrightness;
            RenderSettings.ambientGroundColor = s.AmbientGround * s.GlobalBrightness;
        }

        private static void ApplySun(LookSettings s)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(s.SunPitch, s.SunYaw, 0f);
                l.color = s.SunColor;
                l.intensity = s.SunIntensity * s.GlobalBrightness;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = s.ShadowStrength;
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
            adj.postExposure.Override(s.EffectivePostExposure);
            adj.colorFilter.Override(s.ColorFilter);

            var vig = profile.Add<Vignette>(false);
            vig.intensity.Override(Mathf.Min(s.VignetteIntensity, LookSettings.MaxVignette));
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

    /// <summary>
    /// Todos los valores del look en un solo sitio. "GlobalBrightness" sube o baja de un golpe la luz del sol,
    /// el ambiente, la exposición, la niebla y la paleta de las texturas (1 = neutro, rango útil 0.5 a 2).
    /// </summary>
    public class LookSettings
    {
        public int TargetFps = 60;

        /// <summary>Brillo global. 1 = valores base; por defecto medio-alto para que se distinga bien el escenario.</summary>
        public float GlobalBrightness = 1.25f;

        /// <summary>Texturas procedurales: tamaño (512 o 1024), colores, óxido, grietas y suciedad. Ver SurfaceParams.</summary>
        public SurfaceParams Surface = new SurfaceParams { Size = 512 };

        /// <summary>Ambiente apocalíptico: niebla baja, bruma, ceniza, humo, chispas, rayos de luz y suciedad del suelo.</summary>
        public AtmosphereSettings Atmosphere = new AtmosphereSettings();

        /// <summary>Vegetación y relleno: árboles muertos, arbustos, hierba, coches, neumáticos... con topes de cantidad.</summary>
        public VegetationSettings Vegetation = new VegetationSettings();

        /// <summary>Señales de tráfico desgastadas y marcas viales.</summary>
        public SignSettings Signs = new SignSettings();

        /// <summary>Salto, vault y caída: altura de salto, alturas mín/máx de vault, duración, coyote time, jump buffer, gravedad. Se cargan del ScriptableObject Resources/Config/Movement si existe.</summary>
        public SevenDays.Procedural.MovementSettings Movement = SevenDays.Player.PlayerMovementConfig.LoadSettings();

        // Niebla y ambiente: gris azulado medio (tarde nublada)
        public Color FogColor = new Color(0.50f, 0.55f, 0.60f);
        public float FogDensity = 0.008f;
        public Color AmbientSky = new Color(0.50f, 0.56f, 0.64f);
        public Color AmbientEquator = new Color(0.40f, 0.42f, 0.46f);
        public Color AmbientGround = new Color(0.22f, 0.22f, 0.23f);

        // Sol alto y de luz fría y difusa (nublado), sombras suaves para que no queden negras
        public float SunPitch = 52f;
        public float SunYaw = -35f;
        public Color SunColor = new Color(0.96f, 0.95f, 0.93f);
        public float SunIntensity = 1.0f;
        public float ShadowStrength = 0.78f;

        // Post-procesado: sigue desaturado, pero con menos contraste y viñeta
        public float Saturation = -30f;
        public float Contrast = 14f;
        public float PostExposure = 0.35f;
        public Color ColorFilter = new Color(0.94f, 0.97f, 1f);
        public float VignetteIntensity = 0.22f;
        public const float MaxVignette = 0.25f;
        public float BloomThreshold = 1.0f;
        public float BloomIntensity = 0.4f;
        public float GrainIntensity = 0.15f;

        /// <summary>Exposición en pasos (EV): +0.3 por cada +0.25 de brillo.</summary>
        public float EffectivePostExposure => PostExposure + (GlobalBrightness - 1f) * 1.2f;

        /// <summary>Color del cielo: el de la niebla mezclado con gris oscuro según Atmosphere.SkyDim.</summary>
        public Color EffectiveSkyColor => Color.Lerp(EffectiveFogColor, new Color(0.30f, 0.31f, 0.33f), Mathf.Clamp01(Atmosphere.SkyDim));

        public Color EffectiveFogColor
        {
            get
            {
                float k = Mathf.Sqrt(GlobalBrightness); // la niebla cambia más despacio que la luz
                return new Color(Mathf.Min(1f, FogColor.r * k), Mathf.Min(1f, FogColor.g * k), Mathf.Min(1f, FogColor.b * k), 1f);
            }
        }

        /// <summary>Parámetros de textura con el brillo global aplicado a la paleta (curva suave: el brillo pesa menos que en la luz).</summary>
        public SurfaceParams EffectiveSurface()
        {
            var p = Surface.Clone();
            p.Brightness = Surface.Brightness * Mathf.Pow(GlobalBrightness, 0.6f);
            return p;
        }
    }
}

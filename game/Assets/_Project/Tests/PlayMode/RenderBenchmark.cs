using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Banco de pruebas de render (no es una prueba de aprobado/suspenso: escribe una tabla en la consola). Levanta la arena completa, renderiza la cámara del
    /// jugador a una textura a 1280x720, 1920x1080 y 2560x1440 y mide cada frame forzando a la GPU a terminar (lectura de 1 píxel). Luego apaga cada cosa por
    /// separado para ver cuánto cuesta. Medida aproximada (CPU + GPU de un render, sin la ventana del editor): sirve para comparar antes/después.
    /// Se ejecuta a mano: Test Runner > PlayMode > RenderBenchmark (es [Explicit]).
    /// </summary>
    [Explicit, Category("Rendimiento")]
    public class RenderBenchmark
    {
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) };

        [UnityTest]
        public IEnumerator Measure()
        {
            var arenaType = Assembly.Load("Assembly-CSharp").GetType("SevenDays.World.TestArena", true);
            new GameObject("Arena").AddComponent(arenaType);
            // espera a que se generen texturas, mapa, jugador y atmósfera
            float t0 = UnityEngine.Time.realtimeSinceStartup;
            while (UnityEngine.Time.realtimeSinceStartup - t0 < 25f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 240; i++) yield return null;
            var cam = Camera.main;
            Assert.IsNotNull(cam, "sin cámara");

            var sb = new System.Text.StringBuilder("[Benchmark render] " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")\n");
            sb.Append(Counts(cam)).Append('\n');
            var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            sb.Append("URP: renderScale " + urp.renderScale + " MSAA " + urp.msaaSampleCount + " HDR " + urp.supportsHDR + " sombras " + urp.shadowDistance + " m x" + urp.shadowCascadeCount +
                      " cascadas, mapa " + urp.mainLightShadowmapResolution + "  opaca " + urp.supportsCameraOpaqueTexture + " profundidad " + urp.supportsCameraDepthTexture + "\n");

            var dirType = Assembly.Load("Assembly-CSharp").GetType("SevenDays.Look.QualityDirector");
            var director = dirType?.GetProperty("Instance")?.GetValue(null);
            if (director != null)
            {
                // con el gobernador unificado: cada nivel a escala 1.0 (sin resolución dinámica) y las escalas que usaría la resolución dinámica
                var modeType = Assembly.Load("Assembly-CSharp").GetType("SevenDays.Look.QualityMode");
                dirType.GetMethod("SetDynamicResolution").Invoke(director, new object[] { false });
                foreach (var m in new[] { "Alta", "Media", "Baja" })
                {
                    dirType.GetMethod("SetMode").Invoke(director, new[] { Enum.Parse(modeType, m) });
                    for (int i = 0; i < 10; i++) yield return null;
                    urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                    urp.renderScale = 1f;
                    sb.Append(Row("Calidad " + m + " (1.0)", cam));
                    sb.Append(Row("Calidad " + m + " (repetido)", cam));
                }
                dirType.GetMethod("SetMode").Invoke(director, new[] { Enum.Parse(modeType, "Alta") });
                for (int i = 0; i < 10; i++) yield return null;
                urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                foreach (float sc in new[] { 0.75f, 0.6f }) { urp.renderScale = sc; sb.Append(Row("Alta, escala " + sc.ToString("0.00"), cam)); }
                urp.renderScale = 1f;
            }
            else
            {
                sb.Append(Row("Actual", cam));
                sb.Append(Row("Actual (repetido)", cam));
                foreach (var (name, apply, undo) in Toggles(urp, cam))
                {
                    apply(); yield return null;
                    sb.Append(Row(name, cam));
                    undo(); yield return null;
                }
            }
            Debug.Log(sb.ToString());
        }

        private static string Row(string name, Camera cam)
        {
            var s = name.PadRight(26);
            var ms = new float[Sizes.Length];
            for (int i = 0; i < Sizes.Length; i++)
            {
                // la mejor de 3 medianas: el ruido (editor abierto, reloj de la GPU) solo suma
                ms[i] = float.MaxValue;
                for (int round = 0; round < 3; round++) ms[i] = Mathf.Min(ms[i], FrameMs(cam, Sizes[i]));
                s += "  " + Sizes[i].x + "x" + Sizes[i].y + ": " + ms[i].ToString("0.00").PadLeft(6) + " ms";
            }
            // coste de los píxeles: lo que sube de 720p a 1440p (el coste fijo de CPU y sincronización se cancela)
            return s + "   píxeles 1440p-720p: " + (ms[2] - ms[0]).ToString("0.00").PadLeft(5) + " ms\n";
        }

        /// <summary>ms por frame: MEDIANA de 40 renders tras 20 de calentamiento (compila variantes de shader), cada uno esperando a que la GPU termine.</summary>
        private static float FrameMs(Camera cam, Vector2Int size)
        {
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            for (int i = 0; i < 12; i++) { cam.Render(); Sync(rt); }
            var samples = new List<float>(30);
            var w = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < 30; i++) { w.Restart(); cam.Render(); Sync(rt); w.Stop(); samples.Add((float)w.Elapsed.TotalMilliseconds); }
            cam.targetTexture = prev;
            rt.Release(); UnityEngine.Object.Destroy(rt);
            samples.Sort();
            return samples[samples.Count / 2];
        }

        private static void Sync(RenderTexture rt) => AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();

        private static IEnumerable<(string, Action, Action)> Toggles(UniversalRenderPipelineAsset urp, Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            var ssao = Feature(urp, "ScreenSpaceAmbientOcclusion");
            if (ssao != null) yield return ("sin SSAO", () => ssao.SetActive(false), () => ssao.SetActive(true));
            float sd = urp.shadowDistance; int cc = urp.shadowCascadeCount;
            yield return ("sin sombras", () => urp.shadowDistance = 0f, () => urp.shadowDistance = sd);
            yield return ("sombras 2 cascadas", () => urp.shadowCascadeCount = 2, () => urp.shadowCascadeCount = cc);
            yield return ("sin post-procesado", () => data.renderPostProcessing = false, () => data.renderPostProcessing = true);
            var aa = data.antialiasing;
            yield return ("sin SMAA", () => data.antialiasing = AntialiasingMode.None, () => data.antialiasing = aa);
            bool op = urp.supportsCameraOpaqueTexture, dp = urp.supportsCameraDepthTexture;
            yield return ("sin textura opaca/prof.", () => { urp.supportsCameraOpaqueTexture = false; urp.supportsCameraDepthTexture = false; }, () => { urp.supportsCameraOpaqueTexture = op; urp.supportsCameraDepthTexture = dp; });
            bool hdr = urp.supportsHDR;
            yield return ("sin HDR", () => urp.supportsHDR = false, () => urp.supportsHDR = hdr);
            var particles = new List<ParticleSystemRenderer>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None)) if (r.enabled) particles.Add(r);
            yield return ("sin partículas", () => { foreach (var r in particles) r.enabled = false; }, () => { foreach (var r in particles) r.enabled = true; });
            float scale = urp.renderScale;
            yield return ("render scale 0.75", () => urp.renderScale = 0.75f, () => urp.renderScale = scale);
            yield return ("render scale 0.6", () => urp.renderScale = 0.6f, () => urp.renderScale = scale);
            Light sun = null;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                var ld = sun.GetUniversalAdditionalLightData();
                var q = ld.softShadowQuality; var sh = sun.shadows;
                yield return ("sombra suave baja", () => ld.softShadowQuality = SoftShadowQuality.Low, () => ld.softShadowQuality = q);
                yield return ("sombra suave media", () => ld.softShadowQuality = SoftShadowQuality.Medium, () => ld.softShadowQuality = q);
                yield return ("sombra dura", () => sun.shadows = LightShadows.Hard, () => sun.shadows = sh);
                yield return ("2 cascadas + suave baja", () => { urp.shadowCascadeCount = 2; ld.softShadowQuality = SoftShadowQuality.Low; }, () => { urp.shadowCascadeCount = cc; ld.softShadowQuality = q; });
            }
            if (ssao != null)
            {
                var settings = ssao.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ssao);
                var down = settings?.GetType().GetField("Downsample", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (down != null)
                {
                    bool was = (bool)down.GetValue(settings);
                    yield return ("SSAO a mitad", () => down.SetValue(settings, true), () => down.SetValue(settings, was));
                }
            }
        }

        /// <summary>Renderer feature por nombre (SSAO...) del renderer por defecto del URP Asset (por reflexión: la lista no es pública).</summary>
        public static ScriptableRendererFeature Feature(UniversalRenderPipelineAsset urp, string name)
        {
            var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(urp) as ScriptableRendererData[];
            if (list == null || list.Length == 0 || list[0] == null) return null;
            foreach (var f in list[0].rendererFeatures) if (f != null && f.name.Contains(name)) return f;
            return null;
        }

        private static string Counts(Camera cam)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            int visible = 0, casters = 0, tris = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.shadowCastingMode != ShadowCastingMode.Off) casters++;
                if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                visible++;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += (int)mf.sharedMesh.GetIndexCount(0) / 3;
            }
            int lights = 0, lightShadows = 0;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.enabled && l.gameObject.activeInHierarchy) { lights++; if (l.shadows != LightShadows.None) lightShadows++; }
            int ps = 0, alive = 0;
            foreach (var p in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) if (p.gameObject.activeInHierarchy) { ps++; alive += p.particleCount; }
            return "Escena: renderers visibles " + visible + " (~" + tris / 1000 + "k triángulos)  shadow casters " + casters + "  luces " + lights + " (con sombra " + lightShadows + ")  sistemas de partículas " + ps + " (" + alive + " partículas vivas)";
        }
    }
}

using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SevenDays.UI
{
    /// <summary>
    /// Panel "Render" de F6 (a la izquierda): resolución de la ventana y la interna (render scale), ms de CPU y de GPU del frame (FrameTimingManager), FPS, draw
    /// calls, SetPass, triángulos y shadow casters (ProfilerRecorder: editor y builds de desarrollo), luces activas y con sombra, partículas vivas, y el nivel de
    /// calidad del gobernador. Sirve para ver en la ventana real qué cuesta (los números del banco de pruebas en batch son aproximados).
    /// </summary>
    public class RenderDebugPanel : MonoBehaviour
    {
        private bool _open;
        private ProfilerRecorder _draw, _setPass, _tris, _casters;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private float _cpu, _gpu, _fps = 60f, _nextCount;
        private int _lights, _lightShadows, _particles;
        private GUIStyle _label, _head;
        private readonly StringBuilder _sb = new StringBuilder(600);

        /// <summary>Último tiempo de GPU medido (ms; 0 si la plataforma no lo da). Lo usa la resolución dinámica.</summary>
        public static float GpuMs { get; private set; }
        public static float CpuMs { get; private set; }

        private void OnEnable()
        {
            _draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
        }

        private void OnDisable() { _draw.Dispose(); _setPass.Dispose(); _tris.Dispose(); _casters.Dispose(); }

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f6Key.wasPressedThisFrame) _open = !_open;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
            {
                _cpu = Mathf.Lerp(_cpu, (float)_timing[0].cpuFrameTime, 0.1f);
                _gpu = Mathf.Lerp(_gpu, (float)_timing[0].gpuFrameTime, 0.1f);
                GpuMs = _gpu; CpuMs = _cpu;
            }
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime), 0.08f);
            if (_open && Time.unscaledTime > _nextCount)
            {
                // contar luces y partículas es caro: una vez por segundo y solo con el panel abierto
                _nextCount = Time.unscaledTime + 1f;
                _lights = _lightShadows = _particles = 0;
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.isActiveAndEnabled) { _lights++; if (l.shadows != LightShadows.None) _lightShadows++; }
                foreach (var p in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) if (p.gameObject.activeInHierarchy) _particles += p.particleCount;
            }
        }

        private void OnGUI()
        {
            if (!_open || Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _head ??= new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.5f) } };
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float scale = urp != null ? urp.renderScale : 1f;
            _sb.Clear();
            _sb.Append("Ventana ").Append(Screen.width).Append('x').Append(Screen.height).Append("  interna ").Append(Mathf.RoundToInt(Screen.width * scale)).Append('x').Append(Mathf.RoundToInt(Screen.height * scale))
               .Append(" (render scale ").Append(scale.ToString("0.00")).Append(urp != null && urp.upscalingFilter == UpscalingFilterSelection.FSR ? ", FSR" : "").Append(")\n");
            _sb.Append("FPS ").Append(Mathf.RoundToInt(_fps)).Append("   CPU ").Append(_cpu.ToString("0.0")).Append(" ms   GPU ").Append(_gpu > 0f ? _gpu.ToString("0.0") + " ms" : "(no disponible)").Append('\n');
            _sb.Append("Draw calls ").Append(Val(_draw)).Append("   SetPass ").Append(Val(_setPass)).Append("   triángulos ").Append(Val(_tris) / 1000).Append("k   shadow casters ").Append(Val(_casters)).Append('\n');
            _sb.Append("Luces ").Append(_lights).Append(" (con sombra ").Append(_lightShadows).Append(")   partículas vivas ").Append(_particles).Append('\n');
            if (urp != null)
                _sb.Append("Sombras ").Append(urp.shadowDistance.ToString("0")).Append(" m x").Append(urp.shadowCascadeCount).Append(" cascadas   HDR ").Append(urp.supportsHDR ? "sí" : "no")
                   .Append("   MSAA ").Append(urp.msaaSampleCount).Append("x\n");
            _sb.Append("Calidad: ").Append(SevenDays.Look.QualityDirector.Describe());
            var r = new Rect(10, 120, 470, 150);
            var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.65f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = prev;
            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 16, 22), "RENDER (F6)", _head);
            GUI.Label(new Rect(r.x + 8, r.y + 26, r.width - 16, r.height - 30), _sb.ToString(), _label);
        }

        private static long Val(ProfilerRecorder r) => r.Valid ? r.LastValue : -1;
    }
}

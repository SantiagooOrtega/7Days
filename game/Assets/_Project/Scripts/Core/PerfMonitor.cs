using System;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SevenDays.Core
{
    /// <summary>Resultado de una ventana de medición: ms medios por sistema, frame medio, p95, p99 y máximo, memoria por frame y recolecciones.</summary>
    public struct PerfReport
    {
        public int Frames, Zombies;
        public float FrameMs, P95, P99, MaxMs, AllocBytesPerFrame, AllocCountPerFrame, GcCollections, GcMs, MainMs, RenderMs, GpuMs;
        public float[] SectionBytes;   // bytes por frame de cada sistema propio (PerfProbe)
        public float[] Sections;   // PerfProbe (exclusivo) + motor: Física, NavMesh (motor), Partículas, IMGUI (motor), Audio (motor)

        public static readonly string[] EngineNames = { "Física", "NavMesh (motor)", "Partículas", "IMGUI (motor)", "Audio (motor)" };
        public static string Name(int i) => i < PerfProbe.Count ? PerfProbe.Names[i] : EngineNames[i - PerfProbe.Count];
        public static int Total => PerfProbe.Count + EngineNames.Length;

        public string Line() => Zombies + " zombis: frame " + FrameMs.ToString("0.00") + " ms, p95 " + P95.ToString("0.0") + ", p99 " + P99.ToString("0.0") + ", máx " + MaxMs.ToString("0.0")
            + " · hilo principal " + MainMs.ToString("0.0") + " ms, render " + RenderMs.ToString("0.0") + " ms, GPU " + (GpuMs > 0f ? GpuMs.ToString("0.0") + " ms" : "-")
            + " · GC " + AllocBytesPerFrame.ToString("0") + " B/frame (" + AllocCountPerFrame.ToString("0.0") + " alloc), " + GcCollections + " recolecciones";
    }

    /// <summary>
    /// Panel "CPU" de F6 (a la derecha): ms por sistema (IA/NavMesh, física, animación procedural, hitboxes, audio, VIGÍA, UI/IMGUI, loot y marcadores,
    /// partículas, GC), frame medio, picos p95/p99, memoria por frame y recolecciones de basura, y la tabla antes/después guardada por el banco de pruebas.
    /// Los sistemas propios se miden con PerfProbe (C#, exclusivo) y los del motor con ProfilerRecorder (editor y builds de desarrollo).
    /// Sin memoria por frame: buffers fijos; los percentiles se calculan dos veces por segundo.
    /// </summary>
    public class PerfMonitor : MonoBehaviour
    {
        public static PerfMonitor Instance { get; private set; }
        public const int History = 600;

        private ProfilerRecorder _physics, _nav, _partBegin, _partEnd, _gui, _guiEv, _audio, _gcAlloc, _gcCount, _gcCollect;
        private readonly float[] _frames = new float[History];
        private int _head, _filled, _gcBase;
        private readonly float[] _ema = new float[16];
        private float _p95, _p99, _max, _frameAvg, _nextStats, _allocEma, _allocCountEma;
        private bool _open;
        private GUIStyle _label, _head2;
        private readonly StringBuilder _sb = new StringBuilder(2048);
        private string _text = "";
        private float _nextText;

        // ventana de medición (pruebas y botón del panel)
        private bool _win;
        private int _winFrames, _winGc0;
        private double _winFrameMs, _winAlloc, _winAllocCount, _winGcMs, _winMain, _winRender, _winGpu;
        private int _winTimed;
        private readonly FrameTiming[] _ft = new FrameTiming[1];
        private readonly double[] _winSec = new double[16], _winBytes = new double[16];
        private readonly float[] _winSamples = new float[4096];

        /// <summary>Tabla antes/después (la rellena el banco de pruebas o el botón "Medir"); se muestra en el panel.</summary>
        public static string BeforeAfter = MeasuredTable;

        /// <summary>Banco de pruebas (PerfBenchmarkTests) en el editor con render real, horda persiguiendo al jugador: frame medio / p95 / p99 / máx (ms) y basura por frame.</summary>
        public const string MeasuredTable =
            "ANTES -> DESPUÉS (editor, render real; ms medio / p95 / p99 / máx)\n" +
            "Calle 30: 10.5/15.3/43.7/120 -> 7.8/9.1/9.6/9.7\n" +
            "Calle 40: 7.8/9.1/9.5/10.0 -> 7.9/9.2/10.4/10.6\n" +
            "Calle 60: 8.6/9.2/10.0/15.1 -> 8.6/9.2/9.8/10.2\n" +
            "Bosque 40: 8.9/16.0/56.2/107 -> 11.0/34.9/45.3/50.6 (hilo principal 5.0, GPU 3.7: picos del editor)\n" +
            "Basura del juego por frame: 20-36 alloc -> 0 (batch); recolecciones en la ventana: 2 -> 0";

        public static PerfMonitor Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Monitor CPU");
            DontDestroyOnLoad(go);
            return go.AddComponent<PerfMonitor>();
        }

        private void Awake()
        { useGUILayout = false;   // sin GUILayout: Unity se salta la pasada de Layout (la mitad del coste de IMGUI)
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            _physics = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FixedUpdate.PhysicsFixedUpdate");
            if (!_physics.Valid) { _physics.Dispose(); _physics = ProfilerRecorder.StartNew(new ProfilerCategory("PlayerLoop"), "FixedUpdate.PhysicsFixedUpdate"); }
            _nav = ProfilerRecorder.StartNew(ProfilerCategory.Ai, "NavMeshManager");
            _partBegin = ProfilerRecorder.StartNew(new ProfilerCategory("PlayerLoop"), "PreLateUpdate.ParticleSystemBeginUpdateAll");
            _partEnd = ProfilerRecorder.StartNew(ProfilerCategory.Particles, "ParticleSystem.EndUpdateAll");
            _gui = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "GUI.Repaint");
            _guiEv = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "GUI.ProcessEvents");
            _audio = ProfilerRecorder.StartNew(new ProfilerCategory("PlayerLoop"), "PostLateUpdate.UpdateAudio");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            _gcCollect = ProfilerRecorder.StartNew(new ProfilerCategory("GC"), "GC.Collect");
            _gcBase = GC.CollectionCount(0);
        }

        private void OnDisable()
        {
            _physics.Dispose(); _nav.Dispose(); _partBegin.Dispose(); _partEnd.Dispose(); _gui.Dispose(); _guiEv.Dispose(); _audio.Dispose();
            _gcAlloc.Dispose(); _gcCount.Dispose(); _gcCollect.Dispose();
        }

        private static float Ms(ProfilerRecorder r) => r.Valid ? r.LastValue * 1e-6f : 0f;

        /// <summary>Valor del último frame de la sección i (propias y del motor).</summary>
        public float Section(int i)
        {
            if (i < PerfProbe.Count) return PerfProbe.LastMs(i);
            switch (i - PerfProbe.Count)
            {
                case 0: return Ms(_physics);
                case 1: return Ms(_nav);
                case 2: return Ms(_partBegin) + Ms(_partEnd);
                case 3: return Ms(_gui) + Ms(_guiEv);
                default: return Ms(_audio);
            }
        }

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f6Key.wasPressedThisFrame) _open = !_open;
            float ms = Time.unscaledDeltaTime * 1000f;
            if (_filled == History) _hist[Bin(_frames[_head])]--;   // sale el más viejo
            _frames[_head] = ms; _hist[Bin(ms)]++; _head = (_head + 1) % History; if (_filled < History) _filled++;
            int n = PerfReport.Total;
            for (int i = 0; i < n; i++) _ema[i] = Mathf.Lerp(_ema[i], Section(i), 0.05f);
            float alloc = _gcAlloc.Valid ? _gcAlloc.LastValue : 0f, allocN = _gcCount.Valid ? _gcCount.LastValue : 0f;
            _allocEma = Mathf.Lerp(_allocEma, alloc, 0.05f); _allocCountEma = Mathf.Lerp(_allocCountEma, allocN, 0.05f);
            if (_win)
            {
                if (_winFrames < _winSamples.Length) _winSamples[_winFrames] = ms;
                _winFrames++; _winFrameMs += ms; _winAlloc += alloc; _winAllocCount += allocN; _winGcMs += Ms(_gcCollect);
                for (int i = 0; i < n; i++) _winSec[i] += Section(i);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _ft) > 0) { _winTimed++; _winMain += _ft[0].cpuMainThreadFrameTime; _winRender += _ft[0].cpuRenderThreadFrameTime; _winGpu += _ft[0].gpuFrameTime; }
                for (int i = 0; i < PerfProbe.Count; i++) _winBytes[i] += PerfProbe.LastBytes(i);
            }
            if (Time.unscaledTime >= _nextStats && _filled > 10)
            {
                _nextStats = Time.unscaledTime + 0.5f;
                // percentiles con el histograma (0.25 ms por casilla): sin ordenar ni reservar memoria
                _p95 = Percentile(0.95f); _p99 = Percentile(0.99f); _max = 0f;
                for (int i = 0; i < _filled; i++) if (_frames[i] > _max) _max = _frames[i];
                double s = 0; for (int i = 0; i < _filled; i++) s += _frames[i]; _frameAvg = (float)(s / _filled);
            }
        }

        private const float BinMs = 0.25f;
        private readonly int[] _hist = new int[801];
        private static int Bin(float ms) => Mathf.Clamp((int)(ms / BinMs), 0, 800);
        private float Percentile(float q)
        {
            int target = Mathf.CeilToInt(_filled * q), acc = 0;
            for (int i = 0; i < _hist.Length; i++) { acc += _hist[i]; if (acc >= target) return (i + 1) * BinMs; }
            return _hist.Length * BinMs;
        }

        public void BeginWindow()
        {
            _win = true; _winFrames = 0; _winTimed = 0; _winFrameMs = _winAlloc = _winAllocCount = _winGcMs = _winMain = _winRender = _winGpu = 0; _winGc0 = GC.CollectionCount(0);
            Array.Clear(_winSec, 0, _winSec.Length); Array.Clear(_winBytes, 0, _winBytes.Length);
        }

        public PerfReport EndWindow(int zombies)
        {
            _win = false;
            var r = new PerfReport { Frames = _winFrames, Zombies = zombies, Sections = new float[PerfReport.Total], SectionBytes = new float[PerfProbe.Count] };
            if (_winFrames == 0) return r;
            int n = Mathf.Min(_winFrames, _winSamples.Length);
            var sorted = new float[n]; Array.Copy(_winSamples, sorted, n); Array.Sort(sorted);
            r.FrameMs = (float)(_winFrameMs / _winFrames); r.P95 = sorted[(int)(n * 0.95f)]; r.P99 = sorted[Mathf.Min(n - 1, (int)(n * 0.99f))]; r.MaxMs = sorted[n - 1];
            r.AllocBytesPerFrame = (float)(_winAlloc / _winFrames); r.AllocCountPerFrame = (float)(_winAllocCount / _winFrames);
            r.GcCollections = GC.CollectionCount(0) - _winGc0;
            if (_winTimed > 0) { r.MainMs = (float)(_winMain / _winTimed); r.RenderMs = (float)(_winRender / _winTimed); r.GpuMs = (float)(_winGpu / _winTimed); } r.GcMs = (float)(_winGcMs / _winFrames);
            for (int i = 0; i < r.Sections.Length; i++) r.Sections[i] = (float)(_winSec[i] / _winFrames);
            for (int i = 0; i < r.SectionBytes.Length; i++) r.SectionBytes[i] = (float)(_winBytes[i] / _winFrames);
            return r;
        }

        private void OnGUI()
        {
            if (SevenDays.Core.HudGate.Hidden) return;   // intro o epílogo en pantalla: solo la lámina
           
            if (!_open || (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)) return;
            PerfProbe.Begin(PerfSection.UI);
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _head2 ??= new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.5f) } };
            if (Time.unscaledTime >= _nextText)
            {
                _nextText = Time.unscaledTime + 0.5f;   // el texto se rehace dos veces por segundo, no cada frame
                _sb.Clear();
                _sb.Append("Frame ").Append(_frameAvg.ToString("0.0")).Append(" ms   p95 ").Append(_p95.ToString("0.0")).Append("   p99 ").Append(_p99.ToString("0.0")).Append("   máx ").Append(_max.ToString("0.0")).Append('\n');
                _sb.Append("GC: ").Append(Mathf.RoundToInt(_allocEma)).Append(" B/frame (").Append(_allocCountEma.ToString("0.0")).Append(" alloc)   recolecciones ").Append(GC.CollectionCount(0) - _gcBase).Append('\n');
                var zm = SevenDays.Combat.ZombieManager.Instance;
                _sb.Append("Zombis activos ").Append(zm != null ? zm.ActiveCount : 0).Append(" (tope ").Append(zm != null ? zm.MaxZombies : 0).Append(")\n");
                for (int i = 0; i < PerfReport.Total; i++) _sb.Append(PerfReport.Name(i)).Append(": ").Append(_ema[i].ToString("0.00")).Append(" ms\n");
                if (BeforeAfter.Length > 0) _sb.Append('\n').Append(BeforeAfter);
                _text = _sb.ToString();
            }
            var r = new Rect(Screen.width - 520, 120, 510, 330 + (BeforeAfter.Length > 0 ? 200 : 0));
            var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = prev;
            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 16, 22), "CPU (F6)", _head2);
            GUI.Label(new Rect(r.x + 8, r.y + 26, r.width - 16, r.height - 30), _text, _label);
            PerfProbe.End();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() { Instance = null; BeforeAfter = MeasuredTable; }
    }
}

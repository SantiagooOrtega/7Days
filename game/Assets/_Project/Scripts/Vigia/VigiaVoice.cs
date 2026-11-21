using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Vigia
{
    /// <summary>
    /// VIGÍA: cámara asistente que habla. Lee el catálogo cerrado (Resources/vigia_frases.txt), pasa las peticiones por la cola (VigiaQueue: prioridades,
    /// enfriamientos, caducidad), reproduce el clip con filtro de radio, eco, bips y compresión (RadioFx) en su propio AudioSource de prioridad alta (grupo "Vigía")
    /// y dibuja el subtítulo abajo al centro. Si falta un clip, o en modo "Solo texto", solo subtítulo. Uso: VigiaVoice.Say("oleada").
    /// </summary>
    public class VigiaVoice : MonoBehaviour
    {
        public static VigiaVoice Instance { get; private set; }
        public Dictionary<string, VigiaLine> Catalog { get; private set; }
        public VigiaQueue Queue { get; } = new VigiaQueue(System.Environment.TickCount);
        /// <summary>Lo pone VigiaEvents: combate intenso (solo pasa lo importante).</summary>
        public bool Intense;
        /// <summary>Está hablando ahora (la cámara flotante late).</summary>
        public bool Speaking => Queue.Speaking(Time.unscaledTimeAsDouble);
        public VigiaItem Current => Queue.Current;

        private AudioSource _src;
        private float _subStart, _subEnd;
        private string _subText = "";
        private GUIStyle _style, _tag;

        public static VigiaVoice Create(Transform parent)
        {
            var go = new GameObject("VIGIA Voice");
            go.transform.SetParent(parent, false);
            return go.AddComponent<VigiaVoice>();
        }

        private void Awake()
        { useGUILayout = false;   // sin GUILayout: Unity se salta la pasada de Layout (la mitad del coste de IMGUI)
            Instance = this;
            var ta = Resources.Load<TextAsset>("vigia_frases");
            var errors = new List<string>();
            Catalog = ta != null ? VigiaCatalog.Parse(ta.text, errors) : new Dictionary<string, VigiaLine>();
            foreach (var e in errors) Debug.LogWarning("[7Days] VIGÍA: " + e);
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false; _src.spatialBlend = 0f; _src.priority = 0;   // 0 = la más alta: nunca la roba un disparo
            _src.bypassReverbZones = true;
            AudioMix.Route(_src, AudioGroup.Vigia, 1f);
            Queue.Mode = VigiaSettings.Mode;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Pide decir una frase del catálogo (se ignora si no existe, si está en enfriamiento o si el modo no la deja pasar).</summary>
        public static bool Say(string id)
        {
            var v = Instance;
            if (v == null || id == null || !v.Catalog.TryGetValue(id, out var line)) return false;
            return v.Queue.Request(line, Time.unscaledTimeAsDouble, v.Intense);
        }

        private void Update() { SevenDays.Core.PerfProbe.Begin(SevenDays.Core.PerfSection.Vigia); UpdateMedido(); SevenDays.Core.PerfProbe.End(); }


        private void UpdateMedido()
        {
            Queue.Mode = VigiaSettings.Mode;
            double now = Time.unscaledTimeAsDouble;
            _pickedClip = null;
            _durationFn ??= Duration;   // delegado creado una vez (antes, un cierre nuevo cada frame)
            var item = Queue.Next(now, _durationFn, out bool interrupted);
            var clip = _pickedClip;
            if (item == null) return;
            if (interrupted || _src.isPlaying) _src.Stop();
            if (clip != null) { _src.clip = clip; _src.Play(); }
            _subText = item.Text;
            _subStart = Time.unscaledTime;
            _subEnd = (float)Queue.CurrentEnds;
        }

        private string _subShown, _subFull = "";
        private readonly GUIContent _subContent = new GUIContent();
        private AudioClip _pickedClip;
        private System.Func<VigiaItem, double> _durationFn;
        private double Duration(VigiaItem it)
        {
            _pickedClip = Queue.Mode == VigiaMode.SoloTexto ? null : ClipFor(it);
            return _pickedClip != null ? _pickedClip.length + 0.25 : VigiaQueue.TextSeconds(it.Text);
        }

        /// <summary>Clip procesado (radio + bips) de la variante. null si no existe.</summary>
        public AudioClip ClipFor(VigiaItem it) => VigiaClips.Get(VigiaCatalog.ClipName(it.Line.Id, it.Variant));

        // ---------------------------------------------------------------- subtítulos
        private void OnGUI()
        {
            if (SevenDays.UI.StatsScreen.Blocking) return;   // pantalla de muerte/victoria: sin HUD de juego
            if (!VigiaSettings.Subtitles || VigiaSettings.Mode == VigiaMode.Apagada) return;
            float t = Time.unscaledTime;
            if (t > _subEnd + 0.4f || string.IsNullOrEmpty(_subText)) return;
            float alpha = Mathf.Clamp01((t - _subStart) / 0.2f) * Mathf.Clamp01((_subEnd + 0.4f - t) / 0.4f);
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            int size = Mathf.RoundToInt(26 * VigiaSettings.SubtitleScale);   // más compacto
            _style ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, richText = true };
            _style.fontSize = size;
            if (!ReferenceEquals(_subShown, _subText)) { _subShown = _subText; _subFull = "<color=#5fd0ff><b>VIGÍA:</b></color> " + _subText; _subContent.text = _subFull; }   // se compone al cambiar de frase, no cada frame
            string text = _subFull;
            float bw = Mathf.Min(SubtitleLayout.MaxWidth, w - 80f);
            float lineH = size * 1.3f;
            float th = Mathf.Min(_style.CalcHeight(_subContent, bw - 32f), lineH * 2f);   // máx. 2 líneas
            var lr = SubtitleLayout.Rect(w, th, VigiaSettings.SubtitleHeight);   // abajo, justo sobre la vida y la munición (ajustable en Esc)
            var r = new Rect(lr[0], lr[1], lr[2], lr[3]);
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(r.x + 16, r.y + 6, r.width - 32, r.height - 12), text, _style);
            GUI.color = prev;
        }
    }
}

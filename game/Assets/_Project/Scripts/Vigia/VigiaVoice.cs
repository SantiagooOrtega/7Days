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
        {
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

        private void Update()
        {
            Queue.Mode = VigiaSettings.Mode;
            double now = Time.unscaledTimeAsDouble;
            AudioClip clip = null;
            var item = Queue.Next(now, it =>
            {
                clip = Queue.Mode == VigiaMode.SoloTexto ? null : ClipFor(it);
                return clip != null ? clip.length + 0.25 : VigiaQueue.TextSeconds(it.Text);
            }, out bool interrupted);
            if (item == null) return;
            if (interrupted || _src.isPlaying) _src.Stop();
            if (clip != null) { _src.clip = clip; _src.Play(); }
            _subText = item.Text;
            _subStart = Time.unscaledTime;
            _subEnd = (float)Queue.CurrentEnds;
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
            int size = Mathf.RoundToInt(30 * VigiaSettings.SubtitleScale);
            _style ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, richText = true };
            _style.fontSize = size;
            string text = "<color=#5fd0ff><b>VIGÍA:</b></color> " + _subText;
            float bw = Mathf.Min(1150f, w - 80f);
            float lineH = size * 1.3f;
            float h = Mathf.Min(_style.CalcHeight(new GUIContent(text), bw - 40f), lineH * 2f) + 22f;   // máx. 2 líneas
            var r = new Rect((w - bw) / 2f, 1080f - 300f - h, bw, h);   // encima de la vida y la munición
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f * alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(r.x + 20, r.y + 11, r.width - 40, r.height - 22), text, _style);
            GUI.color = prev;
        }
    }
}

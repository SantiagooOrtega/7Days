using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Core;
using SevenDays.Procedural;
using SevenDays.Vigia;

namespace SevenDays.Intro
{
    /// <summary>
    /// Reproduce láminas de cómic a pantalla completa (IMGUI): la intro (8 láminas) y el epílogo. Cada lámina entra y sale con fundido, con movimiento lento
    /// (zoom y paneo, o recorrido por tramos) y encaje con letterbox. Pasa sola a los ~7 s (o cuando VIGÍA termina de hablar); clic, Espacio o flecha derecha
    /// avanzan al instante, flecha izquierda retrocede y Esc o el botón "SALTAR" (abajo a la derecha) la omiten entera. Puntos de progreso abajo.
    /// VIGÍA narra con su voz y subtítulos solo las frases de cada lámina. Al terminar la intro se guarda que ya se vio (PlayerPrefs); se repite desde Opciones.
    /// </summary>
    public class IntroPlayer : MonoBehaviour
    {
        private void Awake() { useGUILayout = false; }   // sin GUILayout: Unity se salta la pasada de Layout (la mitad del coste de IMGUI)

        public const string SeenKey = "intro.seen";
        public static bool Active { get; private set; }
        /// <summary>VIGÍA está hablando en la intro o el epílogo (la música baja).</summary>
        public static bool Speaking { get; private set; }
        public static bool Seen { get => PlayerPrefs.GetInt(SeenKey, 0) == 1; set => PlayerPrefs.SetInt(SeenKey, value ? 1 : 0); }
        /// <summary>Marca de la antigua opción "Saltar intro siempre" (ya no existe): se borra al arrancar.</summary>
        public const string SkipAlwaysKey = "intro.skipAlways";
        /// <summary>EMPEZAR en una partida nueva la pide (no reintentar ni continuar); se consume al arrancar la arena.</summary>
        public static bool NewGameRequested;
        /// <summary>Sale SIEMPRE en cada partida nueva (EMPEZAR); nunca al reintentar ni al continuar. Se puede saltar con SALTAR o Esc.</summary>
        public static bool ShouldPlay => NewGameRequested;
        public static bool ConsumeNewGame() { bool play = ShouldPlay; NewGameRequested = false; return play; }

        /// <summary>Las marcas viejas "intro ya vista" y "saltar intro siempre" la ocultaban en las partidas siguientes: se borran al arrancar.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ClearOldSeenFlag()
        {
            bool any = false;
            foreach (var k in new[] { SeenKey, SkipAlwaysKey }) if (PlayerPrefs.HasKey(k)) { PlayerPrefs.DeleteKey(k); any = true; }
            if (any) PlayerPrefs.Save();
        }

        private IntroSequence _seq;
        private bool _markSeen;
        private Action _onDone;
        private int _index, _target, _line;
        private float _panelStart, _leaveStart = -1f, _lineStart, _lineSeconds, _narrationDone = -1f;
        private string _subtitle = "";
        private AudioSource _src;
        private GUIStyle _text, _skip;
        private bool _overSkip, _paused;
        private static Dictionary<string, VigiaLine> _catalog;
        private static Texture2D _dot;

        public int Index => _index;
        public string Subtitle => _subtitle;

        /// <summary>La intro en cómic (al empezar una partida nueva o desde Opciones).</summary>
        public static IntroPlayer Play(Transform parent = null) => Run(IntroSequence.Load(), parent, true, null);

        /// <summary>El epílogo (final_01) al ganar; onDone muestra después la pantalla de victoria.</summary>
        public static IntroPlayer PlayEpilogue(Transform parent, Action onDone) => Run(IntroSequence.Epilogue(), parent, false, onDone);

        /// <summary>Cualquier secuencia de láminas; onDone se llama al terminar o al saltarla.</summary>
        public static IntroPlayer Run(IntroSequence seq, Transform parent, bool markSeen, Action onDone)
        {
            if (seq == null || seq.panels.Count == 0) { onDone?.Invoke(); return null; }
            var go = new GameObject("Intro");
            if (parent != null) go.transform.SetParent(parent, false);
            var p = go.AddComponent<IntroPlayer>();
            p._seq = seq; p._markSeen = markSeen; p._onDone = onDone;
            p._src = go.AddComponent<AudioSource>();
            p._src.playOnAwake = false; p._src.spatialBlend = 0f; p._src.priority = 0; p._src.ignoreListenerPause = true;
            AudioMix.Route(p._src, AudioGroup.Vigia, 1f);
            Active = true;
            GameInput.InputCaptured = true;
            p._paused = SevenDays.World.DayLoop.Instance != null;   // a mitad de partida (Opciones o epílogo): el juego espera
            if (p._paused) Time.timeScale = 0f;
            p.Show(0);
            return p;
        }

        /// <summary>Texto de una frase del catálogo de VIGÍA (primera variante).</summary>
        public static string LineText(string id)
        {
            if (_catalog == null)
            {
                var ta = Resources.Load<TextAsset>("vigia_frases");
                _catalog = VigiaCatalog.Parse(ta != null ? ta.text : "");
            }
            return _catalog.TryGetValue(id, out var l) && l.Variants.Length > 0 ? l.Variants[0] : "";
        }

        private IntroSequence.Panel Cur => _seq.panels[_index];
        private float Elapsed => Time.unscaledTime - _panelStart;
        private float Hold => Cur.seconds > 0f ? Cur.seconds : IntroText.AutoSeconds;

        private void Show(int i)
        {
            _index = Mathf.Clamp(i, 0, _seq.panels.Count - 1); _target = _index;
            _panelStart = Time.unscaledTime; _leaveStart = -1f;
            _line = -1; _subtitle = ""; _narrationDone = (Cur.narration == null || Cur.narration.Length == 0) ? 0f : -1f;
            if (_src != null) _src.Stop();
            Speaking = false;
        }

        private void StartLine(int n)
        {
            _line = n;
            string id = Cur.narration[n];
            _subtitle = LineText(id);
            bool voice = VigiaSettings.Mode != VigiaMode.SoloTexto && VigiaSettings.Mode != VigiaMode.Apagada;
            var clip = voice ? VigiaClips.Get(VigiaCatalog.ClipName(id, 0)) : null;
            _lineStart = Elapsed;
            if (clip != null) { _src.clip = clip; _src.Play(); _lineSeconds = clip.length; }
            else _lineSeconds = Mathf.Max(1.8f, _subtitle.Length / 14f);   // solo texto: tiempo de lectura
            Speaking = clip != null;
        }

        /// <summary>Narración: las frases de la lámina una tras otra (empiezan cuando la lámina ya entró).</summary>
        private void TickNarration()
        {
            var n = Cur.narration;
            if (_narrationDone >= 0f || n == null) return;
            if (_line < 0) { if (Elapsed >= (Cur.fade > 0f ? Cur.fade : IntroText.FadeSeconds)) StartLine(0); return; }
            if (Elapsed - _lineStart < _lineSeconds + 0.3f) return;
            if (_line + 1 < n.Length) StartLine(_line + 1);
            else { _narrationDone = Elapsed; Speaking = false; }
        }

        private void Update()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var k = Keyboard.current; var m = Mouse.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) { Finish(); return; }
            if (_leaveStart >= 0f)
            {
                if (Time.unscaledTime - _leaveStart >= IntroText.LeaveSeconds) { if (_target >= _seq.panels.Count) Finish(); else Show(_target); }
                return;
            }
            TickNarration();
            bool next = (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame))
                        || (m != null && m.leftButton.wasPressedThisFrame && !_overSkip);
            bool back = k != null && k.leftArrowKey.wasPressedThisFrame;
            if (back && _index > 0) Show(_index - 1);
            else if (next) Leave(_index + 1);
            else if (_narrationDone >= 0f && Elapsed >= Mathf.Max(Hold, _narrationDone + 1.2f) - IntroText.LeaveSeconds) Leave(_index + 1);
        }

        private void Leave(int target)
        {
            _target = target; _leaveStart = Time.unscaledTime;
            if (_src != null) _src.Stop();
            Speaking = false;
        }

        /// <summary>Avanza (pruebas y botones): igual que clic o Espacio.</summary>
        public void Next() => Leave(_index + 1);
        public void Back() { if (_index > 0) Show(_index - 1); }

        private void OnGUI()
        {
            GUI.depth = -100;
            float W = Screen.width, H = Screen.height, s = H / 1080f;
            var prev = GUI.color;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            var p = Cur;
            float e = Elapsed;
            float alpha = IntroText.Alpha(e, _leaveStart >= 0f ? Time.unscaledTime - _leaveStart : -1f, p.fade > 0f ? p.fade : IntroText.FadeSeconds);
            if (p.image != null)
            {
                float t = e / Mathf.Max(Hold, (_narrationDone >= 0f ? _narrationDone + 1.2f : Hold));
                float[] uv = p.tour != null && p.tour.Length >= 8 ? IntroText.Tour(Rects(p.tour), t) : IntroText.KenBurns(_index, t);
                // el recorte visible conserva la relación de aspecto de la imagen: letterbox con el ancho/alto del recorte
                float[] fit = IntroText.Fit(p.image.width * uv[2], p.image.height * uv[3], W, H);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTextureWithTexCoords(new Rect(fit[0], fit[1], fit[2], fit[3]), p.image, new Rect(uv[0], 1f - uv[1] - uv[3], uv[2], uv[3]));
            }
            // subtítulos de VIGÍA (solo cuando habla)
            if (!string.IsNullOrEmpty(_subtitle) && _narrationDone < 0f)
            {
                _text ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, richText = true };
                _text.fontSize = Mathf.RoundToInt(32 * s * VigiaSettings.SubtitleScale);
                var box = new Rect(W * 0.16f, H - 200 * s, W * 0.68f, 110 * s);
                GUI.color = new Color(0f, 0f, 0f, 0.68f * alpha); GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(box.x + 24 * s, box.y, box.width - 48 * s, box.height), "<color=#5fd0ff><b>VIGÍA:</b></color> " + _subtitle, _text);
            }
            // puntos de progreso
            if (_seq.panels.Count > 1)
            {
                _dot ??= Dot();
                float d = 10f * s, gap = 12f * s, total = _seq.panels.Count * d + (_seq.panels.Count - 1) * gap, x0 = (W - total) / 2f;
                for (int i = 0; i < _seq.panels.Count; i++)
                {
                    GUI.color = new Color(1f, 1f, 1f, i == _index ? 0.9f : 0.3f);
                    GUI.DrawTexture(new Rect(x0 + i * (d + gap), H - 40 * s, d, d), _dot);
                }
            }
            // SALTAR abajo a la derecha
            _skip ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            _skip.fontSize = Mathf.RoundToInt(22 * s);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            var sr = new Rect(W - 210 * s, H - 80 * s, 180 * s, 50 * s);
            _overSkip = sr.Contains(Event.current.mousePosition);
            if (GUI.Button(sr, "SALTAR", _skip)) Finish();
            GUI.color = prev;
        }

        private static float[][] Rects(float[] f)
        {
            var r = new float[f.Length / 4][];
            for (int i = 0; i < r.Length; i++) r[i] = new[] { f[i * 4], f[i * 4 + 1], f[i * 4 + 2], f[i * 4 + 3] };
            return r;
        }

        private static Texture2D Dot()
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "intro_dot" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
                    px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(15.5f - d));
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        private void Finish()
        {
            Active = false; Speaking = false;
            GameInput.InputCaptured = false;
            if (_paused) { Time.timeScale = 1f; _paused = false; }
            if (_src != null) _src.Stop();
            var done = _onDone; _onDone = null;
            Destroy(gameObject);
            done?.Invoke();
        }

        private void OnDestroy()
        {
            if (_paused) Time.timeScale = 1f;
            if (Active) { Active = false; Speaking = false; GameInput.InputCaptured = false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = false; Speaking = false; _catalog = null; NewGameRequested = false; }
    }
}

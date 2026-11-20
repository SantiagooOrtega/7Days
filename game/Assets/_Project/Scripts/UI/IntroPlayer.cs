using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// Intro en cómic (historia en docs/HISTORIA.md). Lee Resources/Intro/intro.txt y las imágenes intro_01..07 y las muestra con fundidos desde negro,
    /// zoom lento y texto de máquina de escribir; termina con el título "7 DÍAS / Día 1: La ceniza".
    /// La lógica (avanzar, retroceder, completar el texto, saltar, fin) está en IntroSequence, pura y con pruebas; aquí solo se dibuja con IMGUI,
    /// como el resto del HUD, y se reproducen los sonidos.
    /// Se ve una sola vez al empezar la escena Game (PlayerPrefs, también si se salta). El botón "Ver la intro" del menú Esc la repite encima de la partida, en pausa.
    /// </summary>
    [DefaultExecutionOrder(10000)]   // su Update corre después de los demás: al repetirla desde Esc mantiene el tiempo en 0
    public class IntroPlayer : MonoBehaviour
    {
        public const string SeenPrefKey = "intro.seen";
        /// <summary>Solo en esta escena sale la intro al empezar; las pruebas PlayMode crean la arena en otra y no deben quedarse esperando un clic.</summary>
        public const string GameSceneName = "Game";
        private const string Folder = "Intro/";
        private const int NoiseSize = 128;

        /// <summary>La intro que se está viendo ahora, o null.</summary>
        public static IntroPlayer Active { get; private set; }

        public bool Done => _seq == null || _seq.IsDone;

        private IntroSequence _seq;
        private readonly Dictionary<int, Texture2D> _images = new Dictionary<int, Texture2D>();   // número de viñeta -> imagen (solo las que existen)
        private bool _pauseGame, _finished;
        private readonly List<Behaviour> _frozen = new List<Behaviour>();
        private AudioSource _cue, _keys, _hiss;
        private AudioClip _vigia, _ibarra, _key, _title;
        private Texture2D _noise, _skipNormal, _skipHover;
        private Vector2 _noiseOffset;
        private readonly System.Random _rng = new System.Random();
        private int _lastIndex = -1, _lastChars;
        // texto ya preparado de la viñeta actual (no se rehace en cada OnGUI)
        private int _textIndex = -1, _textChars = -1;
        private float _textWidth, _textHeight;
        private string _textShown;
        private GUIStyle _body, _label, _titleStyle, _subtitle, _hint, _hintRight, _skip;

        // ---------------------------------------------------------------- entrada

        /// <summary>
        /// Para el principio de TestArena.Start: si es la escena Game y la intro no se ha visto, la muestra y espera a que termine (o se salte).
        /// En cualquier otro caso no espera nada.
        /// </summary>
        public static IEnumerator PlayIfFirstTime(Component arena)
        {
            if (arena == null || arena.gameObject.scene.name != GameSceneName) yield break;
            if (PlayerPrefs.GetInt(SeenPrefKey, 0) == 1) yield break;
            var intro = Show(false);
            while (intro != null && !intro.Done) yield return null;
        }

        /// <summary>Botón "Ver la intro" del menú Esc: la muestra encima de la partida con el juego en pausa.</summary>
        public static void Replay()
        {
            if (Active == null) Show(true);
        }

        private static IntroPlayer Show(bool pauseGame)
        {
            if (Active != null) return Active;
            var asset = Resources.Load<TextAsset>(Folder + "intro");
            var errors = new List<string>();
            var panels = asset != null ? IntroScript.Parse(asset.text, errors) : new List<IntroPanel>();
            foreach (var e in errors) Debug.LogWarning("[7Days] Resources/Intro/intro.txt, " + e);
            if (panels.Count == 0)
            {
                Debug.LogWarning("[7Days] No hay viñetas en Resources/Intro/intro.txt: se omite la intro.");
                return null;
            }
            var player = new GameObject("Intro").AddComponent<IntroPlayer>();
            player.Begin(panels, pauseGame);
            return player;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = null;

        private void Begin(List<IntroPanel> panels, bool pauseGame)
        {
            Active = this;
            _pauseGame = pauseGame;

            // imágenes: se cargan todas al empezar (con la pantalla en negro) para que avanzar y retroceder no den tirones; se descargan al terminar
            var missing = new List<string>();
            foreach (var p in panels)
            {
                if (_images.ContainsKey(p.Number)) continue;
                var tex = Resources.Load<Texture2D>(Folder + p.ImageName);
                if (tex != null) _images[p.Number] = tex;
                else if (!missing.Contains(p.ImageName)) missing.Add(p.ImageName);
            }
            if (missing.Count > 0) Debug.Log("[7Days] Intro: faltan " + string.Join(", ", missing) + " en Resources/Intro; esas viñetas se ven en negro con estática y su texto.");
            _seq = new IntroSequence(panels, p => _images.ContainsKey(p.Number));

            _cue = NewSource(0.9f);
            _keys = NewSource(0.35f);
            _hiss = gameObject.AddComponent<AudioSource>();   // su volumen cambia cada frame: se multiplica a mano por el de la interfaz
            _hiss.playOnAwake = false; _hiss.spatialBlend = 0f; _hiss.loop = true; _hiss.volume = 0f; _hiss.ignoreListenerPause = true;
            _hiss.clip = IntroAudio.Static();
            _hiss.Play();
            _vigia = IntroAudio.Vigia();
            _ibarra = IntroAudio.Ibarra();
            _key = IntroAudio.Key();
            _title = IntroAudio.Title();

            _noise = MakeNoise();
            if (pauseGame) Freeze(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private AudioSource NewSource(float volume)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;   // al repetirla en pausa el resto del audio está detenido
            return AudioMix.Route(src, AudioGroup.UI, volume);
        }

        /// <summary>
        /// Repetición desde el menú Esc: la partida se congela (tiempo a 0, audio del mundo en pausa, entrada capturada). Se apagan mientras dura
        /// WeaponHud (fija Time.timeScale en cada frame), PlayerMotor (guardaría el Espacio como un salto pendiente) y DayLoop (Enter empezaría la oleada).
        /// </summary>
        private void Freeze(bool on)
        {
            if (on)
            {
                FreezeAll<WeaponHud>();
                FreezeAll<SevenDays.Player.PlayerMotor>();
                FreezeAll<SevenDays.World.DayLoop>();
                Time.timeScale = 0f;
                AudioListener.pause = true;
                GameInput.InputCaptured = true;
                return;
            }
            foreach (var b in _frozen) if (b != null) b.enabled = true;
            _frozen.Clear();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.InputCaptured = false;
        }

        private void FreezeAll<T>() where T : Behaviour
        {
            foreach (var b in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (b.enabled) { b.enabled = false; _frozen.Add(b); }
        }

        // ---------------------------------------------------------------- bucle

        private void Update()
        {
            if (_seq == null || _finished) return;
            if (_pauseGame) { Time.timeScale = 0f; GameInput.InputCaptured = true; }

            var k = Keyboard.current;
            if (k != null)
            {
                if (k.escapeKey.wasPressedThisFrame) _seq.Skip();
                else if (k.spaceKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) _seq.Advance();
                else if (k.leftArrowKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) _seq.Back();
            }
            _seq.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));   // tiempo real: también corre con el juego en pausa
            _noiseOffset = new Vector2((float)_rng.NextDouble(), (float)_rng.NextDouble());
            UpdateSound();
            if (_seq.IsDone) Finish();
        }

        private void UpdateSound()
        {
            if (_seq.Index != _lastIndex)
            {
                _lastIndex = _seq.Index;
                _lastChars = 0;
                var p = _seq.Current;
                // el sonido depende de quién habla, aunque el texto venga dibujado en la imagen
                if (_seq.OnTitle) _cue.PlayOneShot(_title);
                else if (p != null && p.Speaker == IntroSpeaker.Vigia) _cue.PlayOneShot(_vigia);
                else if (p != null && p.Speaker == IntroSpeaker.Ibarra) _cue.PlayOneShot(_ibarra);
            }
            if (_seq.Stage == IntroStage.Typing && _seq.VisibleChars > _lastChars)
            {
                // un golpe de tecla cada dos letras, nunca en un espacio; más agudo para VIGÍA y más grave para Ibarra
                var p = _seq.Current;
                if (_seq.VisibleChars / 2 != _lastChars / 2 && p.Text[_seq.VisibleChars - 1] != ' ')
                {
                    float pitch = p.Speaker == IntroSpeaker.Vigia ? 1.6f : p.Speaker == IntroSpeaker.Ibarra ? 0.75f : 1f;
                    _keys.pitch = pitch * (0.94f + 0.12f * (float)_rng.NextDouble());
                    _keys.PlayOneShot(_key);
                }
            }
            _lastChars = _seq.VisibleChars;

            // estática de fondo: suave en las viñetas sin imagen y algo más fuerte con la radio de Ibarra
            float hiss = 0f;
            var cur = _seq.Current;
            if (cur != null && !_seq.HasImage) hiss = 0.35f;
            if (cur != null && cur.Speaker == IntroSpeaker.Ibarra) hiss = Mathf.Max(hiss, 0.5f);
            _hiss.volume = hiss * _seq.Alpha * AudioMix.Volume(AudioGroup.UI);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            PlayerPrefs.SetInt(SeenPrefKey, 1);   // vista (o saltada): Reintentar y Jugar otra vez recargan la escena y ya no la repiten
            PlayerPrefs.Save();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (_pauseGame) Freeze(false);   // también si la escena cambia a mitad de la intro
            foreach (var t in _images.Values) if (t != null) Resources.UnloadAsset(t);
            _images.Clear();
            if (_noise != null) Destroy(_noise);
            if (_skipNormal != null) Destroy(_skipNormal);
            if (_skipHover != null) Destroy(_skipHover);
        }

        // ---------------------------------------------------------------- dibujo

        private void OnGUI()
        {
            if (_seq == null || _finished) return;
            GUI.depth = -1000;   // encima del HUD y del menú Esc
            EnsureStyles();
            var e = Event.current;
            float s = Screen.height / 1080f;   // se dibuja en una pantalla virtual de 1080 de alto, como DayLoop
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = 1080f;

            Fill(new Rect(0f, 0f, w, h), Color.black);
            if (!_seq.IsDone)
            {
                float a = _seq.Alpha;
                if (_seq.OnTitle) DrawTitle(w, h, a);
                else DrawPanel(w, h, a);
                DrawChrome(w, h);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(w - 236f, 36f, 200f, 54f), "Saltar  (Esc)", _skip)) _seq.Skip();
                if (e.type == EventType.MouseDown && e.button == 0) { _seq.Advance(); e.Use(); }
            }
            // lo de debajo (el menú Esc al repetirla) no recibe clics ni rueda mientras se ve la intro
            if (e.isMouse || e.type == EventType.ScrollWheel) e.Use();
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawPanel(float w, float h, float a)
        {
            var p = _seq.Current;
            if (_images.TryGetValue(p.Number, out var tex))
            {
                float t = IntroLayout.Ease(_seq.PanelTime);
                // IMAGEN: entera (letterbox) y zoom de 5 % como máximo, para no cortar el texto dibujado.
                // UNITY: llena la pantalla con zoom y paneo; la dirección alterna entre viñetas.
                var r = p.Source == IntroTextSource.Image
                    ? IntroLayout.Fit(w, h, tex.width, tex.height, t)
                    : IntroLayout.Cover(w, h, tex.width, tex.height, t, IntroLayout.UnityZoom, p.Number % 2 == 0 ? 0.7f : -0.7f, -0.35f);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(r.X, r.Y, r.W, r.H), tex);
            }
            else DrawStatic(w, h, 0.1f * a);
            if (_seq.ShowsText) DrawTextBox(w, h, p, a, !_seq.HasImage);
        }

        private void DrawStatic(float w, float h, float alpha)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            var uv = new Rect(_noiseOffset.x, _noiseOffset.y, w / (NoiseSize * 3f), h / (NoiseSize * 3f));   // grano de 3 px
            GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, w, h), _noise, uv);
        }

        /// <summary>Recuadro de texto según quién habla. Sin imagen va centrado; con imagen, abajo.</summary>
        private void DrawTextBox(float w, float h, IntroPanel p, float a, bool centered)
        {
            const float pad = 30f;
            float bw = Mathf.Min(1300f, w - 200f);
            string label = IntroScript.Label(p.Speaker);
            if (_textIndex != _seq.Index || !Mathf.Approximately(_textWidth, bw))
            {
                _textIndex = _seq.Index;
                _textWidth = bw;
                _textChars = -1;
                _textHeight = _body.CalcHeight(new GUIContent(p.Text), bw - pad * 2f);   // alto del texto completo: el recuadro no crece mientras se escribe
            }
            if (_textChars != _seq.VisibleChars)
            {
                _textChars = _seq.VisibleChars;
                _textShown = Typed(p.Text, _textChars);
            }

            Color bg, fg, edge;
            switch (p.Speaker)
            {
                case IntroSpeaker.Vigia: bg = new Color(0f, 0.06f, 0.08f, 0.9f); fg = new Color(0.42f, 0.93f, 1f); edge = new Color(0.3f, 0.85f, 1f, 0.9f); break;
                case IntroSpeaker.Ibarra: bg = new Color(0.07f, 0.09f, 0.04f, 0.92f); fg = new Color(0.71f, 0.79f, 0.5f); edge = new Color(0.45f, 0.53f, 0.25f, 0.95f); break;
                default: bg = new Color(0f, 0f, 0f, 0.88f); fg = Color.white; edge = new Color(1f, 1f, 1f, 0.15f); break;
            }
            float lh = label != null ? 46f : 0f;
            float bh = _textHeight + pad * 2f + lh;
            var box = new Rect((w - bw) * 0.5f, centered ? (h - bh) * 0.5f : h - bh - 120f, bw, bh);
            Fill(box, new Color(bg.r, bg.g, bg.b, bg.a * a));
            Border(box, 2f, new Color(edge.r, edge.g, edge.b, edge.a * a));
            GUI.color = new Color(1f, 1f, 1f, a);
            if (label != null)
            {
                _label.normal.textColor = fg;
                GUI.Label(new Rect(box.x + pad, box.y + pad - 8f, bw - pad * 2f, 40f), label, _label);
            }
            _body.normal.textColor = fg;
            GUI.Label(new Rect(box.x + pad, box.y + pad + lh, bw - pad * 2f, _textHeight + 8f), _textShown, _body);
        }

        /// <summary>La parte aún no escrita va transparente: así las palabras no saltan de línea mientras se escriben.</summary>
        private static string Typed(string text, int visible)
        {
            if (visible >= text.Length) return text;
            return text.Substring(0, visible) + "<color=#00000000>" + text.Substring(visible) + "</color>";
        }

        private void DrawTitle(float w, float h, float a)
        {
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0f, h * 0.5f - 230f, w, 220f), Story.GameTitle, _titleStyle);
            Fill(new Rect(w * 0.5f - 260f, h * 0.5f + 8f, 520f, 4f), new Color(0.62f, 0.08f, 0.06f, 0.9f * a));
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0f, h * 0.5f + 34f, w, 70f), Story.DayHeading(1), _subtitle);
        }

        /// <summary>Barra de progreso discreta, ayuda de controles y "Continuar" cuando la viñeta espera al jugador.</summary>
        private void DrawChrome(float w, float h)
        {
            Fill(new Rect(0f, h - 5f, w, 5f), new Color(1f, 1f, 1f, 0.08f));
            Fill(new Rect(0f, h - 5f, w * _seq.Progress, 5f), new Color(1f, 1f, 1f, 0.35f));
            GUI.color = Color.white;
            GUI.Label(new Rect(36f, h - 64f, 900f, 40f), "Clic / Espacio: seguir     ←: atrás     Esc: saltar", _hint);
            if (_seq.Stage == IntroStage.Hold && _seq.WaitsForPlayer)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f));
                GUI.Label(new Rect(w - 436f, h - 64f, 400f, 40f), "Continuar  »", _hintRight);
            }
        }

        private void EnsureStyles()
        {
            if (_body != null) return;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 34, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 170, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, normal = { textColor = new Color(0.94f, 0.92f, 0.87f) } };
            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.8f, 0.76f, 0.68f) } };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.LowerLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.45f) } };
            _hintRight = new GUIStyle(_hint) { fontSize = 24, alignment = TextAnchor.LowerRight, normal = { textColor = Color.white } };
            _skipNormal = MakeTex(new Color(0f, 0f, 0f, 0.55f));
            _skipHover = MakeTex(new Color(1f, 1f, 1f, 0.2f));
            _skip = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = _skipNormal, textColor = new Color(1f, 1f, 1f, 0.85f) },
                hover = { background = _skipHover, textColor = Color.white },
                active = { background = _skipHover, textColor = Color.white },
            };
        }

        private static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        private static void Border(Rect r, float t, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static Texture2D MakeNoise()
        {
            var tex = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "intro_estatica" };
            var px = new Color32[NoiseSize * NoiseSize];
            var rng = new System.Random(1234);
            for (int i = 0; i < px.Length; i++) { byte v = (byte)rng.Next(256); px[i] = new Color32(v, v, v, 255); }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}

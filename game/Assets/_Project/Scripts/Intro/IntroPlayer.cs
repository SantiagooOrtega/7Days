using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Core;
using SevenDays.Procedural;
using SevenDays.Vigia;

namespace SevenDays.Intro
{
    /// <summary>
    /// Reproduce la intro en cómic a pantalla completa (IMGUI): fundido entre viñetas, movimiento lento de cámara sobre la imagen, texto con máquina de escribir y
    /// narración de VIGÍA (voz con radio). Clic o Espacio: completa el texto o pasa a la siguiente; Esc o "Saltar": la omite. Al terminar se guarda que ya se vio
    /// (PlayerPrefs) y no vuelve a salir hasta que se pida desde Opciones (Esc > Ver la intro otra vez).
    /// </summary>
    public class IntroPlayer : MonoBehaviour
    {
        public const string SeenKey = "intro.seen";
        public static bool Active { get; private set; }
        public static bool Seen { get => PlayerPrefs.GetInt(SeenKey, 0) == 1; set => PlayerPrefs.SetInt(SeenKey, value ? 1 : 0); }
        /// <summary>Sale si no se ha visto. En batch (pruebas automáticas) nunca, para no bloquear la partida.</summary>
        public static bool ShouldPlay => !Seen && !Application.isBatchMode;

        private IntroSequence _seq;
        private int _index;
        private float _panelStart, _fade = 1f;
        private bool _typed;
        private AudioSource _src;
        private GUIStyle _text, _skip;

        public static IntroPlayer Play(Transform parent = null)
        {
            var go = new GameObject("Intro");
            if (parent != null) go.transform.SetParent(parent, false);
            var p = go.AddComponent<IntroPlayer>();
            p._seq = IntroSequence.Load();
            p._src = go.AddComponent<AudioSource>();
            p._src.playOnAwake = false; p._src.spatialBlend = 0f; p._src.priority = 0;
            AudioMix.Route(p._src, AudioGroup.Vigia, 1f);
            Active = p._seq.panels.Count > 0;
            if (!Active) { Destroy(go); return null; }
            GameInput.InputCaptured = true;
            p._paused = SevenDays.World.DayLoop.Instance != null;   // vista otra vez desde Opciones a mitad de partida: el juego espera
            if (p._paused) Time.timeScale = 0f;
            p.Show(0);
            return p;
        }

        private void Show(int i)
        {
            _index = i; _panelStart = Time.unscaledTime; _typed = false;
            _src.Stop();
            var id = _seq.panels[i].narrationId;
            var clip = !string.IsNullOrEmpty(id) && VigiaSettings.Mode != VigiaMode.SoloTexto && VigiaSettings.Mode != VigiaMode.Apagada ? VigiaClips.Get(VigiaCatalog.ClipName(id, 0)) : null;
            if (clip != null) { _src.clip = clip; _src.Play(); }
        }

        private float Elapsed => Time.unscaledTime - _panelStart;
        private string Line => _seq.panels[_index].text ?? "";

        private void Update()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var k = Keyboard.current; var m = Mouse.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) { Finish(); return; }
            bool next = (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) || (m != null && m.leftButton.wasPressedThisFrame && !_overSkip);
            bool typingDone = _typed || IntroText.Visible(Line, Elapsed) >= Line.Length;
            if (next) { if (!typingDone) _typed = true; else Advance(); }
            else if (typingDone && Elapsed > IntroText.PanelSeconds(Line, _src.clip != null && _src.isPlaying ? _src.clip.length : 0f)) Advance();
        }

        private void Advance()
        {
            if (_index + 1 >= _seq.panels.Count) { Finish(); return; }
            Show(_index + 1);
        }

        private bool _overSkip, _paused;
        private void OnGUI()
        {
            GUI.depth = -100;
            float W = Screen.width, H = Screen.height;
            var prev = GUI.color;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            var p = _seq.panels[_index];
            float e = Elapsed;
            float alpha = Mathf.Clamp01(e / 0.7f);
            if (p.image != null)
            {
                // movimiento lento: zoom del 100 % al 110 % y desplazamiento suave
                float z = 1f / (1f + 0.1f * Mathf.Clamp01(e / 12f));
                float ox = (1f - z) * (0.5f + 0.5f * Mathf.Sin(_index * 1.7f)), oy = (1f - z) * 0.5f;
                float imgAspect = p.image.width / (float)p.image.height, scr = W / H;
                Rect dst = imgAspect > scr ? new Rect(0, (H - W / imgAspect) / 2f, W, W / imgAspect) : new Rect((W - H * imgAspect) / 2f, 0, H * imgAspect, H);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTextureWithTexCoords(dst, p.image, new Rect(ox, oy, z, z));
            }
            float s = H / 1080f;
            _text ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, richText = true };
            _text.fontSize = Mathf.RoundToInt(34 * s * VigiaSettings.SubtitleScale);
            string shown = _typed ? Line : Line.Substring(0, IntroText.Visible(Line, e));
            var box = new Rect(W * 0.12f, H - 230 * s, W * 0.76f, 150 * s);
            GUI.color = new Color(0f, 0f, 0f, 0.7f * alpha); GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(box.x + 24 * s, box.y, box.width - 48 * s, box.height), "<color=#5fd0ff><b>VIGÍA:</b></color> " + shown, _text);
            _skip ??= new GUIStyle(GUI.skin.button);
            _skip.fontSize = Mathf.RoundToInt(22 * s);
            GUI.color = Color.white;
            var sr = new Rect(W - 230 * s, 30 * s, 200 * s, 54 * s);
            _overSkip = sr.Contains(Event.current.mousePosition);
            if (GUI.Button(sr, "Saltar (Esc)", _skip)) Finish();
            GUI.Label(new Rect(30 * s, 30 * s, 400 * s, 40 * s), (_index + 1) + " / " + _seq.panels.Count + "   ·   clic o Espacio: seguir", new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * s) });
            GUI.color = prev;
        }

        private void Finish()
        {
            Seen = true;
            Active = false;
            GameInput.InputCaptured = false;
            if (_paused) { Time.timeScale = 1f; _paused = false; }
            if (_src != null) _src.Stop();
            Destroy(gameObject);
        }

        private void OnDestroy() { if (_paused) Time.timeScale = 1f; if (Active) { Active = false; GameInput.InputCaptured = false; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = false;
    }
}

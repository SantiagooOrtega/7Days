using System;
using UnityEngine;
using UnityEngine.UI;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.UI.Tactical;
using SevenDays.World;

namespace SevenDays.UI
{
    public enum PauseScreen { Main, Options, Controls, ConfirmMenu, ConfirmQuit }

    /// <summary>
    /// Menú de pausa (Esc en partida) con el estilo del menú principal: fondo oscuro, panel de metal y filas grandes. Pausa el juego (Time.timeScale = 0),
    /// pausa el audio del juego (la música y la interfaz siguen) y suelta el ratón; el HUD se oculta (HudGate). Esc vuelve atrás o reanuda.
    /// No se abre durante la intro o el epílogo, en la pantalla de muerte/victoria, con el selector de armas abierto ni con el jugador muerto.
    /// Opciones: las mismas del antiguo panel de ajustes (sensibilidad, movimiento de cámara, apuntar, calidad, volúmenes, VIGÍA, subtítulos, intro, controles).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;
        /// <summary>Acciones finales (las pruebas las sustituyen para no cerrar el juego ni recargar la escena).</summary>
        public static Action QuitAction = Application.Quit, ToMenuAction = DayLoop.ToMenu, RetryDayAction = DayLoop.RetryDay;

        public const int Order = 850;
        public const float MainW = 720f, OptionsW = 1180f, RowH = 64f, OptionsRowH = 46f;
        public const int RowSize = 36, OptionsSize = 28, ValueSize = 26;

        public static readonly string[] MainItems = { "Reanudar", "Opciones", "Reiniciar día", "Volver al menú principal", "Salir del juego" };
        public static readonly string[] OptionItems = { "Sensibilidad", "Movimiento de cámara", "Apuntar", "Calidad", "Volumen general", "Zombis", "Armas", "Ambiente",
            "Interfaz", "Voz de VIGÍA", "Música", "Música encendida", "Modo de VIGÍA", "Subtítulos", "Tamaño de texto", "Ver la intro otra vez", "Controles", "Volver" };

        private RectTransform _canvas, _screen;
        private MenuList _list;
        private PlayerCamera _camera;
        private Health _playerHealth;
        private bool _open;
        private int _introFrame = -100;
        public PauseScreen Current { get; private set; }
        public MenuList List => _list;

        public static PauseMenu Create(Transform parent, PlayerCamera cam, Health playerHealth)
        {
            var go = new GameObject("Menú de pausa");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PauseMenu>();
            p._camera = cam; p._playerHealth = playerHealth;
            Instance = p;
            return p;
        }

        private void OnDestroy() { if (Instance == this) { if (_open) Restore(); Instance = null; } }

        private bool CanOpen =>
            !HudGate.Hidden && Time.frameCount - _introFrame > 2 && !StatsScreen.Blocking && !FrontEnd.Blocking && !GameInput.InputCaptured
            && (_playerHealth == null || !_playerHealth.IsDead);

        private void Update()
        {
            if (SevenDays.Intro.IntroPlayer.Active) _introFrame = Time.frameCount;   // el Esc que salta la intro no abre la pausa
            if (_open) { if (StatsScreen.Blocking || (_playerHealth != null && _playerHealth.IsDead)) Close(); return; }   // la lista atiende Esc (Back)
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame && CanOpen) Open();
        }

        // ------------------------------------------------------------ abrir, cerrar
        public void Open()
        {
            if (_open) return;
            _open = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;   // el audio del juego se detiene; la música y la interfaz ignoran la pausa
            GameInput.InputCaptured = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (_canvas == null)
            {
                _canvas = UiKit.Canvas(transform, "Canvas pausa", Order);
                var bg = UiKit.Image(_canvas, "Fondo pausa", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), new Color(0.02f, 0.015f, 0.015f, 0.82f));
                bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one; bg.rectTransform.sizeDelta = Vector2.zero;
            }
            _canvas.gameObject.SetActive(true);
            Show(PauseScreen.Main);
            UiSounds.Accept();
        }

        /// <summary>Reanuda: tiempo, audio, cursor bloqueado y entrada del juego.</summary>
        public void Close()
        {
            if (!_open) return;
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Restore();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        private static void Restore()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.InputCaptured = false;
        }

        // ------------------------------------------------------------ pantallas
        public void Show(PauseScreen s)
        {
            if (_canvas == null) { _canvas = UiKit.Canvas(transform, "Canvas pausa", Order); }
            if (_screen != null) { if (Application.isPlaying) Destroy(_screen.gameObject); else DestroyImmediate(_screen.gameObject); }
            _screen = UiKit.Stretch(_canvas, "Pantalla " + s);
            Current = s;
            switch (s)
            {
                case PauseScreen.Main: BuildMain(); break;
                case PauseScreen.Options: BuildOptions(); break;
                case PauseScreen.Controls: BuildControls(); break;
                default: BuildConfirm(s); break;
            }
        }

        private RectTransform Panel(string name, float w, float h, string header)
        {
            var p = UiKit.Panel(_screen, "Panel " + name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            UiKit.Header(p, header, new Vector2(0f, h / 2f - 44f), w - 40f);
            return p;
        }

        private void BuildMain()
        {
            float h = MainItems.Length * RowH + 130f;
            var p = Panel("pausa", MainW, h, "Pausa");
            _list = MenuList.Create(p, Vector2.zero, MainW - 60f, MainItems, null, RowH, RowSize, ValueSize);
            _list.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -40f);
            _list.AddSeparator(2);
            _list.Confirmed += i =>
            {
                switch (i)
                {
                    case 0: Close(); break;
                    case 1: Show(PauseScreen.Options); break;
                    case 2: Close(); RetryDayAction?.Invoke(); break;
                    case 3: Show(PauseScreen.ConfirmMenu); break;
                    default: Show(PauseScreen.ConfirmQuit); break;
                }
            };
            _list.Back += Close;
            UiKit.Footer(_screen, "↑↓ Elegir · Enter Aceptar · Esc Reanudar");
        }

        private void BuildConfirm(PauseScreen s)
        {
            bool quit = s == PauseScreen.ConfirmQuit;
            var p = Panel("confirmar", MainW, 2 * RowH + 200f, quit ? "¿Salir del juego?" : "¿Volver al menú?");
            UiKit.Text(p, "Se pierde el progreso del día en curso.", 26, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(MainW - 80f, 40f), false, false);
            _list = MenuList.Create(p, Vector2.zero, MainW - 60f, new[] { "No", "Sí" }, null, RowH, RowSize, ValueSize);
            _list.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -70f);
            _list.Confirmed += i =>
            {
                if (i == 0) { Show(PauseScreen.Main); return; }
                Close();
                if (quit) QuitAction?.Invoke(); else ToMenuAction?.Invoke();
            };
            _list.Back += () => Show(PauseScreen.Main);
            UiKit.Footer(_screen, "↑↓ Elegir · Enter Aceptar · Esc Volver");
        }

        private void BuildControls()
        {
            float w = 1500f, h = 620f;
            var p = Panel("controles", w, h, "Controles");
            var t = UiKit.Text(p, PlayerHud.HelpText, 26, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, 10f), new Vector2(w - 80f, h - 220f), false, false);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.lineSpacing = 1.15f;
            _list = MenuList.Create(p, Vector2.zero, 400f, new[] { "Volver" }, null, RowH, RowSize, ValueSize);
            _list.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -h / 2f + 60f);
            _list.Confirmed += _ => Show(PauseScreen.Options); _list.Back += () => Show(PauseScreen.Options);
            UiKit.Footer(_screen, "Enter Volver · Esc Volver");
        }

        private void BuildOptions()
        {
            float h = OptionItems.Length * OptionsRowH + 130f;
            var p = Panel("opciones", OptionsW, h, "Opciones");
            var values = new string[OptionItems.Length]; for (int i = 0; i < values.Length; i++) values[i] = "";
            _list = MenuList.Create(p, Vector2.zero, OptionsW - 60f, OptionItems, values, OptionsRowH, OptionsSize, ValueSize);
            _list.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -40f);
            _list.EnableArrows(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
            Refresh();
            _list.Adjusted += (row, dir) => { Adjust(row, dir); Refresh(); };
            _list.Confirmed += i =>
            {
                if (i == 15) { Close(); SevenDays.Intro.IntroPlayer.Play(); return; }   // ver la intro otra vez (se puede saltar con SALTAR o Esc)
                if (i == 16) { Show(PauseScreen.Controls); return; }
                if (i == OptionItems.Length - 1) { Show(PauseScreen.Main); return; }
                Adjust(i, 1); Refresh();
            };
            _list.Back += () => Show(PauseScreen.Main);
            UiKit.Footer(_screen, "↑↓ Elegir · ←→ Cambiar · Enter Aceptar · Esc Volver");
        }

        private SevenDays.Procedural.CameraSettingsData Cam => _camera != null ? _camera.Settings : null;
        private static string Pct(float v) => Mathf.RoundToInt(v * 100f) + " %";

        private void Refresh()
        {
            var c = Cam; var q = SevenDays.Look.QualityDirector.Instance;
            _list.SetValue(0, c != null ? Pct((c.sensitivity - 0.02f) / 0.38f) : "-");
            _list.SetValue(1, c != null ? Pct(c.motionScale) : "-");
            _list.SetValue(2, c != null ? (c.aimToggle ? "Alternar" : "Mantener") : "-");
            _list.SetValue(3, q != null ? q.Mode.ToString() : "-");
            _list.SetValue(4, Pct(AudioMix.Master));
            _list.SetValue(5, Pct(AudioMix.GroupVolume(AudioGroup.Zombies))); _list.SetValue(6, Pct(AudioMix.GroupVolume(AudioGroup.Weapons)));
            _list.SetValue(7, Pct(AudioMix.GroupVolume(AudioGroup.Ambient))); _list.SetValue(8, Pct(AudioMix.GroupVolume(AudioGroup.UI)));
            _list.SetValue(9, Pct(AudioMix.GroupVolume(AudioGroup.Vigia))); _list.SetValue(10, Pct(AudioMix.GroupVolume(AudioGroup.Music)));
            _list.SetValue(11, AudioMix.MusicOn ? "Sí" : "No");
            _list.SetValue(12, SevenDays.Vigia.VigiaSettings.ModeNames[(int)SevenDays.Vigia.VigiaSettings.Mode]);
            _list.SetValue(13, SevenDays.Vigia.VigiaSettings.Subtitles ? "Sí" : "No");
            _list.SetValue(14, Pct(SevenDays.Vigia.VigiaSettings.SubtitleScale));
        }

        private void Adjust(int row, int dir)
        {
            var c = Cam; var q = SevenDays.Look.QualityDirector.Instance;
            float Step(float v) => Mathf.Clamp01(Mathf.Round((v + 0.05f * dir) * 20f) / 20f);
            switch (row)
            {
                case 0: if (c != null) { c.sensitivity = Mathf.Clamp(c.sensitivity + 0.019f * dir, 0.02f, 0.4f); PlayerPrefs.SetFloat(CameraSettings.SensitivityPrefKey, c.sensitivity); } break;
                case 1: if (c != null) { c.motionScale = Step(c.motionScale); PlayerPrefs.SetFloat(CameraSettings.MotionPrefKey, c.motionScale); } break;
                case 2: if (c != null) { c.aimToggle = !c.aimToggle; PlayerPrefs.SetInt(CameraSettings.AimTogglePrefKey, c.aimToggle ? 1 : 0); } break;
                case 3: if (q != null) q.SetMode((SevenDays.Look.QualityMode)(((int)q.Mode + dir + 4) % 4)); break;
                case 4: AudioMix.Master = Step(AudioMix.Master); break;
                case 5: AudioMix.SetGroupVolume(AudioGroup.Zombies, Step(AudioMix.GroupVolume(AudioGroup.Zombies))); break;
                case 6: AudioMix.SetGroupVolume(AudioGroup.Weapons, Step(AudioMix.GroupVolume(AudioGroup.Weapons))); break;
                case 7: AudioMix.SetGroupVolume(AudioGroup.Ambient, Step(AudioMix.GroupVolume(AudioGroup.Ambient))); break;
                case 8: AudioMix.SetGroupVolume(AudioGroup.UI, Step(AudioMix.GroupVolume(AudioGroup.UI))); break;
                case 9: AudioMix.SetGroupVolume(AudioGroup.Vigia, Step(AudioMix.GroupVolume(AudioGroup.Vigia))); break;
                case 10: AudioMix.SetGroupVolume(AudioGroup.Music, Step(AudioMix.GroupVolume(AudioGroup.Music))); break;
                case 11: AudioMix.MusicOn = !AudioMix.MusicOn; break;
                case 12: SevenDays.Vigia.VigiaSettings.Mode = (SevenDays.Procedural.VigiaMode)(((int)SevenDays.Vigia.VigiaSettings.Mode + dir + 4) % 4); break;
                case 13: SevenDays.Vigia.VigiaSettings.Subtitles = !SevenDays.Vigia.VigiaSettings.Subtitles; break;
                case 14: SevenDays.Vigia.VigiaSettings.SubtitleScale += 0.1f * dir; break;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; QuitAction = Application.Quit; ToMenuAction = DayLoop.ToMenu; RetryDayAction = DayLoop.RetryDay; }
    }
}

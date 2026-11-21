using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SevenDays.Animation;
using SevenDays.Net;
using SevenDays.Procedural;
using SevenDays.UI.Tactical;
using SevenDays.World;

namespace SevenDays.UI
{
    public enum FrontScreen { Login, Menu, Select, Ranking, Profile, Options, Credits }

    /// <summary>
    /// Menú y pantallas previas a la partida, dentro de la escena Game (no hay escenas a mano): Login/Registro -> Menú -> Selección de personaje y mapa -> Intro ->
    /// Juego. Mientras se muestra, la arena espera (Blocking). Ranking (backend con sesión, si no el local), Perfil (historial local), Opciones y Créditos.
    /// Modo prueba sin sesión. "Reintentar" salta el menú; "Menú" desde la partida vuelve aquí. Estilo común: UI.Tactical.
    /// </summary>
    public class FrontEnd : MonoBehaviour
    {
        public static FrontEnd Instance { get; private set; }
        public static bool Blocking => Instance != null && Instance._open;
        /// <summary>La próxima carga de la escena empieza directamente en la partida (Reintentar / Reintentar día).</summary>
        public static bool SkipNext;
        /// <summary>La próxima carga abre el menú principal (sin pasar por el login si ya hay sesión o modo prueba).</summary>
        public static bool TestMode;

        private bool _open = true;
        private RectTransform _canvas, _screen;
        private FrontScreen _current;
        private MenuList _list;
        private InputField _email, _user, _pass;
        private Text _info, _status, _notice;
        private Image _preview, _soldier;
        private bool _register, _busy;
        private float _noticeAt;
        private int _variant, _map;
        private readonly List<Object> _owned = new List<Object>();

        public FrontScreen Current => _current;
        public MenuList List => _list;

        public static readonly string[] MenuItems = { "Jugar", "Continuar", "Ranking", "Perfil", "Opciones", "Créditos", "Salir" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

        private static void OnLoaded(Scene s, LoadSceneMode m)
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (s.name != "Game" || Instance != null) return;
            if (SkipNext) { SkipNext = false; return; }
            var go = new GameObject("FrontEnd");
            Instance = go.AddComponent<FrontEnd>();
            Instance.Open(Session.IsLoggedIn || TestMode ? FrontScreen.Menu : FrontScreen.Login);
        }

        /// <summary>Abre el front-end (también lo usan las pruebas).</summary>
        public static FrontEnd Show(FrontScreen screen)
        {
            if (Instance == null) Instance = new GameObject("FrontEnd").AddComponent<FrontEnd>();
            Instance._open = true; Instance.Open(screen);
            return Instance;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; foreach (var o in _owned) if (o != null) Destroy(o); }

        private void Open(FrontScreen screen)
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (_canvas == null)
            {
                _canvas = UiKit.Canvas(transform, "Canvas menú", 900);
                FlameBackdrop.Create(_canvas, Color.white);
                _variant = Mathf.Max(0, IndexOfVariant(CharacterVariantLibrary.Current.id));
                _map = Mathf.Max(0, System.Array.IndexOf(MapIO.PlayableIds, MapIO.SelectedId));
            }
            if (_screen != null) Destroy(_screen.gameObject);
            _screen = UiKit.Stretch(_canvas, "Pantalla " + screen);
            _current = screen; _list = null; _email = _user = _pass = null; _preview = _soldier = null;
            switch (screen)
            {
                case FrontScreen.Login: BuildLogin(); break;
                case FrontScreen.Menu: BuildMenu(); break;
                case FrontScreen.Select: BuildSelect(); break;
                case FrontScreen.Ranking: BuildRanking(); break;
                case FrontScreen.Profile: BuildProfile(); break;
                case FrontScreen.Options: BuildOptions(); break;
                default: BuildCredits(); break;
            }
        }

        private static int IndexOfVariant(string id) { var all = CharacterVariantLibrary.All; for (int i = 0; i < all.Count; i++) if (all[i].id == id) return i; return 0; }

        // ---------------------------------------------------------------- piezas comunes
        private void Title(string sub)
        {
            var t = UiKit.Text(_screen, "7DAYS", 150, UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(-560f, 410f), new Vector2(900f, 170f), true);
            // marcas de stencil: cortes horizontales a través del título
            foreach (float y in new[] { 395f, 440f }) UiKit.Image(_screen, "Stencil", new Vector2(0.5f, 0.5f), new Vector2(-640f, y), new Vector2(640f, 6f), new Color(0.35f, 0.04f, 0.03f, 0.9f));
            UiKit.Text(_screen, sub, 34, UiKit.Orange, TextAnchor.MiddleLeft, new Vector2(-560f, 320f), new Vector2(900f, 44f), true);
        }

        private void Notice(string text) { _notice = UiKit.Text(_screen, text, 22, UiKit.Orange, TextAnchor.MiddleRight, new Vector2(560f, 470f), new Vector2(700f, 34f), true); _noticeAt = Time.unscaledTime; }

        private RectTransform InfoPanel(string header, out Text body)
        {
            var p = UiKit.Panel(_screen, "Panel info", new Vector2(0.5f, 0.5f), new Vector2(430f, -20f), new Vector2(820f, 560f));
            UiKit.Header(p, header, new Vector2(0f, 245f), 780f);
            body = UiKit.Text(p, "", 26, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, -30f), new Vector2(740f, 440f), false, false);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            return p;
        }

        private MenuList LeftList(string header, IList<string> items, IList<string> values = null, float width = 620f)
        {
            var p = UiKit.Panel(_screen, "Panel lista", new Vector2(0.5f, 0.5f), new Vector2(-470f, -20f), new Vector2(width + 60f, 560f));
            UiKit.Header(p, header, new Vector2(0f, 245f), width + 20f);
            var l = MenuList.Create(p, new Vector2(0f, 200f - items.Count * MenuList.RowHeight / 2f), width, items, values);
            l.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 200f - items.Count * MenuList.RowHeight / 2f);
            return l;
        }

        // ---------------------------------------------------------------- login
        private void BuildLogin()
        {
            Title("VALLE HONDO");
            var p = UiKit.Panel(_screen, "Panel login", new Vector2(0.5f, 0.5f), new Vector2(-300f, -60f), new Vector2(760f, 560f));
            UiKit.Header(p, _register ? "Crear cuenta" : "Iniciar sesión", new Vector2(0f, 245f), 720f);
            float y = 160f;
            _email = Field(p, "EMAIL", ref y, false);
            if (_register) _user = Field(p, "USUARIO (3-20)", ref y, false);
            _pass = Field(p, "CONTRASEÑA (8+)", ref y, true);
            _list = MenuList.Create(p, new Vector2(0f, y - 90f), 680f, new[] { _register ? "Registrarme" : "Entrar", _register ? "Ya tengo cuenta" : "Crear cuenta", "Modo prueba (sin sesión)" });
            _list.Confirmed += i =>
            {
                if (i == 0) _ = Submit();
                else if (i == 1) { _register = !_register; Open(FrontScreen.Login); }
                else { TestMode = true; Open(FrontScreen.Menu); }
            };
            _status = UiKit.Text(_screen, "", 24, UiKit.Orange, TextAnchor.MiddleCenter, new Vector2(-300f, -370f), new Vector2(760f, 40f));
            UiKit.Footer(_screen, "Tab Cambiar campo   ·   ↑↓ Elegir   ·   Enter Aceptar");
        }

        private InputField Field(Transform parent, string label, ref float y, bool password)
        {
            UiKit.Text(parent, label, 20, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(-20f, y), new Vector2(640f, 26f), true);
            var bg = UiKit.Image(parent, "Campo " + label, new Vector2(0.5f, 0.5f), new Vector2(0f, y - 34f), new Vector2(660f, 44f), new Color(0f, 0f, 0f, 0.6f), UiKit.FrameSprite, true);
            bg.raycastTarget = true;
            var t = UiKit.Text(bg.transform, "", 26, UiKit.Cream, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(620f, 40f), false, false);
            t.supportRichText = false;
            var f = bg.gameObject.AddComponent<InputField>(); f.textComponent = t; f.targetGraphic = bg;
            if (password) { f.contentType = InputField.ContentType.Password; f.asteriskChar = '•'; }
            y -= 92f;
            return f;
        }

        private async Task Submit()
        {
            if (_busy) return;
            _busy = true; _status.text = "Conectando...";
            try
            {
                var res = _register ? await AuthService.Register(_email.text.Trim(), _user != null ? _user.text.Trim() : "", _pass.text) : await AuthService.Login(_email.text.Trim(), _pass.text);
                if (this == null) return;
                if (res.Ok) { _pass.text = ""; Open(FrontScreen.Menu); return; }
                _status.text = res.Error;
            }
            finally { _busy = false; }
        }

        // ---------------------------------------------------------------- menú principal
        public static string Describe(int item, LocalProfile p)
        {
            switch (item)
            {
                case 0: return "Nueva partida de 7 días. Elige personaje y mapa: Calle en ruinas o Bosque oscuro.";
                case 1: return p.ContinueDay > 0 ? "Vuelve al día " + p.ContinueDay + " en " + MapName(p.Last.mapId) + "." : "No hay partida a medias.";
                case 2: return "Las mejores partidas" + (Session.IsLoggedIn ? " del servidor." : " de este equipo (sin sesión).");
                case 3: return "Tus partidas, bajas y victorias.";
                case 4: return "Volumen, voz y subtítulos de VIGÍA, pantalla.";
                case 5: return "Quién hizo el juego.";
                default: return "Cerrar el juego.";
            }
        }

        public static string MapName(string id) => id == MapLibrary.ForestId ? "Bosque oscuro" : "Calle en ruinas";

        private void BuildMenu()
        {
            Title("VALLE HONDO");
            _list = LeftList("Menú principal", MenuItems);
            InfoPanel("Información", out _info);
            var prof = FrontEndData.Profile;
            void Refresh(int i)
            {
                var last = prof.Last;
                _info.text = Describe(i, prof) + "\n\n" + (Session.IsLoggedIn ? "SESIÓN: " + FrontEndData.PlayerName : "MODO PRUEBA (sin sesión)") +
                    "\nÚLTIMA PARTIDA: " + (last == null ? "ninguna" : MapName(last.mapId) + " · día " + last.dayReached + " · " + last.kills + " bajas · " + (last.victory ? "victoria" : "caído"));
            }
            _list.Selected += Refresh; Refresh(0);
            _list.Confirmed += i =>
            {
                switch (i)
                {
                    case 0: Open(FrontScreen.Select); break;
                    case 1: if (prof.ContinueDay > 0) { MapIO.SelectedId = prof.Last.mapId; DayLoop.PendingStartDay = prof.ContinueDay; StartGame(); } break;
                    case 2: Open(FrontScreen.Ranking); break;
                    case 3: Open(FrontScreen.Profile); break;
                    case 4: Open(FrontScreen.Options); break;
                    case 5: Open(FrontScreen.Credits); break;
                    default: Application.Quit(); break;
                }
            };
            _list.Back += () => { if (!Session.IsLoggedIn) { TestMode = false; Open(FrontScreen.Login); } };
            Notice("VIGÍA: señal vital detectada");
            UiKit.Footer(_screen, "↑↓ Elegir   ·   Enter Aceptar   ·   Esc Volver");
        }

        // ---------------------------------------------------------------- selección de personaje y mapa
        private void BuildSelect()
        {
            Title("PREPARACIÓN");
            var all = CharacterVariantLibrary.All;
            _list = LeftList("Personaje y mapa", new[] { "Personaje", "Mapa", "Empezar", "Volver" }, new[] { "", "", "", "" });
            InfoPanel("Vista previa", out _info);
            _info.rectTransform.anchoredPosition = new Vector2(120f, -40f); _info.rectTransform.sizeDelta = new Vector2(480f, 440f);
            _soldier = UiKit.Image(_screen, "Silueta", new Vector2(0.5f, 0.5f), new Vector2(200f, -60f), new Vector2(200f, 360f), Color.white);
            _preview = UiKit.Image(_screen, "Mapa", new Vector2(0.5f, 0.5f), new Vector2(200f, -60f), new Vector2(300f, 300f), Color.white);
            void Refresh(int row)
            {
                var v = all[_variant];
                _list.SetValue(0, "◄ " + v.displayName + " ►"); _list.SetValue(1, "◄ " + MapName(MapIO.PlayableIds[_map]) + " ►");
                bool showMap = row == 1;
                _soldier.gameObject.SetActive(!showMap); _preview.gameObject.SetActive(showMap);
                if (showMap)
                {
                    var m = MapIO.LoadLocal(MapIO.PlayableIds[_map]);
                    if (m != null) { var tex = UiKit.Make(128, 128, MenuArt.MapPreview(m, 128), "Vista mapa", TextureWrapMode.Clamp); _owned.Add(tex); _preview.sprite = Sprite.Create(tex, new Rect(0, 0, 128, 128), Vector2.one * 0.5f); }
                    _info.text = MapName(MapIO.PlayableIds[_map]).ToUpperInvariant() + "\n\n" + (MapIO.PlayableIds[_map] == MapLibrary.ForestId
                        ? "Noche cerrada entre pinos, niebla densa y una cabaña con tres entradas. Pocas coberturas duras: muévete."
                        : "Autopista y nave industrial en ruinas, casa central con segundo piso y balcón. Coberturas de hormigón y coches.");
                }
                else
                {
                    var tex = UiKit.Make(96, 160, MenuArt.Soldier(96, 160, v.hiVis ? new[] { v.jacket[0], v.jacket[1] + 0.12f, v.jacket[2] + 0.1f } : new[] { 0.85f + v.camoLight[0] * 0.2f, 0.8f, 0.68f }), "Silueta", TextureWrapMode.Clamp); _owned.Add(tex);
                    _soldier.sprite = Sprite.Create(tex, new Rect(0, 0, 96, 160), Vector2.one * 0.5f);
                    _info.text = v.displayName.ToUpperInvariant() + "\n\nVida " + Mathf.RoundToInt(v.maxHealth) + "\nVelocidad x" + v.moveSpeed.ToString("0.00") + "\nCuración x" + v.healing.ToString("0.00") + "\nBalanceo del arma x" + v.weaponSway.ToString("0.00") + (v.hiVis ? "\n\nRescatista: chaqueta reflectante, capucha y gorra." : "");
                }
            }
            _list.Selected += Refresh; Refresh(0);
            _list.Adjusted += (row, dir) =>
            {
                if (row == 0) _variant = (_variant + dir + all.Count) % all.Count;
                else if (row == 1) _map = (_map + dir + MapIO.PlayableIds.Length) % MapIO.PlayableIds.Length;
                Refresh(row);
            };
            _list.Confirmed += i =>
            {
                if (i == 0 || i == 1) { _list.Select(i, false); return; }
                if (i == 3) { Open(FrontScreen.Menu); return; }
                CharacterVariantLibrary.SetCurrent(all[_variant].id);
                MapIO.SelectedId = MapIO.PlayableIds[_map];
                DayLoop.PendingStartDay = 1;
                StartGame();
            };
            _list.Back += () => Open(FrontScreen.Menu);
            UiKit.Footer(_screen, "↑↓ Elegir   ·   ←→ Cambiar   ·   Enter Empezar   ·   Esc Volver");
        }

        /// <summary>Cierra el front-end: la arena (que esperaba) arranca con la intro y la partida.</summary>
        public void StartGame()
        {
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- ranking y perfil
        private void Table(Transform parent, IList<string[]> rows, int highlight, Vector2 pos)
        {
            for (int r = 0; r < rows.Count; r++)
            {
                if (r == highlight) UiKit.Image(parent, "Fila jugador", new Vector2(0.5f, 0.5f), new Vector2(pos.x, pos.y - r * 44f), new Vector2(1300f, 40f), Color.white, UiKit.HighlightSprite);
                for (int c = 0; c < rows[r].Length; c++)
                    UiKit.Text(parent, rows[r][c], r == 0 ? 20 : 24, r == 0 ? UiKit.CreamDim : UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(pos.x - 560f + c * 220f, pos.y - r * 44f), new Vector2(220f, 36f), r == 0);
            }
        }

        public static List<string[]> RankingRows(IList<MatchRecord> list)
        {
            var rows = new List<string[]> { new[] { "#", "JUGADOR", "PUNTOS", "DÍAS", "BAJAS", "MAPA" } };
            for (int i = 0; i < list.Count; i++) rows.Add(new[] { (i + 1).ToString(), list[i].player, list[i].points.ToString(), list[i].daysCompleted + (list[i].victory ? " ★" : ""), list[i].kills.ToString(), MapName(list[i].mapId) });
            return rows;
        }

        private void BuildRanking()
        {
            Title("RANKING");
            var p = UiKit.Panel(_screen, "Panel ranking", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1400f, 640f));
            UiKit.Header(p, Session.IsLoggedIn ? "Ranking del servidor" : "Ranking local", new Vector2(0f, 285f), 1360f);
            var local = FrontEndData.Profile.Ranking(10);
            int me = local.FindIndex(r => r.player == FrontEndData.PlayerName) + 1;
            Table(p, RankingRows(local), me, new Vector2(0f, 220f));
            if (Session.IsLoggedIn) _ = LoadServerRanking(p);
            Back();
        }

        private async Task LoadServerRanking(RectTransform p)
        {
            var res = await ApiClient.Get<FrontEndData.RankingList>("/api/rankings?limit=10");
            if (this == null || p == null || !res.Ok || res.Data?.rankings == null) return;
            foreach (Transform c in p) if (c.name.StartsWith("Texto") || c.name == "Fila jugador") Destroy(c.gameObject);
            var rows = new List<string[]> { new[] { "#", "JUGADOR", "PUNTOS", "DÍAS", "BAJAS", "" } };
            int me = 0;
            foreach (var r in res.Data.rankings) { rows.Add(new[] { r.position.ToString(), r.username, r.score.ToString(), r.daysSurvived + (r.victory ? " ★" : ""), r.kills.ToString(), "" }); if (r.username == FrontEndData.PlayerName && me == 0) me = rows.Count - 1; }
            Table(p, rows, me, new Vector2(0f, 220f));
        }

        private void BuildProfile()
        {
            Title("PERFIL");
            var prof = FrontEndData.Profile;
            var p = UiKit.Panel(_screen, "Panel perfil", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1400f, 640f));
            UiKit.Header(p, FrontEndData.PlayerName + " · " + prof.matches.Count + " partidas · " + prof.Victories + " victorias · " + prof.TotalKills + " bajas · mejor " + prof.BestPoints, new Vector2(0f, 285f), 1360f);
            var rows = new List<string[]> { new[] { "FECHA", "MAPA", "DÍA", "BAJAS", "PUNTOS", "RESULTADO" } };
            int start = Mathf.Max(0, prof.matches.Count - 10);
            for (int i = prof.matches.Count - 1; i >= start; i--) { var m = prof.matches[i]; rows.Add(new[] { m.date, MapName(m.mapId), m.dayReached.ToString(), m.kills.ToString(), m.points.ToString(), m.victory ? "Victoria" : "Caído" }); }
            Table(p, rows, rows.Count > 1 ? 1 : -1, new Vector2(0f, 220f));
            Back();
        }

        private void Back()
        {
            _list = MenuList.Create(_screen, new Vector2(0f, -420f), 400f, new[] { "Volver" });
            _list.Confirmed += _ => Open(FrontScreen.Menu); _list.Back += () => Open(FrontScreen.Menu);
            UiKit.Footer(_screen, "Enter / Esc Volver");
        }

        // ---------------------------------------------------------------- opciones y créditos
        public const string VolumeKey = "7d.volume";

        private void BuildOptions()
        {
            Title("OPCIONES");
            string[] items = { "Volumen", "Voz de VIGÍA", "Subtítulos", "Altura subtítulos", "Pantalla completa", "Volver" };
            _list = LeftList("Opciones", items, new[] { "", "", "", "", "", "" }, 900f);
            InfoPanel("Ayuda", out _info);
            _info.text = "←→ cambia el valor.\nTodo se guarda al momento y también se ajusta en partida con Esc.";
            void Show()
            {
                _list.SetValue(0, Mathf.RoundToInt(AudioListener.volume * 100f) + " %");
                _list.SetValue(1, SevenDays.Vigia.VigiaSettings.ModeNames[(int)SevenDays.Vigia.VigiaSettings.Mode]);
                _list.SetValue(2, SevenDays.Vigia.VigiaSettings.Subtitles ? "Sí" : "No");
                _list.SetValue(3, Mathf.RoundToInt(SevenDays.Vigia.VigiaSettings.SubtitleHeight * 100f) + " % desde abajo");
                _list.SetValue(4, Screen.fullScreen ? "Sí" : "No");
            }
            void Adj(int row, int dir)
            {
                switch (row)
                {
                    case 0: AudioListener.volume = Mathf.Clamp01(AudioListener.volume + 0.1f * dir); PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume); break;
                    case 1: SevenDays.Vigia.VigiaSettings.Mode = (VigiaMode)(((int)SevenDays.Vigia.VigiaSettings.Mode + dir + 4) % 4); break;
                    case 2: SevenDays.Vigia.VigiaSettings.Subtitles = !SevenDays.Vigia.VigiaSettings.Subtitles; break;
                    case 3: SevenDays.Vigia.VigiaSettings.SubtitleHeight += 0.01f * dir; break;
                    case 4: Screen.fullScreen = !Screen.fullScreen; break;
                }
                Show();
            }
            _list.Adjusted += Adj;
            _list.Confirmed += i => { if (i == items.Length - 1) Open(FrontScreen.Menu); else Adj(i, 1); };
            _list.Back += () => Open(FrontScreen.Menu);
            Show();
            UiKit.Footer(_screen, "↑↓ Elegir   ·   ←→ Cambiar   ·   Esc Volver");
        }

        public const string CreditsAiLine = "Asistencia de IA: Claude (Anthropic), en programación, pruebas y documentación.";

        public static string[] CreditLines()
        {
            var ta = Resources.Load<TextAsset>("creditos");
            var lines = new List<string>();
            if (ta != null) foreach (var l in ta.text.Split('\n')) { var t = l.TrimEnd('\r'); if (t.Length > 0 && !t.StartsWith("#")) lines.Add(t); }
            if (lines.Count == 0) lines.Add("Equipo 7Days");
            lines.Add("");
            lines.Add(CreditsAiLine);
            return lines.ToArray();
        }

        private void BuildCredits()
        {
            Title("CRÉDITOS");
            var p = UiKit.Panel(_screen, "Panel créditos", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1100f, 640f));
            UiKit.Header(p, "Créditos", new Vector2(0f, 285f), 1060f);
            var t = UiKit.Text(p, string.Join("\n", CreditLines()), 28, UiKit.Cream, TextAnchor.UpperCenter, new Vector2(0f, -20f), new Vector2(1000f, 520f), false, false);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Back();
        }

        private void Update()
        {
            if (_notice != null) { float a = Mathf.Clamp01((Time.unscaledTime - _noticeAt - 0.6f) / 0.5f) * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f)); var c = _notice.color; c.a = a; _notice.color = c; }
            if (_list != null)
            {
                bool typing = (_email != null && _email.isFocused) || (_user != null && _user.isFocused) || (_pass != null && _pass.isFocused);
                _list.Focused = !typing;
                var k = UnityEngine.InputSystem.Keyboard.current;
                if (typing && k != null && k.tabKey.wasPressedThisFrame) NextField();
                if (typing && k != null && k.enterKey.wasPressedThisFrame && _current == FrontScreen.Login) _ = Submit();
            }
        }

        private void NextField()
        {
            var f = new List<InputField>(); if (_email != null) f.Add(_email); if (_user != null) f.Add(_user); if (_pass != null) f.Add(_pass);
            int i = f.FindIndex(x => x.isFocused);
            var next = f[(i + 1) % f.Count]; next.Select(); next.ActivateInputField();
        }
    }
}

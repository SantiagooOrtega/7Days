using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Core
{
    /// <summary>
    /// Música en bucle, única en todo el juego: se crea al abrir el juego y vive entre escenas (DontDestroyOnLoad), así que no se corta ni se duplica al
    /// recargar (R), reintentar o cambiar de mapa (M). Suena en carga, login, menú, intro y partida. El volumen sigue la fase (MusicMix): alto en menú, baja con
    /// fundido de 1.5 s al entrar al Día 1 y al morir, sube un poco con jefe o combate intenso y se aparta (ducking) cuando habla VIGÍA.
    /// Se oye por el grupo "Música" del menú Esc (deslizador y apagado). Si el clip es corto, cruza dos fuentes; si no hay clip, silencio sin errores.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public const string LibraryResource = "Audio/MusicLibrary";
        public static MusicManager Instance { get; private set; }

        private AudioSource _a, _b;
        private AudioClip _clip;
        private float _level, _target, _rate = 1f, _nextProbe, _crossStart = -1f;
        private bool _tense, _cross;
        private MusicPhase _phase;

        public AudioClip Clip => _clip;
        public AudioSource Source => _a;
        public float Level => _level;
        public float Target => _target;
        public MusicPhase Phase => _phase;
        public bool Crossfading => _cross;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { if (!Application.isBatchMode) Ensure(); }   // en las pruebas automáticas la crean ellas

        public static MusicManager Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Música");
            return go.AddComponent<MusicManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _a = MakeSource(); _b = MakeSource();
            var lib = Resources.Load<MusicLibrary>(LibraryResource);
            _clip = lib != null ? lib.main : null;
            _cross = _clip != null && MusicMix.NeedsCrossfade(_clip.length);
            _phase = Detect(out bool speaking);
            _level = _target = MusicMix.Target(_phase, speaking, false);
            if (_clip != null) { _a.clip = _clip; _b.clip = _clip; _a.loop = !_cross; _a.Play(); }
            Apply(1f, 0f);
        }

        private AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 0f; s.priority = 0; s.loop = true;
            s.ignoreListenerPause = true; s.bypassReverbZones = true; s.bypassEffects = true;
            s.volume = 0f;
            return s;
        }

        /// <summary>Fase actual del juego a partir de lo que hay en pantalla.</summary>
        public static MusicPhase Detect(out bool vigiaSpeaking)
        {
            vigiaSpeaking = SevenDays.Intro.IntroPlayer.Speaking || (SevenDays.Vigia.VigiaVoice.Instance != null && SevenDays.Vigia.VigiaVoice.Instance.Speaking);
            if (SevenDays.Intro.IntroPlayer.Active) return MusicPhase.Intro;
            var d = SevenDays.World.DayLoop.Instance;
            if (d == null) return MusicPhase.Menu;   // carga, login, menú y "Generando texturas..."
            if (d.Cycle != null && d.Cycle.Phase == DayPhase.Victory) return MusicPhase.Victory;
            if (d.Cycle != null && d.Cycle.Phase == DayPhase.Defeat) return MusicPhase.Dead;
            return MusicPhase.Game;
        }

        private void Update()
        {
            var ph = Detect(out bool speaking);
            if (Time.unscaledTime >= _nextProbe)
            {
                _nextProbe = Time.unscaledTime + 0.5f;   // contar zombis no hace falta cada frame
                var d = SevenDays.World.DayLoop.Instance;
                _tense = d != null && d.Intense;
            }
            float target = MusicMix.Target(ph, speaking, _tense);
            if (ph != _phase || !Mathf.Approximately(target, _target))
            {
                _rate = MusicMix.Rate(_level, target, MusicMix.TransitionSeconds(_phase, ph));
                _phase = ph; _target = target;
            }
            _level = MusicMix.Step(_level, _target, _rate, Time.unscaledDeltaTime);
            TickCrossfade();
        }

        /// <summary>Clip corto: antes de que termine, arranca la otra fuente desde el principio y cruza las dos.</summary>
        private void TickCrossfade()
        {
            if (_clip == null) return;
            if (!_cross) { Apply(1f, 0f); return; }
            if (_crossStart < 0f && _a.isPlaying && _a.time >= _clip.length - MusicMix.CrossfadeSeconds)
            {
                _b.time = 0f; _b.Play(); _crossStart = Time.unscaledTime;
            }
            if (_crossStart >= 0f)
            {
                float t = (Time.unscaledTime - _crossStart) / MusicMix.CrossfadeSeconds;
                MusicMix.CrossfadeGains(t, out float outgoing, out float incoming);
                Apply(outgoing, incoming);
                if (t >= 1f) { _a.Stop(); (_a, _b) = (_b, _a); _crossStart = -1f; Apply(1f, 0f); }
            }
            else Apply(1f, 0f);
        }

        private void Apply(float gainA, float gainB)
        {
            float v = _level * AudioMix.Volume(AudioGroup.Music);
            _a.volume = v * gainA; _b.volume = v * gainB;
        }

        /// <summary>Para las pruebas: fija la fase al instante (sin esperar al fundido).</summary>
        public void Snap() { _phase = Detect(out bool s); _target = _level = MusicMix.Target(_phase, s, _tense); Apply(1f, 0f); }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}

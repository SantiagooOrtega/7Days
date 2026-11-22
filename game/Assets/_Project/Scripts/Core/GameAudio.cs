using UnityEngine;
using SevenDays.Combat;

namespace SevenDays.Core
{
    /// <summary>
    /// Silencio del juego al acabar la partida (muerte, victoria, epílogo, pantallas finales): calla zombis, armas, fuego, ambiente y la cola de VIGÍA de la
    /// partida (sus avisos de pantalla, pedidos después, sí suenan), vacía el pool de voces de zombi y pone a 0 los grupos Zombis, Armas y Ambiente para que
    /// nada nuevo suene. Solo queda la música (que MusicManager sube a volumen de menú con fundido de 1.5 s). Al reintentar o volver al menú (recarga de
    /// escena) se restablece.
    /// </summary>
    public static class GameAudio
    {
        public static bool Silenced => AudioMix.GameplayMuted;

        public static void SilenceGameplay()
        {
            AudioMix.GameplayMuted = true;
            ZombieAudio.StopAll();
            if (SevenDays.Vigia.VigiaVoice.Instance != null) SevenDays.Vigia.VigiaVoice.Instance.Silence();
            foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (!Keep(s) && s.isPlaying) s.Stop();
        }

        public static void Restore() => AudioMix.GameplayMuted = false;

        /// <summary>Lo que sigue sonando: la música, la interfaz, VIGÍA (avisos de pantalla), el cómic y la pantalla de estadísticas.</summary>
        public static bool Keep(AudioSource s)
            => s.GetComponent<MusicManager>() != null || s.GetComponent<SevenDays.Vigia.VigiaVoice>() != null || s.GetComponentInParent<SevenDays.Intro.IntroPlayer>() != null
               || s.GetComponent<SevenDays.UI.StatsScreen>() != null || s.gameObject.name == "UI audio";
    }
}

using UnityEngine;

namespace SevenDays.Core
{
    /// <summary>
    /// La música del juego (Resources/Audio/MusicLibrary): la rellena sola el editor con el clip de Assets/_Project/Audio/Music (musica_juego.mp3 primero),
    /// que tiene prioridad sobre cualquier música generada. Sin clip, el MusicManager queda en silencio sin errores.
    /// </summary>
    public class MusicLibrary : ScriptableObject
    {
        public AudioClip main;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.Combat
{
    /// <summary>
    /// Lista de clips de zombi propios (CC0) que el editor arma sola desde Assets/_Project/Audio/Zombies (Editor/ZombieAudioLibraryBuilder) y guarda en
    /// Resources/Audio/ZombieClipLibrary. Si existe, sus clips sustituyen a los sintetizados del mismo evento. Nombres en docs/ASSETS.md.
    /// </summary>
    public class ZombieClipLibrary : ScriptableObject
    {
        public List<AudioClip> clips = new List<AudioClip>();
    }
}

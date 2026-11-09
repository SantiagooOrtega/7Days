using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.Core
{
    public enum AudioGroup { Zombies, Weapons, Ambient, UI }

    /// <summary>
    /// Mezcla por grupos (zombis, armas, ambiente, UI) más un volumen general, guardada en PlayerPrefs y editable desde el menú Esc.
    /// Hace el papel de un AudioMixer: Unity no deja crear un AudioMixer desde código (es un asset del editor), así que cada AudioSource se registra en su grupo
    /// y su "volume" es el volumen del grupo; los PlayOneShot llevan su propio volumen relativo encima. Los que cambian su volumen cada frame (respiración, mecha)
    /// multiplican por Volume(grupo).
    /// </summary>
    public static class AudioMix
    {
        public const string PrefPrefix = "audio.vol.";
        private static readonly float[] Volumes = { 1f, 1f, 1f, 1f };
        private static float _master = 1f;
        private static bool _loaded;
        private static readonly List<(AudioSource src, AudioGroup g, float baseVolume)> Sources = new List<(AudioSource, AudioGroup, float)>();

        public static readonly string[] Names = { "Zombis", "Armas", "Ambiente", "Interfaz" };

        public static float Master { get { Load(); return _master; } set { Load(); _master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefPrefix + "master", _master); ApplyAll(); } }

        /// <summary>Volumen final de un grupo (grupo x general).</summary>
        public static float Volume(AudioGroup g) { Load(); return Volumes[(int)g] * _master; }
        public static float GroupVolume(AudioGroup g) { Load(); return Volumes[(int)g]; }

        public static void SetGroupVolume(AudioGroup g, float v)
        {
            Load();
            Volumes[(int)g] = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(PrefPrefix + g, Volumes[(int)g]);
            ApplyAll();
        }

        /// <summary>Mete una fuente en su grupo: su volumen pasa a ser baseVolume x volumen del grupo (y se actualiza al mover los deslizadores).</summary>
        public static AudioSource Route(AudioSource src, AudioGroup g, float baseVolume = 1f)
        {
            Load();
            Sources.Add((src, g, baseVolume));
            src.volume = baseVolume * Volume(g);
            return src;
        }

        private static void ApplyAll()
        {
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                var s = Sources[i];
                if (s.src == null) { Sources.RemoveAt(i); continue; }
                s.src.volume = s.baseVolume * Volume(s.g);
            }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _master = PlayerPrefs.GetFloat(PrefPrefix + "master", 1f);
            for (int i = 0; i < Volumes.Length; i++) Volumes[i] = PlayerPrefs.GetFloat(PrefPrefix + (AudioGroup)i, 1f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Sources.Clear(); _loaded = false; }
    }
}

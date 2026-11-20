using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Vigia
{
    /// <summary>Ajustes de VIGÍA guardados en PlayerPrefs (se cambian en el menú Esc): modo, subtítulos y su tamaño. El volumen es el grupo "Vigía" de AudioMix.</summary>
    public static class VigiaSettings
    {
        private const string Key = "vigia.";
        public static VigiaMode Mode { get => (VigiaMode)PlayerPrefs.GetInt(Key + "mode", 0); set => PlayerPrefs.SetInt(Key + "mode", (int)value); }
        public static bool Subtitles { get => PlayerPrefs.GetInt(Key + "subs", 1) == 1; set => PlayerPrefs.SetInt(Key + "subs", value ? 1 : 0); }
        /// <summary>Tamaño del texto de los subtítulos (multiplicador 0.8-1.6).</summary>
        public static float SubtitleScale { get => PlayerPrefs.GetFloat(Key + "subsize", 1f); set => PlayerPrefs.SetFloat(Key + "subsize", Mathf.Clamp(value, 0.8f, 1.6f)); }
        public static readonly string[] ModeNames = { "Completa", "Reducida", "Solo texto", "Apagada" };
    }
}

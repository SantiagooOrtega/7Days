using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Vigia
{
    /// <summary>Carga los WAV de VIGÍA (Resources) y les aplica el filtro de radio una sola vez (caché). La usan la voz en partida y la intro.</summary>
    public static class VigiaClips
    {
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string name)
        {
            if (Cache.TryGetValue(name, out var c)) return c;
            var raw = Resources.Load<AudioClip>(name);
            if (raw != null && raw.loadState != AudioDataLoadState.Loaded) raw.LoadAudioData();
            if (raw != null && raw.channels == 1 && raw.samples > 0)
            {
                var data = new float[raw.samples];
                if (raw.GetData(data, 0))
                {
                    var o = RadioFx.Process(data, raw.frequency);
                    c = AudioClip.Create(name + "_radio", o.Length, 1, raw.frequency, false);
                    c.SetData(o, 0);
                }
            }
            if (c == null) c = raw;   // sin procesar si no se pudo leer
            Cache[name] = c;
            if (c == null) Debug.LogWarning("[7Days] VIGÍA: falta el clip " + name + " (solo subtítulo)");
            return c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();
    }
}

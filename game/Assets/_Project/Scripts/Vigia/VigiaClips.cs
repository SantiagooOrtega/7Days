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

        /// <summary>Clips ya listos (procesados).</summary>
        public static int Ready => Cache.Count;
        public static bool Prewarming { get; private set; }

        /// <summary>
        /// Prepara todos los clips del catálogo mientras carga la partida (detrás de la intro): en el hilo principal solo se lee el WAV (un clip por frame) y el
        /// filtro de radio se calcula en un hilo de fondo. Antes se hacía todo de golpe la primera vez que VIGÍA decía cada frase: un tirón en pleno combate.
        /// </summary>
        private sealed class Runner : MonoBehaviour { }
        private static Runner _runner;

        /// <summary>Arranca el precalentamiento en un objeto propio que sobrevive a recargas de escena (si lo llevara la arena y esta se destruyera a medias, quedaría a medio hacer).</summary>
        public static void StartPrewarm()
        {
            if (Prewarming || !Application.isPlaying) return;
            if (_runner == null) { var go = new GameObject("VIGÍA voces"); Object.DontDestroyOnLoad(go); _runner = go.AddComponent<Runner>(); }
            _runner.StartCoroutine(Prewarm());
        }

        public static System.Collections.IEnumerator Prewarm()
        {
            if (Prewarming) yield break;
            Prewarming = true;
            var ta = Resources.Load<TextAsset>("vigia_frases");
            var cat = VigiaCatalog.Parse(ta != null ? ta.text : "");
            var names = new List<string>();
            foreach (var line in cat.Values) for (int v = 0; v < line.Variants.Length; v++) names.Add(VigiaCatalog.ClipName(line.Id, v));
            // varios a la vez: el filtro de radio va en hilos de fondo; en el hilo principal solo leer el WAV y crear el clip
            const int Parallel = 6;
            var jobs = new List<(string name, AudioClip raw, int freq, System.Threading.Tasks.Task<float[]> task)>();
            int next = 0;
            while (next < names.Count || jobs.Count > 0)
            {
                while (jobs.Count < Parallel && next < names.Count)
                {
                    string name = names[next++];
                    if (Cache.ContainsKey(name)) continue;
                    var raw = Resources.Load<AudioClip>(name);
                    if (raw == null) continue;
                    if (raw.loadState != AudioDataLoadState.Loaded) raw.LoadAudioData();
                    if (raw.channels != 1 || raw.samples <= 0) { Cache[name] = raw; continue; }
                    var data = new float[raw.samples];
                    if (!raw.GetData(data, 0)) { Cache[name] = raw; continue; }
                    int freq = raw.frequency;
                    jobs.Add((name, raw, freq, System.Threading.Tasks.Task.Run(() => RadioFx.Process(data, freq))));
                }
                for (int i = jobs.Count - 1; i >= 0; i--)
                {
                    var j = jobs[i];
                    if (!j.task.IsCompleted) continue;
                    jobs.RemoveAt(i);
                    if (Cache.ContainsKey(j.name)) continue;   // se pidió mientras tanto (Get la hizo al momento)
                    if (j.task.IsFaulted) { Cache[j.name] = j.raw; continue; }
                    var o = j.task.Result;
                    var c = AudioClip.Create(j.name + "_radio", o.Length, 1, j.freq, false);
                    c.SetData(o, 0);
                    Cache[j.name] = c;
                    j.raw.UnloadAudioData();   // el WAV original ya no hace falta en memoria
                }
                yield return null;
            }
            Prewarming = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Cache.Clear(); Prewarming = false; _runner = null; }
    }
}

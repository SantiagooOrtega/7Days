using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Banco de sonidos de zombi: por tipo, todas las muestras de cada evento y las dos gargantas (timbres), sintetizadas por ZombieSynth en un hilo de fondo
    /// y convertidas a AudioClip en el hilo principal (unas pocas por frame, en Pump). Si hay clips CC0 en Assets/_Project/Audio/Zombies (ver docs/ASSETS.md),
    /// el editor los lista en Resources/Audio/ZombieClipLibrary y tienen prioridad sobre los sintetizados.
    /// </summary>
    public static class ZombieSoundBank
    {
        private const int ClipsPerPump = 6;
        private sealed class Rendered { public int Key; public int Variant; public float[] Data; public string Name; }
        private static readonly Dictionary<string, Task<List<Rendered>>> Jobs = new Dictionary<string, Task<List<Rendered>>>();
        private static readonly List<Rendered> Pending = new List<Rendered>();
        private static readonly Dictionary<int, AudioClip[]> Clips = new Dictionary<int, AudioClip[]>();
        private static readonly Dictionary<int, AudioClip[]> Overrides = new Dictionary<int, AudioClip[]>();
        private static readonly Dictionary<string, int> TypeIndex = new Dictionary<string, int>();
        private static bool _overridesLoaded;

        public static long Bytes { get; private set; }
        public static int ClipCount { get; private set; }
        public static int OverrideCount { get; private set; }
        public static bool Busy => Pending.Count > 0 || Jobs.Count > 0;

        private static int Index(string typeId)
        {
            if (!TypeIndex.TryGetValue(typeId, out int i)) { i = TypeIndex.Count + 1; TypeIndex[typeId] = i; }
            return i;
        }

        private static int Key(int type, ZombieSound ev, int timbre) => type * 1000 + (int)ev * 10 + timbre;

        /// <summary>Empieza a generar (si no estaba) los sonidos de un tipo en segundo plano.</summary>
        public static void Prewarm(string typeId)
        {
            LoadOverrides();
            if (Jobs.ContainsKey(typeId) || Clips.ContainsKey(Key(Index(typeId), ZombieSound.Idle, 0))) return;
            int type = Index(typeId);
            Jobs[typeId] = Task.Run(() =>
            {
                var list = new List<Rendered>();
                foreach (var ev in ZombieSoundMap.EventsFor(typeId))
                    for (int tb = 0; tb < (ZombieSoundMap.IsVocal(ev) ? ZombieSynth.Timbres : 1); tb++)
                        for (int v = 0; v < ZombieSoundMap.Variants(ev); v++)
                            list.Add(new Rendered { Key = Key(type, ev, tb), Variant = v, Data = ZombieSynth.Render(typeId, ev, v, tb), Name = typeId + " " + ev + " " + tb + "/" + v });
                return list;
            });
            SevenDays.Animation.CharacterArtLoader.Ensure();
        }

        /// <summary>Convierte a AudioClip lo que ya terminó (hilo principal; lo llama CharacterArtLoader cada frame).</summary>
        public static void Pump()
        {
            if (Jobs.Count > 0)
            {
                string done = null;
                foreach (var kv in Jobs) if (kv.Value.IsCompleted) { done = kv.Key; break; }
                if (done != null)
                {
                    var t = Jobs[done]; Jobs.Remove(done);
                    if (t.IsFaulted) Debug.LogError("[7Days] Error sintetizando sonidos de " + done + ": " + t.Exception);
                    else Pending.AddRange(t.Result);
                }
            }
            for (int k = 0; k < ClipsPerPump && Pending.Count > 0; k++)
            {
                var r = Pending[Pending.Count - 1]; Pending.RemoveAt(Pending.Count - 1);
                var clip = AudioClip.Create(r.Name, r.Data.Length, 1, ZombieSynth.Rate, false);
                clip.SetData(r.Data, 0);
                if (!Clips.TryGetValue(r.Key, out var arr) || arr == null) { arr = new AudioClip[ZombieSoundMap.Variants((ZombieSound)(r.Key / 10 % 100))]; Clips[r.Key] = arr; }
                arr[r.Variant] = clip;
                Bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip);
                ClipCount++;
            }
        }

        /// <summary>Genera y convierte ya todo lo pendiente (pruebas y panel F6).</summary>
        public static void WaitAll()
        {
            foreach (var t in new List<Task<List<Rendered>>>(Jobs.Values)) t.Wait();
            while (Busy) Pump();
        }

        /// <summary>Una muestra al azar del evento (distinta de la anterior, guardada en "last"); null si aún no está generada.</summary>
        public static AudioClip Pick(string typeId, ZombieSound ev, int timbre, ref int last)
        {
            int type = Index(typeId);
            if (!Overrides.TryGetValue(Key(type, ev, 0), out var arr) && !Overrides.TryGetValue(Key(0, ev, 0), out arr))
            {
                if (!ZombieSoundMap.IsVocal(ev)) timbre = 0;
                if (!Clips.TryGetValue(Key(type, ev, timbre), out arr)) return null;
            }
            int n = arr.Length;
            int i = n <= 1 ? 0 : (last < 0 || last >= n ? Random.Range(0, n) : Random.Range(0, n - 1));
            if (n > 1 && last >= 0 && last < n && i >= last) i++;   // nunca la misma dos veces seguidas
            if (arr[i] == null) return null;
            last = i;
            return arr[i];
        }

        // ------------------------------------------------------------------ clips CC0 opcionales
        /// <summary>Nombre de un clip propio: "{tipo|any}_{evento}_{n}", p. ej. "common_idle_1", "tank_roar_2", "any_headshot_1" (sin distinguir mayúsculas).</summary>
        public static bool ParseOverrideName(string name, out string typeId, out ZombieSound ev)
        {
            typeId = null; ev = ZombieSound.Idle;
            if (string.IsNullOrEmpty(name)) return false;
            string[] parts = name.ToLowerInvariant().Split('_');
            if (parts.Length < 2) return false;
            int evAt = parts.Length - 1;
            if (int.TryParse(parts[evAt], out _)) evAt--;
            if (evAt < 1) return false;
            if (!System.Enum.TryParse(parts[evAt], true, out ev) || !System.Enum.IsDefined(typeof(ZombieSound), ev) || int.TryParse(parts[evAt], out _)) return false;
            typeId = string.Join("_", parts, 0, evAt);
            if (typeId != "any" && System.Array.IndexOf(ZombieIds.All, typeId) < 0) return false;
            return true;
        }

        private static void LoadOverrides()
        {
            if (_overridesLoaded) return;
            _overridesLoaded = true;
            var lib = Resources.Load<ZombieClipLibrary>("Audio/ZombieClipLibrary");
            if (lib == null || lib.clips == null) return;
            var tmp = new Dictionary<int, List<AudioClip>>();
            foreach (var c in lib.clips)
            {
                if (c == null || !ParseOverrideName(c.name, out string typeId, out var ev)) continue;
                int key = Key(typeId == "any" ? 0 : Index(typeId), ev, 0);
                if (!tmp.TryGetValue(key, out var l)) tmp[key] = l = new List<AudioClip>();
                l.Add(c); OverrideCount++;
            }
            foreach (var kv in tmp) Overrides[kv.Key] = kv.Value.ToArray();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Jobs.Clear(); Pending.Clear(); Clips.Clear(); Overrides.Clear(); TypeIndex.Clear(); _overridesLoaded = false; Bytes = 0; ClipCount = 0; OverrideCount = 0; }
    }
}

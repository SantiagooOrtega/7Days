using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Look
{
    /// <summary>
    /// Texturas de señales (color + normales), una por tipo y variante de desgaste. Solo se generan las que el mapa necesita,
    /// en hilos de fondo (como SurfaceLibrary), y quedan en caché.
    /// </summary>
    public static class SignLibrary
    {
        private struct Entry { public Texture2D Diffuse, Normal; }
        private static readonly Dictionary<(SignType, int), Entry> Entries = new Dictionary<(SignType, int), Entry>();

        public static bool TryGet(SignType type, int variant, out Texture2D diffuse, out Texture2D normal)
        {
            if (Entries.TryGetValue((type, variant), out var e) && e.Diffuse != null) { diffuse = e.Diffuse; normal = e.Normal; return true; }
            diffuse = normal = null;
            return false;
        }

        public static IEnumerator Build(IEnumerable<(SignType type, int variant)> needed, int size, int seed)
        {
            var jobs = new List<((SignType, int) key, Task<SurfaceResult> task)>();
            foreach (var (type, variant) in needed)
            {
                if (Entries.ContainsKey((type, variant))) continue;
                var t = type; var v = variant;
                jobs.Add(((t, v), Task.Run(() => SignGenerator.Generate(t, SignGenerator.VariantParams(v, size, seed + (int)t * 13)))));
            }
            bool done;
            do
            {
                done = true;
                foreach (var j in jobs) if (!j.task.IsCompleted) done = false;
                if (!done) yield return null;
            } while (!done);

            foreach (var j in jobs)
            {
                if (j.task.IsFaulted) { Debug.LogError("[7Days] Falló una textura de señal: " + j.task.Exception?.GetBaseException().Message); continue; }
                var r = j.task.Result;
                Entries[j.key] = new Entry { Diffuse = ToTexture(r.Diffuse, r.Size, false), Normal = ToTexture(r.Normal, r.Size, true) };
                yield return null;
            }
        }

        private static Texture2D ToTexture(byte[] rgba, int size, bool linear)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            t.SetPixelData(rgba, 0);
            t.Apply(true, true);
            return t;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Entries.Clear();
    }
}

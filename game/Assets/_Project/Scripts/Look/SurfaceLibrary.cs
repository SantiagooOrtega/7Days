using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using SevenDays.Procedural;
using Debug = UnityEngine.Debug;

namespace SevenDays.Look
{
    /// <summary>
    /// Texturas procedurales (asfalto, muro, concreto, metal) con mapa de color y de normales.
    /// Se generan UNA vez al iniciar, en hilos de fondo (el juego no se congela) y quedan en caché.
    /// Si existen archivos Resources/Look/{nombre}_diff, esos tienen prioridad y no se genera nada para ese material.
    /// </summary>
    public static class SurfaceLibrary
    {
        public const string Floor = "floor";
        public const string Wall = "wall";
        public const string Concrete = "concrete";
        public const string Metal = "metal";
        public const string Container = "container";
        public const string CrateWood = "crate";
        public const string Cardboard = "cardboard";

        private struct Entry { public Texture2D Diffuse, Normal; }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        private static readonly (string name, SurfaceKind kind)[] Kinds =
        {
            (Floor, SurfaceKind.Asphalt), (Wall, SurfaceKind.Wall), (Concrete, SurfaceKind.Concrete), (Metal, SurfaceKind.Metal),
            (Container, SurfaceKind.Container), (CrateWood, SurfaceKind.CrateWood), (Cardboard, SurfaceKind.Cardboard),
        };

        public static bool Ready { get; private set; }

        public static bool HasFileOverride(string name) => Resources.Load<Texture2D>("Look/" + name + "_diff") != null;

        public static bool TryGet(string name, out Texture2D diffuse, out Texture2D normal)
        {
            if (Entries.TryGetValue(name, out var e) && e.Diffuse != null)
            {
                diffuse = e.Diffuse;
                normal = e.Normal;
                return true;
            }
            diffuse = normal = null;
            return false;
        }

        /// <summary>Corrutina: genera lo que falte. Úsala con "yield return SurfaceLibrary.Build(params)".</summary>
        public static IEnumerator Build(SurfaceParams p)
        {
            if (Ready) yield break;
            var sw = Stopwatch.StartNew();

            var jobs = new List<(string name, Task<SurfaceResult> task)>();
            foreach (var (name, kind) in Kinds)
            {
                if (HasFileOverride(name)) continue; // los archivos del usuario ganan
                SurfaceKind k = kind;
                var local = p.Clone(); // cada hilo con su copia
                jobs.Add((name, Task.Run(() => SurfaceGenerator.Generate(k, local))));
            }

            // Esperar sin bloquear: el juego sigue dibujando frames mientras los hilos trabajan.
            bool done;
            do
            {
                done = true;
                foreach (var j in jobs) if (!j.task.IsCompleted) done = false;
                if (!done) yield return null;
            } while (!done);

            // Subir a la GPU: una textura por frame para no hacer un tirón.
            foreach (var j in jobs)
            {
                if (j.task.IsFaulted)
                {
                    Debug.LogError("[7Days] Falló la textura procedural '" + j.name + "': " + j.task.Exception?.GetBaseException().Message);
                    continue;
                }
                var r = j.task.Result;
                Entries[j.name] = new Entry { Diffuse = ToTexture(r.Diffuse, r.Size, false), Normal = ToTexture(r.Normal, r.Size, true) };
                yield return null;
            }

            Ready = true;
            Debug.Log("[7Days] Texturas procedurales listas (" + jobs.Count + " materiales, " + p.Size + " px) en " + sw.ElapsedMilliseconds + " ms");
        }

        private static Texture2D ToTexture(byte[] rgba, int size, bool linear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            tex.SetPixelData(rgba, 0);
            tex.Apply(true, true); // genera mipmaps y libera la copia en CPU
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
            Ready = false;
        }
    }
}

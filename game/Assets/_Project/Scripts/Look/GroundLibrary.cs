using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;
using Debug = UnityEngine.Debug;

namespace SevenDays.Look
{
    /// <summary>
    /// Genera una vez, en hilos de fondo, el mapa de mezcla del piso (color con suavidad en el alfa + normales) y la textura de detalle, y crea
    /// el material. El mapa se divide en tiras de filas que se calculan en paralelo; la subida a la GPU va una textura por frame.
    /// </summary>
    public static class GroundLibrary
    {
        public static Material Material { get; private set; }
        public static bool Ready => Material != null;

        public static IEnumerator Build(GroundSettings gs, float globalBrightness)
        {
            if (!gs.Enabled || Ready) yield break;
            var sw = Stopwatch.StartNew();
            var p = gs.ToParams(globalBrightness);
            var buffers = new GroundBuffers(p.Size);

            int strips = Mathf.Clamp(SystemInfo.processorCount, 2, 8);
            var rows = new Task[strips];
            for (int i = 0; i < strips; i++)
            {
                int y0 = p.Size * i / strips, y1 = p.Size * (i + 1) / strips;
                rows[i] = Task.Run(() => GroundGenerator.FillRows(p, buffers, y0, y1));
            }
            var detailTask = Task.Run(() => GroundDetail.Generate(gs.DetailSize, gs.Seed + 3));
            foreach (var t in rows) while (!t.IsCompleted) yield return null;
            var finishTask = Task.Run(() => GroundGenerator.Finish(p, buffers)); // huellas + normales
            while (!finishTask.IsCompleted || !detailTask.IsCompleted) yield return null;

            if (finishTask.IsFaulted || detailTask.IsFaulted)
            {
                Debug.LogError("[7Days] Falló la generación del piso: " + (finishTask.Exception ?? detailTask.Exception)?.GetBaseException().Message + ". Se usa el concreto genérico.");
                yield break;
            }

            var map = finishTask.Result;
            var detail = detailTask.Result;
            var albedo = MakeTexture(map.Diffuse, map.Size, false, TextureWrapMode.Clamp); yield return null;
            var normal = MakeTexture(map.Normal, map.Size, true, TextureWrapMode.Clamp); yield return null;
            var dAlbedo = MakeTexture(detail.Diffuse, detail.Size, false, TextureWrapMode.Repeat); yield return null;
            var dNormal = MakeTexture(detail.Normal, detail.Size, true, TextureWrapMode.Repeat);

            float tiles = 60f / Mathf.Max(0.2f, gs.DetailTileMeters); // repeticiones de la textura de detalle sobre todo el mapa
            Material = Prim.GetGround(albedo, normal, dAlbedo, dNormal, tiles, gs.DetailStrength);
            Debug.Log("[7Days] Piso listo (" + p.Size + " px, " + strips + " hilos) en " + sw.ElapsedMilliseconds + " ms");
        }

        private static Texture2D MakeTexture(byte[] rgba, int size, bool linear, TextureWrapMode wrap)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, linear) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixelData(rgba, 0);
            t.Apply(true, true);
            return t;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Material = null;
    }
}

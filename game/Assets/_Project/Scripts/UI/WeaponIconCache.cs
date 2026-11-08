using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// Iconos de armas para el HUD: se generan UNA vez por arma desde las piezas de su modelo (WeaponIcons: silueta blanca, detalle gris y contorno, 256x128) y se
    /// guardan en caché. Texturas pequeñas sin mipmaps; el HUD las tiñe con el color de rareza.
    /// </summary>
    public static class WeaponIconCache
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static Texture2D _empty;

        public static Texture2D Get(WeaponData d)
        {
            string key = d.id + "|" + d.kind + "|" + d.length.ToString("0.###") + "|" + d.thickness.ToString("0.###");
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = Make(WeaponIcons.Rasterize(WeaponModel.Build(d)), "Icono " + d.id);
            Cache[key] = t;
            return t;
        }

        /// <summary>Icono tenue de una ranura vacía.</summary>
        public static Texture2D Empty => _empty != null ? _empty : _empty = Make(WeaponIcons.Empty(), "Icono vacío");

        private static Texture2D Make(byte[] rgba, string name)
        {
            var t = new Texture2D(WeaponIcons.Width, WeaponIcons.Height, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixelData(rgba, 0);
            t.Apply(false, true);
            return t;
        }

        /// <summary>Memoria de los iconos en caché (bytes).</summary>
        public static long Bytes { get { long n = 0; foreach (var t in Cache.Values) if (t != null) n += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t); return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Cache.Clear(); _empty = null; }
    }
}

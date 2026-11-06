using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Look
{
    /// <summary>Texturas de calcomanías (sangre, grietas, charcos), generadas una vez y en caché. Son pequeñas (256 px).</summary>
    public static class DecalLibrary
    {
        private static readonly Dictionary<DecalKind, Texture2D> Textures = new Dictionary<DecalKind, Texture2D>();

        public static Texture2D Get(DecalKind kind, int seed = 21)
        {
            if (Textures.TryGetValue(kind, out var t) && t != null) return t;
            const int size = 256;
            var px = DecalGenerator.Generate(kind, size, seed + (int)kind);
            t = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            t.SetPixelData(px, 0);
            t.Apply(true, true);
            Textures[kind] = t;
            return t;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Textures.Clear();
    }
}

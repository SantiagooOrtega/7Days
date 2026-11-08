using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Materiales de los zombis (URP Lit, sin assets externos): un material de piel por tipo (con mapa de emisión para ojos, venas y pústulas), uno de ropa común (con
    /// recorte por alfa para los agujeros) y uno de heridas (decals recortados por alfa). Las texturas se generan en hilos de fondo (ZombieTextures), a 512 px, y se
    /// aplican con mipmaps: color BC3, máscara BC3, normales sin comprimir y emisión BC. Mientras llegan, los materiales ya tienen su color base.
    /// </summary>
    public static class ZombieArt
    {
        public const int TextureSize = 512;
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), EmissionColor = Shader.PropertyToID("_EmissionColor");

        private sealed class Job { public Material Target; public ZombieAtlasKind Kind; public Task<ZombieMaps> Task; public string Name; }

        private static readonly Dictionary<string, Material> Skins = new Dictionary<string, Material>();
        private static readonly List<Job> Jobs = new List<Job>();
        private static Material _cloth, _wounds;
        /// <summary>Memoria de todas las texturas de zombi ya subidas (bytes, con mipmaps), para el informe.</summary>
        public static long TextureBytes { get; private set; }
        public static int TexturesReady { get; private set; }
        public static bool Busy => Jobs.Count > 0;

        public static Material Skin(string typeId)
        {
            typeId = ZombieAnatomies.Has(typeId) ? typeId : ZombieIds.Common;
            if (Skins.TryGetValue(typeId, out var m) && m != null) return m;
            var a = ZombieAnatomies.For(typeId);
            m = Lit("Zombi piel " + typeId, new Color(a.SkinBase[0], a.SkinBase[1], a.SkinBase[2]));
            m.EnableKeyword("_EMISSION");
            m.SetColor(EmissionColor, Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Skins[typeId] = m;
            Start(m, ZombieAtlasKind.Skin, typeId);
            return m;
        }

        public static Material Cloth
        {
            get
            {
                if (_cloth != null) return _cloth;
                _cloth = Lit("Zombi ropa", new Color(0.4f, 0.38f, 0.34f));
                AlphaClip(_cloth, 0.5f);
                Start(_cloth, ZombieAtlasKind.Cloth, ZombieIds.Common);
                return _cloth;
            }
        }

        public static Material Wounds
        {
            get
            {
                if (_wounds != null) return _wounds;
                _wounds = Lit("Zombi heridas", new Color(0.3f, 0.05f, 0.05f));
                AlphaClip(_wounds, 0.5f);
                Start(_wounds, ZombieAtlasKind.Wounds, ZombieIds.Common);
                return _wounds;
            }
        }

        private static void AlphaClip(Material m, float cutoff)
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
        }

        private static Material Lit(string name, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor(BaseColor, c);
            m.SetFloat("_Smoothness", 0.3f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", 2f);
            return m;
        }

        private static void Start(Material target, ZombieAtlasKind kind, string typeId)
        {
            Jobs.Add(new Job { Target = target, Kind = kind, Name = kind + " " + typeId, Task = Task.Run(() => ZombieTextures.Generate(kind, typeId, TextureSize)) });
            CharacterArtLoader.Ensure();
        }

        /// <summary>Aplica las texturas terminadas (la llama CharacterArtLoader cada frame en el hilo principal).</summary>
        public static void Poll()
        {
            for (int i = Jobs.Count - 1; i >= 0; i--)
            {
                var j = Jobs[i];
                if (!j.Task.IsCompleted) continue;
                Jobs.RemoveAt(i);
                if (j.Task.IsFaulted) { Debug.LogError("[7Days] Error generando texturas de zombi " + j.Name + ": " + j.Task.Exception); continue; }
                Apply(j);
            }
        }

        public static void WaitAll()
        {
            foreach (var j in Jobs.ToArray()) j.Task.Wait();
            Poll();
        }

        private static void Apply(Job j)
        {
            var maps = j.Task.Result;
            int n = maps.Size;
            var m = j.Target;
            var albedo = Make(maps.Albedo, n, false, true, j.Name + " color");
            m.SetColor(BaseColor, Color.white);
            m.SetTexture("_BaseMap", albedo);
            long bytes = Bytes(albedo);
            if (j.Kind != ZombieAtlasKind.Wounds)
            {
                var normal = Make(maps.Normal, maps.NormalSize, true, false, j.Name + " normales");
                var mask = Make(maps.Mask, n, true, true, j.Name + " máscara");
                m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
                m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f);
                bytes += Bytes(normal) + Bytes(mask);
            }
            if (j.Kind == ZombieAtlasKind.Skin)
            {
                var emis = Make(maps.Emission, maps.EmissionSize, false, true, j.Name + " emisión");
                m.SetTexture("_EmissionMap", emis);
                bytes += Bytes(emis);
            }
            TextureBytes += bytes; TexturesReady++;
        }

        private static Texture2D Make(byte[] rgba, int n, bool linear, bool compress, string name)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, linear) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixelData(rgba, 0);
            t.Apply(true, false);
            if (compress && SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)) t.Compress(false);
            t.Apply(false, true);
            return t;
        }

        private static long Bytes(Texture2D t) => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Skins.Clear(); Jobs.Clear(); _cloth = null; _wounds = null; TextureBytes = 0; TexturesReady = 0; }
    }
}

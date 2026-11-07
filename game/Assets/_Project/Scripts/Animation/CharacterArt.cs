using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Materiales y texturas de personajes y armas (procedurales, sin assets externos). Cada variante tiene 4 materiales URP Lit (uniforme, equipo,
    /// casco/cabeza y visor) con mapas de color (BC), normales (sin comprimir) y máscara (R metal, G oclusión, A suavidad: BC). Las texturas se generan en hilos de
    /// fondo (CharacterTextures) y se guardan en caché; mientras, los materiales ya existen con su color base, así que el personaje se ve desde el primer frame
    /// y "se viste" en cuanto terminan. Las armas comparten un atlas (WeaponMaterial) y un acento por rareza.
    /// </summary>
    public static class CharacterArt
    {
        public sealed class Set
        {
            public Material Uniform, Gear, Helmet, Visor;
            public bool Ready;
            public Material this[int slot] => slot == SoldierModel.SlotUniform ? Uniform : slot == SoldierModel.SlotGear ? Gear : slot == SoldierModel.SlotHelmet ? Helmet : Visor;
            /// <summary>Memoria de las texturas de este conjunto (bytes, con mipmaps), para el informe.</summary>
            public long TextureBytes;
        }

        private sealed class Job
        {
            public Material Target;
            public AtlasKind Kind;
            public Task<AtlasMaps> Task;
            public Set Owner;
        }

        private static readonly Dictionary<string, Set> Sets = new Dictionary<string, Set>();
        private static readonly List<Job> Jobs = new List<Job>();
        private static Material _weapon;
        private static readonly Dictionary<int, Material> Accents = new Dictionary<int, Material>();
        private static Set _weaponSet;

        /// <summary>Materiales de una variante (se crean al momento; las texturas llegan cuando terminan los hilos).</summary>
        public static Set For(CharacterVariantData v)
        {
            v = v ?? new CharacterVariantData();
            string key = v.id + "|" + v.seed + "|" + Hash(v);
            if (Sets.TryGetValue(key, out var set) && set.Uniform != null) return set;
            set = new Set
            {
                Uniform = Lit("Soldado uniforme " + v.id, Col(v.camoBase)),
                Gear = Lit("Soldado equipo " + v.id, Col(v.gear)),
                Helmet = Lit("Soldado casco " + v.id, Col(v.helmet)),
                Visor = VisorMaterial(),
            };
            Sets[key] = set;
            Start(set.Uniform, AtlasKind.Uniform, v, set);
            Start(set.Gear, AtlasKind.Gear, v, set);
            Start(set.Helmet, AtlasKind.Helmet, v, set);
            CharacterArtLoader.Ensure();
            return set;
        }

        /// <summary>Material compartido de las armas (atlas de pavonado, polímero, madera, cinta, lentes...).</summary>
        public static Material WeaponMaterial
        {
            get
            {
                if (_weapon != null) return _weapon;
                _weapon = Lit("Armas (atlas)", new Color(0.12f, 0.12f, 0.13f));
                _weaponSet = new Set();
                Start(_weapon, AtlasKind.Weapon, new CharacterVariantData(), _weaponSet);
                CharacterArtLoader.Ensure();
                return _weapon;
            }
        }

        /// <summary>Acento de color por rareza (franja en culata, cargador o empuñadura): apagado, sin romper el estilo.</summary>
        public static Material Accent(Rarity r)
        {
            int key = (int)r;
            if (Accents.TryGetValue(key, out var m) && m != null) return m;
            Color c = r == Rarity.Common ? new Color(0.62f, 0.6f, 0.55f) : r == Rarity.Uncommon ? new Color(0.36f, 0.45f, 0.27f)
                    : r == Rarity.Rare ? new Color(0.27f, 0.38f, 0.5f) : new Color(0.62f, 0.3f, 0.12f);   // épica: naranja óxido
            m = Lit("Acento " + r, c);
            m.SetFloat("_Smoothness", 0.35f);
            Accents[key] = m;
            return m;
        }

        /// <summary>Hay texturas generándose (la lista solo se toca en el hilo principal).</summary>
        public static bool Busy => Jobs.Count > 0;

        /// <summary>Aplica las texturas que ya terminaron (lo llama CharacterArtLoader cada frame en el hilo principal).</summary>
        public static void Poll()
        {
            for (int i = Jobs.Count - 1; i >= 0; i--)
            {
                var j = Jobs[i];
                if (!j.Task.IsCompleted) continue;
                Jobs.RemoveAt(i);
                if (j.Task.IsFaulted) { Debug.LogError("[7Days] Error generando texturas de " + j.Kind + ": " + j.Task.Exception); continue; }
                Apply(j);
            }
        }

        /// <summary>Espera a que terminen todas las texturas pendientes (pruebas, o al cargar detrás de "Generando texturas...").</summary>
        public static void WaitAll()
        {
            foreach (var j in Jobs.ToArray()) j.Task.Wait();
            Poll();
        }

        private static void Start(Material target, AtlasKind kind, CharacterVariantData v, Set owner)
        {
            var copy = v.Clone();
            int size = CharacterTextures.DefaultSize(kind);
            Jobs.Add(new Job { Target = target, Kind = kind, Owner = owner, Task = Task.Run(() => CharacterTextures.Generate(kind, copy, size)) });
        }

        private static void Apply(Job j)
        {
            var maps = j.Task.Result;
            int n = maps.Size;
            // color: sRGB, comprimido a BC; normales: lineales SIN comprimir; máscara: lineal, comprimida (el canal A de suavidad aguanta bien en BC3)
            var albedo = Make(maps.Albedo, n, false, true, j.Kind + " color");
            var normal = Make(maps.Normal, maps.NormalSize, true, false, j.Kind + " normales");
            var mask = Make(maps.Mask, n, true, true, j.Kind + " máscara");
            var m = j.Target;
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
            m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f);
            j.Owner.TextureBytes += Bytes(albedo) + Bytes(normal) + Bytes(mask);
            bool any = false;
            foreach (var other in Jobs) if (other.Owner == j.Owner) any = true;
            if (!any) j.Owner.Ready = true;
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

        private static Material Lit(string name, Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.25f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", 2f);   // mallas cerradas (Winding): caras traseras fuera
            return m;
        }

        /// <summary>Visor ahumado: transparente, muy liso y con un leve brillo propio (cubre las cuencas de la calavera).</summary>
        private static Material VisorMaterial()
        {
            var m = Lit("Visor ahumado", new Color(0.05f, 0.07f, 0.08f, 0.55f));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);   // lámina de una capa: se ve por las dos caras
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Smoothness", 0.95f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.12f, 0.2f, 0.18f));
            return m;
        }

        private static Color Col(float[] c) { var r = CharacterVariantData.C(c); return new Color(r.R, r.G, r.B); }

        private static int Hash(CharacterVariantData v) => JsonUtility.ToJson(v).GetHashCode();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Sets.Clear(); Jobs.Clear(); Accents.Clear(); _weapon = null; _weaponSet = null; }
    }
}

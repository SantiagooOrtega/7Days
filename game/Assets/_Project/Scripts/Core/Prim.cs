using System.Collections.Generic;
using UnityEngine;
using SevenDays.Look;

namespace SevenDays.Core
{
    /// <summary>
    /// Crea objetos con primitivas de Unity (sin assets externos). Los materiales se comparten por
    /// color/tipo para que el SRP Batcher y el GPU instancing agrupen las llamadas de dibujo.
    /// Si existen texturas en Resources/Look (ver docs/ASSETS.md) se usan; si no, se usa solo el color.
    /// </summary>
    public static class Prim
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Material _template;

        public static GameObject Create(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color,
            Transform parent = null, bool collider = true)
            => Create(type, name, position, scale, GetMaterial(color), parent, collider);

        public static GameObject Create(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material,
            Transform parent = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>
        /// Caja con UV por cara a escala real (la textura no se estira). "tileMeters" = metros por repetición;
        /// "uvOffset" desplaza la textura para que objetos iguales no se vean idénticos. "bevel" (m) bisela las aristas para
        /// disimular las uniones entre caras. Escala 1: el tamaño va en la malla; el collider es la caja completa.
        /// </summary>
        public static GameObject CreateBox(string name, Vector3 position, Vector3 size, Material material, Transform parent = null,
            float tileMeters = 3f, Vector2 uvOffset = default, bool collider = true, float bevel = 0.03f)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh.Get(size, tileMeters, uvOffset, bevel);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>
        /// Material transparente (con la luz de la escena) para calcomanías: sangre, grietas, charcos, marcas viales.
        /// La transparencia viene del canal alfa de la textura. Usa el Lit de URP, el mismo de las primitivas.
        /// </summary>
        public static Material GetDecal(Texture2D texture, Color tint, float smoothness, string key)
        {
            string k = "d|" + key + "|" + Key(tint) + "|" + smoothness.ToString("0.##");
            if (Cache.TryGetValue(k, out var m) && m != null) return m;
            var mat = NewMaterial(tint, smoothness, 0f);
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_Surface", 1f); // transparente
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", 1f);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // alfa normal, no premultiplicado
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetShaderPassEnabled("ShadowCaster", false);
            Cache[k] = mat;
            return mat;
        }

        /// <summary>Material mate de un color (poco brillo: evita el aspecto de plástico).</summary>
        public static Material GetMaterial(Color color) => GetOrCreate("c|" + Key(color), color, null, null, Vector2.one, 0.1f, 0f);

        /// <summary>Material que brilla (lámparas, balas trazadoras). Con Bloom da un halo.</summary>
        public static Material GetEmissive(Color color, float intensity)
        {
            string key = "e|" + Key(color) + "|" + intensity.ToString("0.#");
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var mat = NewMaterial(Color.black, 0f, 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
            Cache[key] = mat;
            return mat;
        }

        /// <summary>
        /// Material con textura. Prioridad: 1) archivos Resources/Look/{name}_diff (y _nor), 2) textura procedural
        /// de SurfaceLibrary, 3) solo el color. "tiling" = repeticiones sobre la superficie. Con tintTexture la textura
        /// se multiplica por el color (útil para contenedores de distintos colores).
        /// </summary>
        public static Material GetTextured(Color color, string name, Vector2 tiling, float smoothness = 0.15f, float metallic = 0f,
            bool tintTexture = false, float tone = 1f)
        {
            Texture2D diff = Resources.Load<Texture2D>("Look/" + name + "_diff");
            Texture2D nor = null;
            bool fromFile = diff != null;
            if (fromFile) nor = Resources.Load<Texture2D>("Look/" + name + "_nor");
            else if (!SurfaceLibrary.TryGet(name, out diff, out nor)) return GetMaterial(color);

            Color tint = !tintTexture ? Color.white : (fromFile ? color : color * 1.5f);
            tint = new Color(tint.r * tone, tint.g * tone, tint.b * tone, 1f); // tone: variación de brillo por objeto
            string key = "t|" + Key(tint) + "|" + name + "|" + tiling.x.ToString("0") + "x" + tiling.y.ToString("0")
                + "|" + smoothness.ToString("0.##") + "|" + metallic.ToString("0.##");
            return GetOrCreate(key, tint, diff, nor, tiling, smoothness, metallic);
        }

        /// <summary>Material con una textura de color y de normales ya creadas (por ejemplo las de una señal), compartido por clave.</summary>
        public static Material GetWithTextures(Texture2D diffuse, Texture2D normal, Color tint, float smoothness, float metallic, string key)
        {
            string k = "x|" + key + "|" + Key(tint) + "|" + smoothness.ToString("0.##") + "|" + metallic.ToString("0.##");
            return GetOrCreate(k, tint, diffuse, normal, Vector2.one, smoothness, metallic);
        }

        /// <summary>Tiling razonable para una pieza grande: una repetición cada ~3 metros.</summary>
        public static Vector2 TilingFor(Vector3 scale, float meters = 3f)
            => new Vector2(Mathf.Max(1f, Mathf.Round(Mathf.Max(scale.x, scale.z) / meters)), Mathf.Max(1f, Mathf.Round(scale.y / meters)));

        private static Material GetOrCreate(string key, Color color, Texture2D diff, Texture2D nor, Vector2 tiling,
            float smoothness, float metallic)
        {
            if (Cache.TryGetValue(key, out var existing) && existing != null) return existing;
            var mat = NewMaterial(color, smoothness, metallic);
            if (diff != null)
            {
                mat.SetTexture("_BaseMap", diff);
                mat.SetTextureScale("_BaseMap", tiling);
            }
            if (nor != null)
            {
                mat.SetTexture("_BumpMap", nor);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.EnableKeyword("_NORMALMAP");
            }
            Cache[key] = mat;
            return mat;
        }

        private static Material NewMaterial(Color color, float smoothness, float metallic)
        {
            if (_template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _template = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            var mat = new Material(_template) { enableInstancing = true };
            mat.SetColor("_BaseColor", color); // URP Lit
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        private static string Key(Color color)
        {
            Color32 c = color;
            return ((c.r << 24) | (c.g << 16) | (c.b << 8) | c.a).ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _template = null;
        }
    }
}

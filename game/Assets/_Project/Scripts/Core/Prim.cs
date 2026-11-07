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
            // Inmediato: Destroy() espera al final del frame y la auditoría de colisiones (que corre en el mismo frame) aún vería el collider.
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
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
        /// <param name="twoSided">
        /// false (por defecto) = Cull Back: para sólidos cerrados. true = Cull Off ("Render Face: Both"): para láminas de una sola
        /// capa y vegetación. OJO: URP Lit no voltea la normal de la cara trasera; por eso las mallas de doble cara llevan normales
        /// que valen para las dos caras (hacia arriba en la hierba) o una cara trasera propia con la normal volteada (dorso de la placa).
        /// </param>
        public static Material GetMaterial(Color color, bool twoSided = false)
            => GetOrCreate("c|" + Key(color) + Side(twoSided), color, null, null, Vector2.one, 0.1f, 0f, twoSided);

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
            bool tintTexture = false, float tone = 1f, bool twoSided = false)
        {
            Texture2D diff = Resources.Load<Texture2D>("Look/" + name + "_diff");
            Texture2D nor = null;
            bool fromFile = diff != null;
            if (fromFile) nor = Resources.Load<Texture2D>("Look/" + name + "_nor");
            else if (!SurfaceLibrary.TryGet(name, out diff, out nor)) return GetMaterial(color, twoSided);

            Color tint = !tintTexture ? Color.white : (fromFile ? color : color * 1.5f);
            tint = new Color(tint.r * tone, tint.g * tone, tint.b * tone, 1f); // tone: variación de brillo por objeto
            string key = "t|" + Key(tint) + "|" + name + "|" + tiling.x.ToString("0") + "x" + tiling.y.ToString("0")
                + "|" + smoothness.ToString("0.##") + "|" + metallic.ToString("0.##") + Side(twoSided);
            return GetOrCreate(key, tint, diff, nor, tiling, smoothness, metallic, twoSided);
        }

        /// <summary>Material con una textura de color y de normales ya creadas (por ejemplo las de una señal), compartido por clave.</summary>
        public static Material GetWithTextures(Texture2D diffuse, Texture2D normal, Color tint, float smoothness, float metallic, string key,
            bool twoSided = false)
        {
            string k = "x|" + key + "|" + Key(tint) + "|" + smoothness.ToString("0.##") + "|" + metallic.ToString("0.##") + Side(twoSided);
            return GetOrCreate(k, tint, diffuse, normal, Vector2.one, smoothness, metallic, twoSided);
        }

        private static string Side(bool twoSided) => twoSided ? "|2s" : "";

        /// <summary>
        /// Material del piso con URP Lit (sin shader propio): mapa de mezcla grande (alfa = suavidad, así el barro mojado brilla) y mapas de
        /// "Detail" repetidos a otra escala (keyword _DETAIL_MULX2: el albedo de detalle multiplica x2, por eso su gris medio es neutro).
        /// </summary>
        public static Material GetGround(Texture2D albedo, Texture2D normal, Texture2D detailAlbedo, Texture2D detailNormal, float detailTiles, float detailStrength)
        {
            var mat = NewMaterial(Color.white, 1f, 0f); // _Smoothness = 1 x alfa del albedo
            mat.name = "Ground";
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_SmoothnessTextureChannel", 1f);
            mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mat.SetTexture("_DetailAlbedoMap", detailAlbedo);
            mat.SetTexture("_DetailNormalMap", detailNormal);
            mat.SetTextureScale("_DetailAlbedoMap", new Vector2(detailTiles, detailTiles));
            mat.SetTextureScale("_DetailNormalMap", new Vector2(detailTiles, detailTiles));
            mat.SetFloat("_DetailAlbedoMapScale", detailStrength);
            mat.SetFloat("_DetailNormalMapScale", detailStrength);
            mat.EnableKeyword("_DETAIL_MULX2");
            return mat;
        }

        /// <summary>Tiling razonable para una pieza grande: una repetición cada ~3 metros.</summary>
        public static Vector2 TilingFor(Vector3 scale, float meters = 3f)
            => new Vector2(Mathf.Max(1f, Mathf.Round(Mathf.Max(scale.x, scale.z) / meters)), Mathf.Max(1f, Mathf.Round(scale.y / meters)));

        private static Material GetOrCreate(string key, Color color, Texture2D diff, Texture2D nor, Vector2 tiling,
            float smoothness, float metallic, bool twoSided = false)
        {
            if (Cache.TryGetValue(key, out var existing) && existing != null) return existing;
            var mat = NewMaterial(color, smoothness, metallic, twoSided);
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

        private static Material NewMaterial(Color color, float smoothness, float metallic, bool twoSided = false)
        {
            if (_template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _template = probe.GetComponent<Renderer>().sharedMaterial;
                if (Application.isPlaying) Object.Destroy(probe); else Object.DestroyImmediate(probe);   // fuera de Play (pruebas EditMode) Destroy no está permitido
            }
            var mat = new Material(_template) { enableInstancing = true };
            mat.SetColor("_BaseColor", color); // URP Lit
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            // Cull explícito (no se hereda de la plantilla): 2 = Back (sólidos cerrados), 0 = Off (láminas de una capa).
            // En URP Lit esta propiedad gobierna ForwardLit, ShadowCaster, DepthOnly y DepthNormals, así que las sombras
            // usan la misma cara que el color.
            mat.SetFloat("_Cull", twoSided ? 0f : 2f);
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

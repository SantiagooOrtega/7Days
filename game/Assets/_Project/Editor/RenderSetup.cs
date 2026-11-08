using UnityEditor;
using UnityEngine;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Activa "Frame Timing Stats" (Player Settings) para que el FrameTimingManager dé el tiempo de GPU también en una build: lo usan la resolución dinámica y
    /// el panel Render de F6. Se hace por la API del editor (no editando ProjectSettings a mano, que el editor abierto podría pisar).
    /// </summary>
    [InitializeOnLoad]
    public static class RenderSetup
    {
        static RenderSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                if (!PlayerSettings.enableFrameTimingStats)
                {
                    PlayerSettings.enableFrameTimingStats = true;
                    Debug.Log("[7Days] Activado Player Settings > Frame Timing Stats (tiempo de GPU para la resolución dinámica).");
                }
                EnsureVariantKeepers();
            };
        }

        private const string KeepDir = "Assets/_Project/Resources/Look/VariantKeepers";

        /// <summary>
        /// En una build, Unity quita las variantes de shader que no usa ningún material del proyecto; los materiales de personajes, armas y partículas se crean
        /// por código, así que se perderían (sin normales, sin máscara, sin fundido de la niebla). Estos materiales "guardavariantes" en Resources las conservan.
        /// </summary>
        [MenuItem("7Days/Crear materiales guardavariantes (build)")]
        public static void EnsureVariantKeepers()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (lit == null || particles == null) return;
            System.IO.Directory.CreateDirectory(KeepDir);
            int made = 0;
            made += Keep("Lit_Textured", lit, m => { m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP"); });
            made += Keep("Lit_Emissive", lit, m => m.EnableKeyword("_EMISSION"));
            made += Keep("Lit_TransparentEmissive", lit, m =>
            {
                m.SetFloat("_Surface", 1f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_EMISSION");
                m.SetFloat("_Cull", 0f); m.renderQueue = 3000;
            });
            made += Keep("Particles_Fading", particles, m => { m.SetFloat("_Surface", 1f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_FADING_ON"); });
            if (made > 0) { AssetDatabase.SaveAssets(); Debug.Log("[7Days] Creados " + made + " materiales guardavariantes en " + KeepDir); }
        }

        private static int Keep(string name, Shader shader, System.Action<Material> setup)
        {
            string path = KeepDir + "/" + name + ".mat";
            if (System.IO.File.Exists(path)) return 0;
            var m = new Material(shader) { name = name };
            setup(m);
            AssetDatabase.CreateAsset(m, path);
            return 1;
        }
    }
}

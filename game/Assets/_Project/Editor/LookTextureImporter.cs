using UnityEditor;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Configura automáticamente las texturas que se copien a Assets/_Project/Resources/Look:
    /// "*_nor" se importa como mapa de normales y todas se limitan a 1024 px para cuidar la memoria y los 60 FPS.
    /// </summary>
    public class LookTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Look/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Compressed;
            if (assetPath.Contains("_nor")) importer.textureType = TextureImporterType.NormalMap;
        }
    }
}

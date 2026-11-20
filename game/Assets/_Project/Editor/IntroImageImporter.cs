using UnityEditor;
using UnityEngine;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Configura las imágenes de la intro en cómic que se copien a Assets/_Project/Resources/Intro (intro_01 ... intro_07, PNG o JPG):
    /// sin mipmaps (siempre se ven a pantalla completa), compresión de alta calidad (BC7, no la normal, para que el texto dibujado no se emborrone),
    /// sRGB, máximo 2048 px y sin reescalar a potencia de 2 (si no, una imagen 16:9 se deformaría a 2048x1024).
    /// </summary>
    public class IntroImageImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Intro/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.wrapMode = TextureWrapMode.Clamp;   // el zoom no debe dejar ver el borde opuesto
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.isReadable = false;
        }
    }
}

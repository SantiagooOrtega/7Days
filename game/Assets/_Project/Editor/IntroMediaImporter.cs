using UnityEditor;
using UnityEngine;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Importación de las láminas del cómic (Intro/Resources) y de la música (Audio/Music):
    /// láminas sin mipmaps, sRGB, calidad alta (sin compresión agresiva), máx. 2048 px;
    /// música en streaming con Vorbis ~70 (no ocupa memoria y suena en bucle sin cortes).
    /// </summary>
    public class IntroMediaImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/_Project/Intro/Resources/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.sRGBTexture = true;
            t.mipmapEnabled = false;
            t.maxTextureSize = 2048;
            t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.alphaSource = TextureImporterAlphaSource.None;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/_Project/Audio/Music/")) return;
            var a = (AudioImporter)assetImporter;
            a.forceToMono = false;
            a.loadInBackground = true;
            var s = a.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.preloadAudioData = false;
            a.defaultSampleSettings = s;
        }
    }
}

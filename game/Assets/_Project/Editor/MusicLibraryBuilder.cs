using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SevenDays.Core;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Enlaza la música: al importar audios en Assets/_Project/Audio/Music (o al abrir el editor) rehace Resources/Audio/MusicLibrary con musica_juego
    /// (o el primer clip de la carpeta). Si la carpeta queda vacía, borra la lista y el juego queda en silencio. A mano: menú 7Days > Audio > Reconstruir música.
    /// </summary>
    [InitializeOnLoad]
    public class MusicLibraryBuilder : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Audio/Music";
        public const string LibraryPath = "Assets/_Project/Resources/Audio/MusicLibrary.asset";

        static MusicLibraryBuilder() { EditorApplication.delayCall += () => { if (AssetDatabase.LoadAssetAtPath<MusicLibrary>(LibraryPath) == null) Rebuild(); }; }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p => p.StartsWith(Folder + "/"))) Rebuild();
        }

        [MenuItem("7Days/Audio/Reconstruir música")]
        public static void Rebuild()
        {
            var clips = AssetDatabase.IsValidFolder(Folder)
                ? AssetDatabase.FindAssets("t:AudioClip", new[] { Folder }).Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).ToList()
                : new System.Collections.Generic.List<AudioClip>();
            var main = clips.FirstOrDefault(c => c.name == "musica_juego") ?? clips.FirstOrDefault();
            var lib = AssetDatabase.LoadAssetAtPath<MusicLibrary>(LibraryPath);
            if (main == null)
            {
                if (lib != null) { AssetDatabase.DeleteAsset(LibraryPath); Debug.Log("[7Days] Sin música: el juego queda en silencio."); }
                return;
            }
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<MusicLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            if (lib.main == main) return;
            lib.main = main;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[7Days] Música del juego: " + main.name);
        }
    }
}

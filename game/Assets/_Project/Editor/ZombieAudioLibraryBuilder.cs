using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SevenDays.Combat;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Clips de zombi propios (CC0): al meter o quitar audios en Assets/_Project/Audio/Zombies, rehace Resources/Audio/ZombieClipLibrary con los que tengan un
    /// nombre válido ("common_idle_1", "tank_roar_2", "any_headshot_1"...; ver docs/ASSETS.md). Si la carpeta queda vacía, borra la lista y vuelven los sintetizados.
    /// También a mano: menú 7Days > Audio > Reconstruir clips de zombi.
    /// </summary>
    public class ZombieAudioLibraryBuilder : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Audio/Zombies";
        public const string LibraryPath = "Assets/_Project/Resources/Audio/ZombieClipLibrary.asset";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p => p.StartsWith(Folder + "/"))) Rebuild();
        }

        [MenuItem("7Days/Audio/Reconstruir clips de zombi")]
        public static void Rebuild()
        {
            var clips = AssetDatabase.IsValidFolder(Folder)
                ? AssetDatabase.FindAssets("t:AudioClip", new[] { Folder }).Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).ToList()
                : new System.Collections.Generic.List<AudioClip>();
            var valid = clips.Where(c => ZombieSoundBank.ParseOverrideName(c.name, out _, out _)).ToList();
            foreach (var bad in clips.Except(valid)) Debug.LogWarning("[7Days] Clip de zombi con nombre no reconocido (se ignora): " + bad.name + ". Formato: tipo_evento_n, ver docs/ASSETS.md");
            var lib = AssetDatabase.LoadAssetAtPath<ZombieClipLibrary>(LibraryPath);
            if (valid.Count == 0)
            {
                if (lib != null) { AssetDatabase.DeleteAsset(LibraryPath); Debug.Log("[7Days] Sin clips de zombi propios: se usan los sintetizados."); }
                return;
            }
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<ZombieClipLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.clips = valid;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[7Days] Clips de zombi propios: " + valid.Count + " (tienen prioridad sobre los sintetizados).");
        }
    }
}

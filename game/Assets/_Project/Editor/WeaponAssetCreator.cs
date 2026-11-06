using System.IO;
using UnityEditor;
using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Crea un ScriptableObject por arma en Assets/_Project/Resources/Weapons la primera vez que Unity compila (solo los que falten: nunca pisa los que ya
    /// editaste). También en el menú 7Days > Crear armas por defecto (ScriptableObjects).
    /// </summary>
    [InitializeOnLoad]
    public static class WeaponAssetCreator
    {
        private const string Dir = "Assets/_Project/Resources/Weapons";

        static WeaponAssetCreator()
        {
            EditorApplication.delayCall += () => { if (!Application.isPlaying) CreateMissing(false); };
        }

        [MenuItem("7Days/Crear armas por defecto (ScriptableObjects)")]
        public static void CreateFromMenu() => CreateMissing(true);

        private static void CreateMissing(bool verbose)
        {
            Directory.CreateDirectory(Dir);
            int created = 0;
            foreach (var data in WeaponCatalog.Defaults())
            {
                string path = Dir + "/" + data.id + ".asset";
                if (File.Exists(path)) continue;
                var asset = ScriptableObject.CreateInstance<WeaponAsset>();
                asset.Data = data;
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }
            if (created > 0) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("[7Days] Creados " + created + " ScriptableObjects de armas en " + Dir); }
            else if (verbose) Debug.Log("[7Days] Ya existen los ScriptableObjects de armas en " + Dir);
        }
    }
}

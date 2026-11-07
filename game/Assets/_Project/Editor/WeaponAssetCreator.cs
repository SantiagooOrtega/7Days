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
            int created = 0, upgraded = 0;
            foreach (var data in WeaponCatalog.Defaults())
            {
                string path = Dir + "/" + data.id + ".asset";
                if (File.Exists(path))
                {
                    // datos de una versión anterior (sin retroceso/dispersión/apuntado nuevos y con el daño viejo): se actualizan a los valores por defecto
                    var existing = AssetDatabase.LoadAssetAtPath<WeaponAsset>(path);
                    if (existing != null && (existing.Data == null || existing.Data.dataVersion < WeaponData.CurrentDataVersion))
                    {
                        // versión reciente: se migra conservando lo editado a mano; muy antigua: valores por defecto
                        if (existing.Data == null || !WeaponCatalog.Migrate(existing.Data)) existing.Data = data;
                        EditorUtility.SetDirty(existing);
                        upgraded++;
                    }
                    continue;
                }
                var asset = ScriptableObject.CreateInstance<WeaponAsset>();
                asset.Data = data;
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }
            if (upgraded > 0) Debug.Log("[7Days] Actualizados " + upgraded + " ScriptableObjects de armas de una versión anterior (valores por defecto nuevos).");
            if (created > 0 || upgraded > 0) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("[7Days] Creados " + created + " ScriptableObjects de armas en " + Dir); }
            else if (verbose) Debug.Log("[7Days] Ya existen los ScriptableObjects de armas en " + Dir);
        }
    }
}

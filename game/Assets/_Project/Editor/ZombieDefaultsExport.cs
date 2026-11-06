using System.IO;
using UnityEditor;
using UnityEngine;
using SevenDays.Combat;
using SevenDays.Procedural;

namespace SevenDays.EditorTools
{
    /// <summary>Menú 7Days > Exportar zombis por defecto: escribe Resources/Zombies/zombies.json para editarlo a mano (tiene prioridad sobre el catálogo del código).</summary>
    public static class ZombieDefaultsExport
    {
        [MenuItem("7Days/Exportar zombis por defecto (JSON)")]
        public static void Export()
        {
            string dir = "Assets/_Project/Resources/Zombies";
            Directory.CreateDirectory(dir);
            string path = dir + "/zombies.json";
            if (File.Exists(path) && !EditorUtility.DisplayDialog("zombies.json", "Ya existe. ¿Sobrescribir con los valores por defecto?", "Sobrescribir", "Cancelar")) return;
            File.WriteAllText(path, ZombieIO.ToJson(ZombieCatalog.Defaults()));
            AssetDatabase.Refresh();
            Debug.Log("[7Days] Escrito " + path);
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using SevenDays.Look;
using SevenDays.World;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Menú 7Days > Mapas > Exportar mapa 1: escribe Resources/Maps/calle_en_ruinas.json con el mapa 1 generado por los valores por defecto (el mismo mapa que se
    /// construía antes por código). También sirve en batch: -executeMethod SevenDays.EditorTools.MapExport.ExportStreetRuinsBatch
    /// </summary>
    public static class MapExport
    {
        public const string Folder = "Assets/_Project/Resources/Maps";

        [MenuItem("7Days/Mapas/Exportar mapa 1 (Calle en ruinas) a JSON")]
        public static void ExportStreetRuins()
        {
            string path = Write();
            AssetDatabase.Refresh();
            Debug.Log("[7Days] Escrito " + path);
        }

        [MenuItem("7Days/Mapas/Exportar los 3 mapas a JSON")]
        public static void ExportAll()
        {
            foreach (var p in WriteAll()) Debug.Log("[7Days] Escrito " + p);
            AssetDatabase.Refresh();
        }

        public static void ExportStreetRuinsBatch() { Write(); }
        public static void ExportAllBatch() { WriteAll(); }

        private static string[] WriteAll() => new[] { Write(), Write(SevenDays.Procedural.MapLibrary.Neighborhood()), Write(SevenDays.Procedural.MapLibrary.Forest()) };

        private static string Write(SevenDays.Procedural.MapData map = null)
        {
            Directory.CreateDirectory(Folder);
            map ??= MapDefaults.StreetRuins(new LookSettings());
            string path = Folder + "/" + map.id + ".json";
            File.WriteAllText(path, MapIO.ToJson(map));
            return path;
        }
    }
}

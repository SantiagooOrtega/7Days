using System.IO;
using UnityEditor;
using UnityEngine;
using SevenDays.Procedural;
using SevenDays.World;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Menú 7Days > Exportar layout de props (JSON): genera la distribución por defecto y la guarda como JSON, lista para
    /// copiarla a un mapa de la base de datos o para ponerla en Resources/Layouts/props.json y editarla a mano.
    /// </summary>
    public static class PropLayoutExport
    {
        [MenuItem("7Days/Exportar layout de props (JSON)")]
        public static void Export()
        {
            var layout = VegetationLayout.Generate(new VegetationParams(), IndustrialRuins.Exclusions());
            string dir = "Assets/_Project/Resources/Layouts";
            Directory.CreateDirectory(dir);
            string path = dir + "/props_default.json";
            File.WriteAllText(path, PropLayoutIO.ToJson(layout));
            AssetDatabase.Refresh();
            Debug.Log("[7Days] Layout exportado: " + path + " (" + layout.items.Length + " piezas). Renómbralo a props.json para que el juego lo use.");
        }

        [MenuItem("7Days/Exportar casa (JSON)")]
        public static void ExportHouse()
        {
            string dir = "Assets/_Project/Resources/Layouts";
            Directory.CreateDirectory(dir);
            string path = dir + "/house_default.json";
            File.WriteAllText(path, HouseIO.ToJson(HouseLayoutData.Default()));
            AssetDatabase.Refresh();
            Debug.Log("[7Days] Casa exportada: " + path + ". Renómbralo a house.json para que el juego lo use.");
        }
    }
}

using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Repara ScriptableObjects de Resources/Config que quedaron con "m_Script: {fileID: 0}" (script desvinculado). Pasó con CameraRunSettings: su clase vivía en
    /// CameraConfigs.cs y Unity solo enlaza un ScriptableObject a un archivo que se llama como la clase. Lee la clase de m_EditorClassIdentifier, busca su MonoScript
    /// y reescribe solo la línea m_Script: los valores guardados no se tocan. Corre solo al abrir el editor; también en el menú 7Days > Reparar scripts de los ajustes.
    /// </summary>
    [InitializeOnLoad]
    public static class MissingScriptRepair
    {
        private const string Dir = "Assets/_Project/Resources/Config";

        static MissingScriptRepair() { EditorApplication.delayCall += () => { if (!Application.isPlaying) Repair(false); }; }

        [MenuItem("7Days/Reparar scripts de los ajustes (ScriptableObjects)")]
        public static void RepairFromMenu() => Repair(true);

        private static void Repair(bool verbose)
        {
            if (!Directory.Exists(Dir)) return;
            int fixedCount = 0;
            foreach (var path in Directory.GetFiles(Dir, "*.asset"))
            {
                string text = File.ReadAllText(path);
                if (!text.Contains("m_Script: {fileID: 0}")) continue;
                var m = Regex.Match(text, @"m_EditorClassIdentifier:\s*[^:\s]*:[^:\s]*:(\w+)");
                if (!m.Success) { Debug.LogWarning("[7Days] " + path + " no tiene script y no se sabe su clase: bórralo y se volverá a crear."); continue; }
                string cls = m.Groups[1].Value;
                string guid = null;
                foreach (var g in AssetDatabase.FindAssets(cls + " t:MonoScript"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                    if (script != null && script.GetClass() != null && script.GetClass().Name == cls) { guid = g; break; }
                }
                if (guid == null) { Debug.LogWarning("[7Days] No se encontró el script de la clase " + cls + " para " + path + "."); continue; }
                text = text.Replace("m_Script: {fileID: 0}", "m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}");
                File.WriteAllText(path, text);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                fixedCount++;
                Debug.Log("[7Days] Reparado " + path + ": vuelve a usar el script " + cls + " (valores conservados).");
            }
            if (fixedCount == 0 && verbose) Debug.Log("[7Days] Ningún ajuste tenía el script desvinculado.");
        }
    }
}

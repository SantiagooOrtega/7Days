using UnityEditor;
using UnityEngine;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Crea la capa "Hitbox" en ProjectSettings/TagManager si no existe (los hitboxes de los zombies viven ahí). Usa la capa 31 si está libre,
    /// que es la misma que el código usa como respaldo; si no, la primera libre desde la 8.
    /// </summary>
    [InitializeOnLoad]
    public static class HitboxLayerSetup
    {
        private const string LayerName = "Hitbox";

        static HitboxLayerSetup() { EditorApplication.delayCall += Ensure; }

        [MenuItem("7Days/Crear capa Hitbox")]
        public static void Ensure()
        {
            if (LayerMask.NameToLayer(LayerName) >= 0) return;
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null) return;
            int slot = -1;
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(31).stringValue)) slot = 31;
            else for (int i = 8; i < 32; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { slot = i; break; }
            if (slot < 0) { Debug.LogWarning("[7Days] No hay capas libres para 'Hitbox'."); return; }
            layers.GetArrayElementAtIndex(slot).stringValue = LayerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[7Days] Capa '" + LayerName + "' creada en el índice " + slot + ".");
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using SevenDays.Player;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Crea Resources/Config/CameraSettings.asset y CameraRunSettings.asset la primera vez que Unity compila (nunca pisa los que ya editaste).
    /// Menú 7Days > Crear ajustes de cámara.
    /// </summary>
    [InitializeOnLoad]
    public static class CameraConfigCreator
    {
        private const string Dir = "Assets/_Project/Resources/Config";

        static CameraConfigCreator() { EditorApplication.delayCall += () => { if (!Application.isPlaying) CreateMissing(false); }; }

        [MenuItem("7Days/Crear ajustes de cámara (ScriptableObjects)")]
        public static void CreateFromMenu() => CreateMissing(true);

        private static void CreateMissing(bool verbose)
        {
            Directory.CreateDirectory(Dir);
            int created = 0;
            created += Create<CameraSettings>(Dir + "/CameraSettings.asset");
            created += Create<CameraRunSettings>(Dir + "/CameraRunSettings.asset");
            if (created > 0) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("[7Days] Creados " + created + " ajustes de cámara en " + Dir); }
            else if (verbose) Debug.Log("[7Days] Ya existen los ajustes de cámara en " + Dir);
        }

        private static int Create<T>(string path) where T : ScriptableObject
        {
            if (File.Exists(path)) return 0;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
            return 1;
        }
    }
}

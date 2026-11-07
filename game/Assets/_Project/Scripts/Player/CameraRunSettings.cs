using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// ScriptableObject de la cámara al correr (estilo Gears of War): FOV, acercamiento, balanceo, roll, rebote, fatiga, jadeo y límites duros.
    /// Assets/_Project/Resources/Config/CameraRunSettings.asset (se crea solo; menú 7Days > Crear ajustes de cámara).
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Cámara de carrera", fileName = "CameraRunSettings")]
    public class CameraRunSettings : ScriptableObject
    {
        public const string ResourcePath = "Config/CameraRunSettings";
        public CameraRunSettingsData Settings = new CameraRunSettingsData();

        public static CameraRunSettingsData Load()
        {
            var a = Resources.Load<CameraRunSettings>(ResourcePath);
            var s = a != null ? JsonUtility.FromJson<CameraRunSettingsData>(JsonUtility.ToJson(a.Settings)) : new CameraRunSettingsData();
            s.Validate();
            return s;
        }
    }
}

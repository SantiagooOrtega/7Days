using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// ScriptableObject con los ajustes generales de la cámara (sensibilidad, límites de pitch, suavizado, hombro, sacudidas). Opcional: sin asset se usan los
    /// valores por defecto. Se crea solo en Assets/_Project/Resources/Config/CameraSettings.asset (menú 7Days > Crear ajustes de cámara).
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Ajustes de cámara", fileName = "CameraSettings")]
    public class CameraSettings : ScriptableObject
    {
        public const string ResourcePath = "Config/CameraSettings";
        public CameraSettingsData Settings = new CameraSettingsData();

        public static CameraSettingsData Load()
        {
            var a = Resources.Load<CameraSettings>(ResourcePath);
            // copia: lo que se toca en el panel F5 durante el juego no modifica el asset
            var s = a != null ? JsonUtility.FromJson<CameraSettingsData>(JsonUtility.ToJson(a.Settings)) : new CameraSettingsData();
            s.motionScale = PlayerPrefs.GetFloat(MotionPrefKey, s.motionScale);
            s.sensitivity = PlayerPrefs.GetFloat(SensitivityPrefKey, s.sensitivity);
            s.aimToggle = PlayerPrefs.GetInt(AimTogglePrefKey, s.aimToggle ? 1 : 0) == 1;
            s.Validate();
            return s;
        }

        /// <summary>Opción de accesibilidad "Movimiento de cámara" (0..1) y sensibilidad, guardadas por jugador.</summary>
        public const string MotionPrefKey = "7days.cameraMotion", SensitivityPrefKey = "7days.sensitivity", AimTogglePrefKey = "7days.aimToggle";
    }

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

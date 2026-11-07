using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// ScriptableObject con la pose de locomoción (flexión de rodilla por fase, muslo, rodar del pie, absorción, inclinación, media rodilla). Opcional: sin asset
    /// se usan los valores por defecto. Se crea solo en Resources/Config/LocomotionPoseSettings.asset; los valores también se ajustan en vivo en F6.
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Pose de locomoción", fileName = "LocomotionPoseSettings")]
    public class LocomotionPoseSettings : ScriptableObject
    {
        public const string ResourcePath = "Config/LocomotionPoseSettings";
        public LocomotionPoseData Settings = new LocomotionPoseData();

        public static LocomotionPoseData Load()
        {
            var a = Resources.Load<LocomotionPoseSettings>(ResourcePath);
            var s = a != null ? JsonUtility.FromJson<LocomotionPoseData>(JsonUtility.ToJson(a.Settings)) : new LocomotionPoseData();
            s.Validate();
            return s;
        }
    }
}

using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// ScriptableObject con la complexión del soldado (altura, hombros, cadera, grosor general, del torso y de las extremidades, cabeza, brazos y piernas). Opcional:
    /// sin asset se usa RigProportions.Soldier(). Se crea solo en Resources/Config/ProportionSettings.asset; también se ajusta en vivo en F6 ("Aplicar complexión").
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Complexión del personaje", fileName = "ProportionSettings")]
    public class ProportionSettings : ScriptableObject
    {
        public const string ResourcePath = "Config/ProportionSettings";
        public RigProportions Settings = RigProportions.Soldier();

        public static RigProportions Load()
        {
            var a = Resources.Load<ProportionSettings>(ResourcePath);
            var p = a != null ? JsonUtility.FromJson<RigProportions>(JsonUtility.ToJson(a.Settings)) : RigProportions.Soldier();
            p.Validate();
            return p;
        }
    }
}

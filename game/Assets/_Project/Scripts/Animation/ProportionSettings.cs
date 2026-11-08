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
        /// <summary>Versión de los valores (0 si el asset es anterior a este campo: era la esbelta). 2 = soldado atlético y fuerte. Los campos que faltan en un asset viejo valen 0, no el actual.</summary>
        public int version;
        public const int CurrentVersion = 2;

        /// <summary>Proporciones base (sin el grosor de la variante). Un asset de la versión 1 con los valores esbeltos de fábrica pasa a los nuevos; si lo editaste a mano, se respeta.</summary>
        public static RigProportions Load()
        {
            var a = Resources.Load<ProportionSettings>(ResourcePath);
            var p = a != null ? JsonUtility.FromJson<RigProportions>(JsonUtility.ToJson(a.Settings)) : RigProportions.Soldier();
            if (a != null && a.version < CurrentVersion) Migrate(p);
            p.Validate();
            return p;
        }

        /// <summary>Proporciones de una variante: la base con su grosor.</summary>
        public static RigProportions For(CharacterVariantData v) => Load().WithBuild(v != null ? v.buildScale : 1f);

        private static void Migrate(RigProportions p)
        {
            const float e = 0.0015f;
            if (Mathf.Abs(p.shoulderWidth - RigProportions.SlimShoulders) < e) p.shoulderWidth = RigProportions.SoldierShoulders;
            if (Mathf.Abs(p.hipWidth - RigProportions.SlimHips) < e) p.hipWidth = RigProportions.SoldierHips;
            if (Mathf.Abs(p.bulk - RigProportions.SlimBulk) < e) p.bulk = RigProportions.SoldierBulk;
            if (Mathf.Abs(p.limbThickness - RigProportions.SlimLimbs) < e) p.limbThickness = RigProportions.SoldierLimbs;
        }
    }
}

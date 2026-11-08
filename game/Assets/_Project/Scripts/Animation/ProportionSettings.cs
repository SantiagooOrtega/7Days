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
        /// <summary>Versión de los valores (0 si el asset es anterior a este campo: era la esbelta). 2 = soldado atlético y fuerte, 3 = musculoso (deltoides, bíceps, cuádriceps, gemelo). Los campos que faltan en un asset viejo valen 0, no el actual.</summary>
        public int version;
        public const int CurrentVersion = 3;

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

        private static bool Near(float a, float b, float e) => Mathf.Abs(a - b) < e;

        /// <summary>Pone al día unas proporciones de una versión anterior (público para las pruebas).</summary>
        public static void Migrate(RigProportions p)
        {
            const float e = 0.0015f;
            // los valores de fábrica de las versiones anteriores (esbelta y fuerte) pasan a los actuales; lo editado a mano se respeta
            if (Near(p.shoulderWidth, RigProportions.SlimShoulders, e) || Near(p.shoulderWidth, RigProportions.V2Shoulders, e)) p.shoulderWidth = RigProportions.SoldierShoulders;
            if (Near(p.hipWidth, RigProportions.SlimHips, e)) p.hipWidth = RigProportions.SoldierHips;
            if (Near(p.bulk, RigProportions.SlimBulk, e)) p.bulk = RigProportions.SoldierBulk;
            if (Near(p.limbThickness, RigProportions.SlimLimbs, e) || Near(p.limbThickness, RigProportions.V2Limbs, e)) p.limbThickness = RigProportions.SoldierLimbs;
        }
    }
}

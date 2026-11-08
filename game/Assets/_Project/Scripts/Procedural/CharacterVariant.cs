using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum Headgear { SkullHelmet, CapGoggles, MedicHelmet, SniperHood }

    /// <summary>
    /// Variante jugable del soldado (pura y serializable con JsonUtility, validable desde JSON para el backend o el contenido offline): pieza de cabeza, paleta
    /// (camuflaje de 4 colores, equipo, cuero, botas, protecciones, metal, casco, pintura y acento) y estadísticas. Colores en sRGB 0..1 como [r, g, b].
    /// Todas comparten la misma base (esqueleto, cuerpo, chaleco); cambian la cabeza, los colores y unos pocos números.
    /// </summary>
    [Serializable]
    public class CharacterVariantData
    {
        public string id = "calavera";
        public string displayName = "Calavera";
        public Headgear headgear = Headgear.SkullHelmet;
        public bool visor = true;          // visor ahumado sobre las cuencas (solo con la placa calavera)
        public bool armband;               // brazalete (médico)
        public int seed = 7;
        public float buildScale = 1f;      // multiplica el grosor del cuerpo (ProportionSettings.bulk): la Calavera es algo más corpulenta

        // camuflaje woodland oscuro: base, manchas medianas, manchas oscuras, trazos grises
        public float[] camoBase = { 0.24f, 0.26f, 0.19f };
        public float[] camoMid = { 0.27f, 0.22f, 0.16f };
        public float[] camoDark = { 0.09f, 0.09f, 0.08f };
        public float[] camoLight = { 0.36f, 0.36f, 0.33f };
        public float camoScale = 1f;

        public float[] gear = { 0.21f, 0.22f, 0.17f };       // nylon del chaleco y portacargadores
        public float[] gearDark = { 0.12f, 0.12f, 0.11f };   // correas, cinturón, velcro
        public float[] leather = { 0.11f, 0.1f, 0.09f };     // guantes
        public float[] boot = { 0.18f, 0.14f, 0.1f };
        public float[] pad = { 0.15f, 0.15f, 0.14f };        // rodilleras, coderas, nudillos
        public float[] metal = { 0.16f, 0.16f, 0.17f };      // herrajes, rieles
        public float[] helmet = { 0.2f, 0.21f, 0.17f };      // cubierta del casco
        public float[] paint = { 0.82f, 0.8f, 0.74f };       // pintura blanca gastada de la calavera
        public float[] accent = { 0.62f, 0.3f, 0.12f };      // naranja apagado (franjas, cintas)
        public float[] fabric = { 0.3f, 0.27f, 0.2f };       // gorra, pañuelo, capucha o red

        // estadísticas (la Calavera es la referencia: todo 1)
        public float maxHealth = 100f;
        public float moveSpeed = 1f;       // multiplica la velocidad de movimiento
        public float healing = 1f;         // multiplica lo que curan botiquines y vendas
        public float weaponSway = 1f;      // multiplica el balanceo del arma al mover la mira

        public static Rgb C(float[] c) => c != null && c.Length >= 3 ? new Rgb(c[0], c[1], c[2]) : new Rgb(0.5f, 0.5f, 0.5f);

        /// <summary>Corrige valores imposibles (colores fuera de 0..1, estadísticas fuera de rango) y rellena los que falten.</summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(id)) id = "variante";
            if (string.IsNullOrEmpty(displayName)) displayName = id;
            var d = new CharacterVariantData();
            camoBase = Fix(camoBase, d.camoBase); camoMid = Fix(camoMid, d.camoMid); camoDark = Fix(camoDark, d.camoDark); camoLight = Fix(camoLight, d.camoLight);
            gear = Fix(gear, d.gear); gearDark = Fix(gearDark, d.gearDark); leather = Fix(leather, d.leather); boot = Fix(boot, d.boot); pad = Fix(pad, d.pad);
            metal = Fix(metal, d.metal); helmet = Fix(helmet, d.helmet); paint = Fix(paint, d.paint); accent = Fix(accent, d.accent); fabric = Fix(fabric, d.fabric);
            camoScale = Clamp(camoScale, 0.5f, 2f);
            buildScale = Clamp(buildScale, 0.9f, 1.1f);
            maxHealth = Clamp(maxHealth, 60f, 140f);
            moveSpeed = Clamp(moveSpeed, 0.85f, 1.15f);
            healing = Clamp(healing, 0.5f, 1.6f);
            weaponSway = Clamp(weaponSway, 0.5f, 1.5f);
            if (headgear != Headgear.SkullHelmet) visor = false;
        }

        private static float[] Fix(float[] c, float[] fallback)
        {
            if (c == null || c.Length < 3) return (float[])fallback.Clone();
            return new[] { Clamp(c[0], 0f, 1f), Clamp(c[1], 0f, 1f), Clamp(c[2], 0f, 1f) };
        }

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : (v < lo ? lo : (v > hi ? hi : v));

        public CharacterVariantData Clone() => (CharacterVariantData)MemberwiseClone();
    }

    public static class CharacterVariants
    {
        /// <summary>Las variantes jugables por defecto (los ScriptableObject de Resources/Config/Variants las pueden sustituir).</summary>
        public static List<CharacterVariantData> Defaults() => new List<CharacterVariantData>
        {
            new CharacterVariantData { id = "calavera", displayName = "Calavera", headgear = Headgear.SkullHelmet, visor = true, seed = 7, buildScale = 1.04f },
            new CharacterVariantData
            {
                id = "explorador", displayName = "Explorador", headgear = Headgear.CapGoggles, visor = false, seed = 21, buildScale = 0.97f,
                camoBase = new[] { 0.42f, 0.4f, 0.31f }, camoMid = new[] { 0.36f, 0.3f, 0.21f }, camoDark = new[] { 0.2f, 0.19f, 0.15f }, camoLight = new[] { 0.55f, 0.52f, 0.43f },
                gear = new[] { 0.38f, 0.33f, 0.24f }, gearDark = new[] { 0.22f, 0.2f, 0.16f }, boot = new[] { 0.3f, 0.23f, 0.16f },
                fabric = new[] { 0.36f, 0.32f, 0.24f }, accent = new[] { 0.55f, 0.24f, 0.14f },
                maxHealth = 90f, moveSpeed = 1.05f,
            },
            new CharacterVariantData
            {
                id = "medico", displayName = "Médico", headgear = Headgear.MedicHelmet, visor = false, armband = true, seed = 33,
                camoBase = new[] { 0.27f, 0.29f, 0.27f }, camoMid = new[] { 0.21f, 0.23f, 0.22f }, camoDark = new[] { 0.1f, 0.11f, 0.11f }, camoLight = new[] { 0.4f, 0.41f, 0.39f },
                gear = new[] { 0.19f, 0.21f, 0.2f }, helmet = new[] { 0.24f, 0.25f, 0.23f }, fabric = new[] { 0.16f, 0.17f, 0.17f }, accent = new[] { 0.58f, 0.22f, 0.14f },
                healing = 1.3f,
            },
            new CharacterVariantData
            {
                id = "francotirador", displayName = "Francotirador", headgear = Headgear.SniperHood, visor = false, seed = 47, camoScale = 0.8f,
                camoBase = new[] { 0.2f, 0.21f, 0.15f }, camoMid = new[] { 0.25f, 0.2f, 0.13f }, camoDark = new[] { 0.08f, 0.08f, 0.06f }, camoLight = new[] { 0.31f, 0.3f, 0.22f },
                gear = new[] { 0.18f, 0.18f, 0.13f }, fabric = new[] { 0.23f, 0.22f, 0.15f },
                maxHealth = 95f, weaponSway = 0.8f,
            },
        };

        public static CharacterVariantData Find(IEnumerable<CharacterVariantData> list, string id)
        {
            foreach (var v in list) if (v.id == id) return v;
            return null;
        }
    }
}

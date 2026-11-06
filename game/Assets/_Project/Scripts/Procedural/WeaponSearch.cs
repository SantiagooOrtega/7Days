using System;
using System.Collections.Generic;
using System.Linq;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Armas al buscar (datos, validables con zod): probabilidad baja pero real por tipo de contenedor, más en la casa y en zonas de riesgo; "pity timer" (si llevas
    /// pityStart búsquedas sin arma sube la probabilidad hasta asegurarla en pityGuarantee); rareza ponderada (comunes más frecuentes, épicas muy raras).
    /// </summary>
    [Serializable]
    public class WeaponSearchConfig
    {
        public float rubble = 0.08f, bush = 0.09f, car = 0.11f, crate = 0.12f, furniture = 0.15f;
        public float riskBonus = 0.03f;      // por nivel de riesgo por encima de 1 (zonas expuestas)
        public float maxChance = 0.25f;      // tope antes del pity
        public int pityStart = 7;            // búsquedas seguidas sin arma antes de empezar a subir la probabilidad
        public float pityStep = 0.06f;       // lo que sube por cada búsqueda extra
        public int pityGuarantee = 15;       // a esta racha sin arma, la siguiente búsqueda la da seguro
        public float[] rarityWeights = { 50f, 35f, 12f, 2f };       // común, poco común, rara, épica
        public float[] riskRarityWeights = { 20f, 35f, 33f, 12f };  // zonas de riesgo 2: mejor rareza
        public int duplicateAmmo = 8;        // si ya tienes todas las armas posibles: munición en vez de arma
        public int guaranteedMin = 3;        // armas colocadas en el mapa en cada día (casa, lejos y expuesta, contenedor)
    }

    /// <summary>Contadores de las búsquedas de la partida (para el pity timer y las estadísticas).</summary>
    public class SearchState
    {
        public int Searches, SearchesWithoutWeapon, WeaponsFound;
    }

    public static class WeaponSearch
    {
        public static float ContainerChance(WeaponSearchConfig c, LootPoint p)
        {
            float b;
            switch (p.container)
            {
                case "rubble": b = c.rubble; break;
                case "bush": b = c.bush; break;
                case "car": b = c.car; break;
                case "furniture": b = c.furniture; break;
                case "crate": b = c.crate; break;
                default: return 0f;   // el suelo no se "busca": no da armas por esta vía
            }
            b += Math.Max(0, p.risk - 1) * c.riskBonus;
            return Math.Min(c.maxChance, b);
        }

        /// <summary>Probabilidad con el pity timer aplicado.</summary>
        public static float WithPity(WeaponSearchConfig c, float baseChance, int searchesWithoutWeapon)
        {
            if (searchesWithoutWeapon >= c.pityGuarantee) return 1f;
            if (searchesWithoutWeapon < c.pityStart) return baseChance;
            return Math.Min(1f, baseChance + c.pityStep * (searchesWithoutWeapon - c.pityStart + 1));
        }

        /// <summary>Arma que el jugador NO tiene, por rareza ponderada (si no hay de la rareza elegida, se reparte entre las que sí tengan candidatos). Null si ya tiene todas.</summary>
        public static string PickWeapon(WeaponSearchConfig c, int risk, ICollection<string> owned, Random rng)
        {
            var all = WeaponCatalog.Defaults().Where(w => !owned.Contains(w.id)).ToList();
            if (all.Count == 0) return null;
            var weights = risk >= 2 ? c.riskRarityWeights : c.rarityWeights;
            var rarities = all.Select(w => (int)w.rarity).Distinct().ToList();
            double total = rarities.Sum(r => (double)Math.Max(0f, r < weights.Length ? weights[r] : 0f));
            if (total <= 0) return all[rng.Next(all.Count)].id;
            double roll = rng.NextDouble() * total, acc = 0;
            int chosen = rarities[rarities.Count - 1];
            foreach (int r in rarities) { acc += Math.Max(0f, r < weights.Length ? weights[r] : 0f); if (roll <= acc) { chosen = r; break; } }
            var pool = all.Where(w => (int)w.rarity == chosen).ToList();
            return pool[rng.Next(pool.Count)].id;
        }

        /// <summary>
        /// Resultado de buscar en un contenedor: ¿toca arma? (garantizada, o probabilidad del contenedor con pity). Si toca y quedan armas sin tener, da una;
        /// si no toca, tira en la tabla del punto SIN armas (la munición y los botiquines quedan como estaban).
        /// </summary>
        public static LootItem RollSearch(LootLayoutData l, LootPoint p, Random rng, ICollection<string> owned, SearchState st, out bool gaveWeapon)
        {
            var cfg = l.weaponSearch ?? new WeaponSearchConfig();
            gaveWeapon = false;
            st.Searches++;
            bool hit = p.guaranteeWeapon || rng.NextDouble() < WithPity(cfg, ContainerChance(cfg, p), st.SearchesWithoutWeapon);
            if (hit)
            {
                string id = PickWeapon(cfg, p.risk, owned, rng);
                if (id != null) { gaveWeapon = true; st.SearchesWithoutWeapon = 0; st.WeaponsFound++; return LootItem.Weapon(id); }
                // ya tiene todas: munición
                st.SearchesWithoutWeapon = 0;
                return LootItem.Ammo(AmmoType.Rifle, cfg.duplicateAmmo);
            }
            st.SearchesWithoutWeapon++;
            return LootRoller.Roll(l, p.table, rng, Rarity.Common, noWeapons: true);
        }
    }

    public static class WeaponGuarantee
    {
        /// <summary>
        /// Cuántos puntos de arma garantizada hay que volver a armar al empezar un día para que haya al menos "minimum" armas disponibles en el mapa
        /// (puntos garantizados sin buscar + armas sueltas en el suelo), sin pasar de las armas que el jugador aún no tiene.
        /// </summary>
        public static int ToRearm(int availableGuaranteedPoints, int weaponPickupsOnGround, int unownedWeapons, int minimum)
        {
            int available = availableGuaranteedPoints + weaponPickupsOnGround;
            int target = Math.Min(minimum, unownedWeapons);
            return Math.Max(0, target - available);
        }
    }
}

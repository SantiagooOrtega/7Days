using System.Collections.Generic;
using System.Linq;

namespace SevenDays.Procedural
{
    /// <summary>Botín por defecto: tablas de objetos, drops por zombie y puntos repartidos (casa, coches, escombros, arbustos, cajas expuestas y objetos sueltos).</summary>
    public static class LootDefaults
    {
        private static LootEntry E(string kind, string id, int amount, float weight) => new LootEntry { kind = kind, id = id, amount = amount, weight = weight };

        public static LootLayoutData Create(IList<PropEntry> props = null, HouseLayoutData house = null)
        {
            var l = new LootLayoutData();
            l.tables = new[]
            {
                new LootTable { id = "ground", entries = new[] { E("ammo", "pistol", 4, 3), E("medkit", "bandage", 1, 3), E("ammo", "shotgun", 2, 1), E("ammo", "rifle", 5, 1) } },
                new LootTable { id = "low", entries = new[] { E("ammo", "pistol", 5, 4), E("medkit", "bandage", 1, 4), E("ammo", "shotgun", 2, 1), E("medkit", "small", 1, 1), E("ammo", "rifle", 4, 1) } },
                new LootTable { id = "mid", entries = new[] {
                    E("ammo", "pistol", 7, 3), E("ammo", "rifle", 6, 2), E("ammo", "shotgun", 3, 2), E("medkit", "small", 1, 2), E("medkit", "bandage", 1, 2),
                    E("weapon", "revolver", 1, 1), E("weapon", "smg", 1, 1), E("weapon", "shotgun", 1, 1) } },
                new LootTable { id = "high", entries = new[] {
                    E("ammo", "rifle", 10, 3), E("ammo", "pistol", 8, 2), E("ammo", "shotgun", 4, 2), E("medkit", "medium", 1, 2), E("medkit", "large", 1, 0.7f),
                    E("weapon", "assault_rifle", 1, 1), E("weapon", "smg", 1, 1), E("weapon", "sniper", 1, 0.35f), E("weapon", "revolver", 1, 1) } },
                new LootTable { id = "drop_common", entries = new[] { E("medkit", "bandage", 1, 3), E("ammo", "pistol", 3, 3) } },
                new LootTable { id = "drop_special", entries = new[] { E("ammo", "pistol", 4, 3), E("ammo", "shotgun", 2, 2), E("ammo", "rifle", 4, 2), E("medkit", "small", 1, 2), E("medkit", "bandage", 1, 2) } },
                new LootTable { id = "drop_boss", entries = new[] { E("medkit", "medium", 1, 2), E("medkit", "large", 1, 1), E("weapon", "assault_rifle", 1, 1), E("weapon", "sniper", 1, 0.5f), E("ammo", "rifle", 15, 2) } },
            };
            l.drops = new[]
            {
                new DropRule { zombie = "common", chance = 0.06f, table = "drop_common" },
                new DropRule { zombie = "runner", chance = 0.12f, table = "drop_special" },
                new DropRule { zombie = "brute", chance = 0.22f, table = "drop_special" },
                new DropRule { zombie = "spitter", chance = 0.25f, table = "drop_special" },
                new DropRule { zombie = "boss", chance = 1f, table = "drop_boss", guaranteed = true, minRarity = (int)Rarity.Rare },
            };

            var pts = new List<LootPoint>();
            void P(string id, float x, float z, string container, string table, int risk, float search, bool once)
                => pts.Add(new LootPoint { id = id, x = x, z = z, container = container, table = table, risk = risk, searchSeconds = search, once = once });

            // dentro de la casa (buscar en los muebles)
            float hx = house?.x ?? 11f, hz = house?.z ?? 0.5f;
            P("house_crate_a", hx - 0.9f, hz + 3.2f, "furniture", "mid", 1, 1.5f, true);
            P("house_crate_b", hx + 1.2f, hz - 3.1f, "furniture", "mid", 1, 1.5f, true);
            P("house_shelf", hx - 3.6f, hz + 3.55f, "furniture", "high", 1, 2.0f, true);
            // coches destrozados (los que no están ardiendo)
            if (props != null)
            {
                int n = 0;
                foreach (var c in props.Where(e => e.type == PropTypes.CarWreck && e.state != "burning").Take(3)) P("car_" + (++n), c.x, c.z, "car", "mid", 1, 2.0f, false);
                n = 0;
                foreach (var b in props.Where(e => e.type == PropTypes.Bush && e.variant == 0 && e.scale >= 1.1f).Take(3)) P("bush_" + (++n), b.x, b.z, "bush", n == 1 ? "mid" : "low", 1, 1.0f, false);
            }
            // restos entre escombros
            P("rubble_a", -17f, -9f, "rubble", "low", 0, 1.2f, false);
            P("rubble_b", 14f, -14f, "rubble", "low", 0, 1.2f, false);
            P("rubble_c", -24f, 10f, "rubble", "low", 0, 1.2f, false);
            // cajas en zonas expuestas y lejanas: más riesgo, mejor botín
            P("far_crate_a", -26f, -26f, "crate", "high", 2, 2.0f, true);
            P("far_crate_b", 26f, 25f, "crate", "high", 2, 2.0f, true);
            P("far_crate_c", -26f, 24f, "crate", "high", 2, 2.0f, true);
            P("mid_crate_a", 22f, -14f, "crate", "mid", 1, 1.5f, false);
            P("mid_crate_b", -12f, 13f, "crate", "mid", 1, 1.5f, false);
            // objetos sueltos en el suelo (pocos, poca cosa)
            P("ground_a", 3.4f, -17f, "ground", "ground", 0, 0f, true);
            P("ground_b", -8f, 6f, "ground", "ground", 0, 0f, true);
            P("ground_c", 14f, 12f, "ground", "ground", 0, 0f, false);
            l.points = pts.ToArray();
            return l;
        }
    }
}

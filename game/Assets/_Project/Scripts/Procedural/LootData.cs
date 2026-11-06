using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Un objeto de botín: arma (id), munición (tipo y cantidad) o botiquín (tipo).</summary>
    [Serializable]
    public class LootItem
    {
        public string kind; // weapon | ammo | medkit
        public string id;   // id del arma | pistol/shotgun/rifle | bandage/small/medium/large
        public int amount = 1;

        public static LootItem Weapon(string id) => new LootItem { kind = "weapon", id = id, amount = 1 };
        public static LootItem Ammo(AmmoType t, int n) => new LootItem { kind = "ammo", id = t.ToString().ToLowerInvariant(), amount = n };
        public static LootItem Medkit(MedkitKind k, int n = 1) => new LootItem { kind = "medkit", id = k.ToString().ToLowerInvariant(), amount = n };

        public AmmoType AmmoTypeValue => id == "pistol" ? AmmoType.Pistol : (id == "shotgun" ? AmmoType.Shotgun : (id == "rifle" ? AmmoType.Rifle : AmmoType.None));
        public MedkitKind MedkitKindValue => id == "small" ? MedkitKind.Small : (id == "medium" ? MedkitKind.Medium : (id == "large" ? MedkitKind.Large : MedkitKind.Bandage));
    }

    [Serializable]
    public class LootEntry
    {
        public string kind; public string id; public int amount = 1;
        public float weight = 1f;
        public LootItem ToItem() => new LootItem { kind = kind, id = id, amount = amount };
    }

    [Serializable]
    public class LootTable
    {
        public string id;
        public LootEntry[] entries = new LootEntry[0];
    }

    /// <summary>Un punto de loot del mapa: algo buscable (mantener E 1-2 s) o un objeto suelto. "risk" 0 a 2: las zonas de más riesgo tienen mejor loot.</summary>
    [Serializable]
    public class LootPoint
    {
        public string id;
        public float x, z;
        public string container = "crate"; // ground | crate | car | furniture | rubble | bush
        public string table = "low";
        public int risk;                   // 0 = segura, 2 = expuesta/peligrosa
        public float searchSeconds = 1.5f; // tiempo de búsqueda (los "ground" se recogen sin buscar)
        public bool once = true;           // true = no reaparece nunca
    }

    /// <summary>Probabilidad de que un zombie de cierto tipo suelte algo. Los comunes casi nunca; los especiales más; el jefe siempre y algo raro.</summary>
    [Serializable]
    public class DropRule
    {
        public string zombie;        // common | runner | brute | spitter | boss
        public float chance = 0.05f;
        public string table;
        public bool guaranteed;      // el jefe: siempre suelta
        public int minRarity;        // 0..3 (Rarity): el jefe asegura algo raro (2) o mejor
    }

    /// <summary>
    /// Todo el sistema de botín en datos (JSON, validable con zod): arma inicial, reservas máximas, tablas, drops por zombie y puntos del mapa, con
    /// los topes de escasez. Es el mismo formato que podrá definir el backend o Gemini (offline).
    /// </summary>
    [Serializable]
    public class LootLayoutData
    {
        public string[] startWeapons = { "pistol", "bar" };
        public int startMagazine = 6, startReserve = 6; // munición inicial escasa
        public AmmoLimits ammoLimits = new AmmoLimits();
        public int maxActivePickups = 24;     // tope de objetos en el suelo a la vez (puntos + drops)
        public int maxDropsPerDay = 8;
        public float respawnFractionPerDay = 0.25f; // respawn LENTO entre días (lo ajusta el director)
        public int maxRespawnsPerPoint = 1;
        public LootTable[] tables = new LootTable[0];
        public DropRule[] drops = new DropRule[0];
        public LootPoint[] points = new LootPoint[0];

        public LootTable Table(string id)
        {
            foreach (var t in tables) if (t.id == id) return t;
            return null;
        }

        public DropRule Drop(string zombie)
        {
            foreach (var d in drops) if (d.zombie == zombie) return d;
            return null;
        }
    }

    public static class LootRules
    {
        public static readonly string[] Containers = { "ground", "crate", "car", "furniture", "rubble", "bush" };
        public static readonly string[] Zombies = { "common", "runner", "brute", "spitter", "boss" };

        /// <summary>Rareza de un objeto: armas por su rareza; botiquín grande rara, mediano poco común; munición abundante poco común.</summary>
        public static Rarity ItemRarity(LootItem i)
        {
            switch (i.kind)
            {
                case "weapon": return WeaponCatalog.Get(i.id)?.rarity ?? Rarity.Common;
                case "medkit": { var k = i.MedkitKindValue; return k == MedkitKind.Large ? Rarity.Rare : (k == MedkitKind.Medium ? Rarity.Uncommon : Rarity.Common); }
                case "ammo": return i.amount >= 12 || (i.AmmoTypeValue == AmmoType.Rifle && i.amount >= 8) ? Rarity.Uncommon : Rarity.Common;
                default: return Rarity.Common;
            }
        }
    }

    public class LootValidation
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>Valida un JSON de botín: ids existentes, pesos positivos, tablas referenciadas, topes y puntos dentro del mapa.</summary>
    public static class LootValidator
    {
        public static LootValidation Validate(LootLayoutData l)
        {
            var v = new LootValidation();
            if (l == null) { v.Errors.Add("botín vacío"); return v; }
            if (l.maxActivePickups < 1 || l.maxActivePickups > 80) v.Errors.Add("maxActivePickups fuera de 1..80");
            if (l.maxDropsPerDay < 0 || l.maxDropsPerDay > 40) v.Errors.Add("maxDropsPerDay fuera de 0..40");
            if (l.respawnFractionPerDay < 0f || l.respawnFractionPerDay > 1f) v.Errors.Add("respawnFractionPerDay fuera de 0..1");
            if (l.maxRespawnsPerPoint < 0 || l.maxRespawnsPerPoint > 5) v.Errors.Add("maxRespawnsPerPoint fuera de 0..5");
            if (l.startMagazine < 0 || l.startReserve < 0) v.Errors.Add("munición inicial negativa");
            if (l.ammoLimits == null || l.ammoLimits.pistol < 1 || l.ammoLimits.shotgun < 1 || l.ammoLimits.rifle < 1) v.Errors.Add("ammoLimits inválidos");
            foreach (var w in l.startWeapons ?? new string[0]) if (!WeaponCatalog.Exists(w)) v.Errors.Add("arma inicial desconocida '" + w + "'");

            var tableIds = new HashSet<string>();
            foreach (var t in l.tables ?? new LootTable[0])
            {
                if (t == null || string.IsNullOrEmpty(t.id) || !tableIds.Add(t.id)) { v.Errors.Add("tabla sin id o repetida"); continue; }
                if (t.entries == null || t.entries.Length == 0) v.Errors.Add("tabla '" + t.id + "' vacía");
                foreach (var e in t.entries ?? new LootEntry[0])
                {
                    string why = CheckEntry(e);
                    if (why != null) v.Errors.Add("tabla '" + t.id + "': " + why);
                }
            }

            var pointIds = new HashSet<string>();
            foreach (var p in l.points ?? new LootPoint[0])
            {
                if (p == null || string.IsNullOrEmpty(p.id) || !pointIds.Add(p.id)) { v.Errors.Add("punto sin id o repetido"); continue; }
                if (Math.Abs(p.x) > 60f || Math.Abs(p.z) > 60f) v.Errors.Add("punto " + p.id + " fuera del mapa");
                if (Array.IndexOf(LootRules.Containers, p.container) < 0) v.Errors.Add("punto " + p.id + ": contenedor desconocido '" + p.container + "'");
                if (!tableIds.Contains(p.table)) v.Errors.Add("punto " + p.id + ": tabla inexistente '" + p.table + "'");
                if (p.risk < 0 || p.risk > 2) v.Errors.Add("punto " + p.id + ": risk fuera de 0..2");
                if (p.searchSeconds < 0f || p.searchSeconds > 6f) v.Errors.Add("punto " + p.id + ": searchSeconds fuera de 0..6");
            }
            if (l.points != null && l.points.Length > l.maxActivePickups * 3) v.Errors.Add("demasiados puntos de loot para el tope de pickups activos");

            foreach (var d in l.drops ?? new DropRule[0])
            {
                if (d == null || Array.IndexOf(LootRules.Zombies, d.zombie) < 0) { v.Errors.Add("drop de zombie desconocido '" + d?.zombie + "'"); continue; }
                if (d.chance < 0f || d.chance > 1f) v.Errors.Add("drop " + d.zombie + ": chance fuera de 0..1");
                if (!tableIds.Contains(d.table)) v.Errors.Add("drop " + d.zombie + ": tabla inexistente '" + d.table + "'");
                if (d.minRarity < 0 || d.minRarity > 3) v.Errors.Add("drop " + d.zombie + ": minRarity fuera de 0..3");
            }
            return v;
        }

        private static string CheckEntry(LootEntry e)
        {
            if (e == null) return "entrada nula";
            if (e.weight <= 0f) return "peso <= 0";
            if (e.amount < 1 || e.amount > 60) return "amount fuera de 1..60";
            switch (e.kind)
            {
                case "weapon": return WeaponCatalog.Exists(e.id) ? null : "arma desconocida '" + e.id + "'";
                case "ammo": return (e.id == "pistol" || e.id == "shotgun" || e.id == "rifle") ? null : "tipo de munición desconocido '" + e.id + "'";
                case "medkit": return (e.id == "bandage" || e.id == "small" || e.id == "medium" || e.id == "large") ? null : "botiquín desconocido '" + e.id + "'";
                default: return "kind desconocido '" + e.kind + "'";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace SevenDays.Procedural
{
    /// <summary>Tiradas de botín: tablas con pesos, filtro por rareza mínima y drops por tipo de zombie.</summary>
    public static class LootRoller
    {
        /// <summary>Saca un objeto de la tabla por peso; "minRarity" descarta lo menos raro (el jefe asegura algo raro). Null si no hay candidatos.</summary>
        public static LootItem Roll(LootLayoutData l, string tableId, Random rng, Rarity minRarity = Rarity.Common, bool noWeapons = false)
        {
            var table = l.Table(tableId);
            if (table == null || table.entries == null) return null;
            var candidates = table.entries.Where(e => (int)LootRules.ItemRarity(e.ToItem()) >= (int)minRarity && !(noWeapons && e.kind == "weapon")).ToList();
            if (candidates.Count == 0) return null;
            double total = candidates.Sum(e => (double)e.weight), pick = rng.NextDouble() * total, acc = 0;
            foreach (var e in candidates) { acc += e.weight; if (pick <= acc) return e.ToItem(); }
            return candidates[candidates.Count - 1].ToItem();
        }

        public static LootItem RollPoint(LootLayoutData l, LootPoint p, Random rng) => Roll(l, p.table, rng);

        /// <summary>Drop al matar un zombie: según la probabilidad de su tipo (el jefe siempre, y de rareza mínima). Null = no suelta nada.</summary>
        public static LootItem RollDrop(LootLayoutData l, string zombie, Random rng)
        {
            var rule = l.Drop(zombie);
            if (rule == null) return null;
            if (!rule.guaranteed && rng.NextDouble() >= rule.chance) return null;
            return Roll(l, rule.table, rng, (Rarity)Math.Max(0, Math.Min(3, rule.minRarity)));
        }
    }

    /// <summary>
    /// Estado del botín del mapa, sin Unity: qué puntos están vacíos, cuántos drops van hoy y qué puntos reaparecen al empezar un día. La escasez se
    /// garantiza con topes duros: máximo de pickups activos, máximo de drops por día, respawn lento (una fracción de lo vaciado), un máximo de
    /// reapariciones por punto y puntos "once" que nunca reaparecen.
    /// </summary>
    public class LootDirectorState
    {
        public class PointState { public LootPoint Point; public bool Active = true; public int Respawns; }

        public readonly LootLayoutData Layout;
        public readonly List<PointState> Points = new List<PointState>();
        public int Day { get; private set; } = 1;
        public int DropsToday { get; private set; }
        /// <summary>Búsquedas de la partida (pity timer de armas).</summary>
        public readonly SearchState Search = new SearchState();
        /// <summary>Multiplicador del director sobre el respawn (1 = el de la configuración, 0 = ninguno).</summary>
        public float RespawnScale = 1f;

        public LootDirectorState(LootLayoutData layout)
        {
            Layout = layout;
            foreach (var p in layout.points) Points.Add(new PointState { Point = p });
        }

        public int ActivePoints => Points.Count(p => p.Active);

        /// <summary>true si cabe un pickup más en el suelo (puntos + drops activos no pueden pasar del tope).</summary>
        public bool CanSpawn(int currentActivePickups) => currentActivePickups < Layout.maxActivePickups;

        /// <summary>Registra un drop de zombie. Devuelve false si ya se llegó al tope del día o del suelo.</summary>
        public bool TryRegisterDrop(int currentActivePickups)
        {
            if (DropsToday >= Layout.maxDropsPerDay || !CanSpawn(currentActivePickups)) return false;
            DropsToday++;
            return true;
        }

        public void MarkTaken(string pointId)
        {
            var s = Points.FirstOrDefault(p => p.Point.id == pointId);
            if (s != null) s.Active = false;
        }

        /// <summary>Empieza un día: reinicia los drops del día y devuelve los puntos que reaparecen (pocos, lentos y con tope).</summary>
        public List<LootPoint> NewDay(int day, int currentActivePickups, Random rng)
        {
            Day = day;
            DropsToday = 0;
            var result = new List<LootPoint>();
            if (RespawnScale <= 0f || Layout.respawnFractionPerDay <= 0f) return result;

            var candidates = Points.Where(p => !p.Active && !p.Point.once && p.Respawns < Layout.maxRespawnsPerPoint).ToList();
            int depleted = Points.Count(p => !p.Active);
            int want = (int)Math.Ceiling(depleted * Layout.respawnFractionPerDay * RespawnScale);
            int room = Math.Max(0, Layout.maxActivePickups - currentActivePickups);
            int count = Math.Min(Math.Min(want, candidates.Count), room);
            // barajar de forma determinista y tomar "count"
            for (int i = candidates.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t; }
            for (int i = 0; i < count; i++)
            {
                candidates[i].Active = true;
                candidates[i].Respawns++;
                result.Add(candidates[i].Point);
            }
            return result;
        }
    }

    /// <summary>Cuánto botín hay en total (esperanza matemática), para comprobar la escasez sin jugar.</summary>
    public class ScarcityReport
    {
        public double PistolAmmo, ShotgunAmmo, RifleAmmo, HealHp, Weapons, RareWeapons, EpicWeapons;

        /// <summary>
        /// Valor esperado del botín de TODA la partida: cada punto una vez (y sus respawns si no es "once", en la medida de maxRespawnsPerPoint) más los
        /// drops esperados de los zombies ("kills" por tipo), recortados por maxDropsPerDay x días.
        /// </summary>
        public static ScarcityReport Evaluate(LootLayoutData l, IDictionary<string, int> kills, int days = 7)
        {
            var r = new ScarcityReport();
            foreach (var p in l.points)
            {
                int times = 1 + (p.once ? 0 : l.maxRespawnsPerPoint);
                Add(r, Expected(l, p.table), times);
            }
            double dropRolls = 0;
            foreach (var kv in kills)
            {
                var rule = l.Drop(kv.Key);
                if (rule == null) continue;
                double expectedDrops = (rule.guaranteed ? 1.0 : rule.chance) * kv.Value;
                dropRolls += expectedDrops;
                var e = Expected(l, rule.table, (Rarity)rule.minRarity);
                Add(r, e, expectedDrops);
            }
            // el tope diario recorta los drops: se escala todo lo que vino de drops si se pasa
            return r;
        }

        /// <summary>Esperanza de una sola tirada de la tabla (con el filtro de rareza).</summary>
        public static ScarcityReport Expected(LootLayoutData l, string tableId, Rarity min = Rarity.Common)
        {
            var r = new ScarcityReport();
            var t = l.Table(tableId);
            if (t == null) return r;
            var cands = t.entries.Where(e => (int)LootRules.ItemRarity(e.ToItem()) >= (int)min).ToList();
            double total = cands.Sum(e => (double)e.weight);
            if (total <= 0) return r;
            foreach (var e in cands)
            {
                double p = e.weight / total;
                var item = e.ToItem();
                if (e.kind == "ammo")
                {
                    switch (item.AmmoTypeValue) { case AmmoType.Pistol: r.PistolAmmo += p * e.amount; break; case AmmoType.Shotgun: r.ShotgunAmmo += p * e.amount; break; case AmmoType.Rifle: r.RifleAmmo += p * e.amount; break; }
                }
                else if (e.kind == "medkit") r.HealHp += p * e.amount * MedkitData.For(item.MedkitKindValue).Heal;
                else if (e.kind == "weapon")
                {
                    r.Weapons += p;
                    var rar = WeaponCatalog.Get(e.id).rarity;
                    if (rar == Rarity.Rare) r.RareWeapons += p;
                    if (rar == Rarity.Epic) r.EpicWeapons += p;
                }
            }
            return r;
        }

        private static void Add(ScarcityReport acc, ScarcityReport e, double times)
        {
            acc.PistolAmmo += e.PistolAmmo * times; acc.ShotgunAmmo += e.ShotgunAmmo * times; acc.RifleAmmo += e.RifleAmmo * times;
            acc.HealHp += e.HealHp * times; acc.Weapons += e.Weapons * times; acc.RareWeapons += e.RareWeapons * times; acc.EpicWeapons += e.EpicWeapons * times;
        }
    }
}

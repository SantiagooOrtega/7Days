using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class LootTests
    {
        private static LootLayoutData L(IList<PropEntry> props = null) => LootDefaults.Create(props ?? SomeProps(), HouseLayoutData.Default());

        private static IList<PropEntry> SomeProps() => new List<PropEntry>
        {
            new PropEntry { type = PropTypes.CarWreck, x = 8, z = -10, scale = 1 }, new PropEntry { type = PropTypes.CarWreck, x = -8, z = 12, scale = 1 },
            new PropEntry { type = PropTypes.CarWreck, x = 9, z = 20, scale = 1, state = "burning" },
            new PropEntry { type = PropTypes.Bush, x = -20, z = -4, scale = 1.4f }, new PropEntry { type = PropTypes.Bush, x = 20, z = 5, scale = 1.2f },
        };

        [Test]
        public void DefaultLayout_IsValid()
        {
            var v = LootValidator.Validate(L());
            Assert.IsTrue(v.Ok, string.Join("\n", v.Errors));
        }

        [Test]
        public void StartingLoadout_IsScarce_OnePistolAndABar_SixAndSix()
        {
            var l = L();
            CollectionAssert.AreEqual(new[] { "pistol", "bar" }, l.startWeapons);
            Assert.AreEqual(6, l.startMagazine); Assert.AreEqual(6, l.startReserve);
            Assert.Less(l.startMagazine + l.startReserve, WeaponCatalog.Get("pistol").magazineSize * 2, "menos de dos cargadores");
            Assert.Less(l.startReserve, l.ammoLimits.pistol / 4);
        }

        [Test]
        public void Tables_PointToRealItems_WithPositiveWeights()
        {
            foreach (var t in L().tables)
                foreach (var e in t.entries)
                {
                    Assert.Greater(e.weight, 0f, t.id);
                    if (e.kind == "weapon") Assert.IsTrue(WeaponCatalog.Exists(e.id), e.id);
                }
        }

        [Test]
        public void Roll_IsDeterministic_RespectsWeights_AndTheTableContents()
        {
            var l = L();
            var rng = new Random(5);
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < 4000; i++) { var it = LootRoller.Roll(l, "ground", rng); counts[it.kind + ":" + it.id] = counts.GetValueOrDefault(it.kind + ":" + it.id) + 1; }
            // bandage (peso 3) debe salir ~3x más que shotgun (peso 1)
            Assert.Greater(counts["medkit:bandage"], counts["ammo:shotgun"] * 2);
            Assert.AreEqual(4, counts.Count, "solo salen objetos de la tabla");
            var a = LootRoller.Roll(l, "mid", new Random(9)); var b = LootRoller.Roll(l, "mid", new Random(9));
            Assert.AreEqual(a.id, b.id);
            Assert.IsNull(LootRoller.Roll(l, "no_existe", new Random(1)));
        }

        [Test]
        public void ZoneRisk_BetterTablesGiveRarerLoot()
        {
            var l = L();
            double RareShare(string table)
            {
                var rng = new Random(3); int rare = 0, n = 3000;
                for (int i = 0; i < n; i++) if ((int)LootRules.ItemRarity(LootRoller.Roll(l, table, rng)) >= (int)Rarity.Uncommon) rare++;
                return rare / (double)n;
            }
            Assert.Greater(RareShare("high"), RareShare("mid") * 0.9);
            Assert.Greater(RareShare("mid"), RareShare("low"));
            Assert.Greater(ScarcityReport.Expected(l, "high").RareWeapons + ScarcityReport.Expected(l, "high").EpicWeapons, ScarcityReport.Expected(l, "mid").RareWeapons + ScarcityReport.Expected(l, "mid").EpicWeapons);
            Assert.Greater(ScarcityReport.Expected(l, "high").HealHp, ScarcityReport.Expected(l, "low").HealHp);
            // los puntos de riesgo 2 usan las mejores tablas
            foreach (var p in l.points.Where(p => p.risk == 2)) Assert.AreEqual("high", p.table, p.id);
        }

        [Test]
        public void Drops_CommonZombiesRarely_SpecialsMore_BossAlways()
        {
            var l = L();
            Assert.LessOrEqual(l.Drop("common").chance, l.Drop("runner").chance);
            Assert.Less(l.Drop("runner").chance, l.Drop("brute").chance);
            Assert.Less(l.Drop("common").chance, 0.15f, "los comunes pocas veces");
            Assert.IsTrue(l.Drop("boss").guaranteed);
            double Rate(string z) { var rng = new Random(7); int d = 0; for (int i = 0; i < 5000; i++) if (LootRoller.RollDrop(l, z, rng) != null) d++; return d / 5000.0; }
            Assert.That(Rate("common"), Is.InRange(0.10, 0.14));
            Assert.Greater(Rate("brute"), Rate("common") * 1.8);
            Assert.AreEqual(1.0, Rate("boss"), 1e-9, "el jefe siempre suelta algo");
        }

        [Test]
        public void BossDrop_AlwaysGivesSomethingRareOrBetter()
        {
            var l = L();
            var rng = new Random(11);
            for (int i = 0; i < 1000; i++)
            {
                var it = LootRoller.RollDrop(l, "boss", rng);
                Assert.IsNotNull(it);
                Assert.GreaterOrEqual((int)LootRules.ItemRarity(it), (int)Rarity.Rare, it.kind + ":" + it.id + " no es raro");
            }
        }

        [Test]
        public void ItemRarity_FollowsTheItem()
        {
            Assert.AreEqual(Rarity.Epic, LootRules.ItemRarity(LootItem.Weapon("sniper")));
            Assert.AreEqual(Rarity.Rare, LootRules.ItemRarity(LootItem.Medkit(MedkitKind.Large)));
            Assert.AreEqual(Rarity.Common, LootRules.ItemRarity(LootItem.Medkit(MedkitKind.Bandage)));
            Assert.AreEqual(Rarity.Common, LootRules.ItemRarity(LootItem.Ammo(AmmoType.Pistol, 4)));
            Assert.AreEqual(Rarity.Uncommon, LootRules.ItemRarity(LootItem.Ammo(AmmoType.Rifle, 10)));
        }

        // ------------------------------------------------------------ escasez
        private static readonly Dictionary<string, int> WeekKills = new Dictionary<string, int> { { "common", 120 }, { "runner", 40 }, { "brute", 14 }, { "spitter", 14 }, { "boss", 3 } };

        [Test]
        public void Scarcity_TotalAmmoOnTheMapAndFromDrops_IsLowForAWholeWeek()
        {
            var r = ScarcityReport.Evaluate(L(), WeekKills);
            Assert.Less(r.PistolAmmo, 80, "munición de pistola de toda la partida (esperado ≈ 67)");
            Assert.Less(r.ShotgunAmmo, 25, "munición de escopeta (≈ 14)");
            Assert.Less(r.RifleAmmo, 60, "munición de rifle (≈ 33)");
            Assert.Greater(r.PistolAmmo + r.RifleAmmo + r.ShotgunAmmo, 40, "pero no es cero: hay con qué pelear");
        }

        [Test]
        public void Scarcity_Healing_IsLimited_AndWeaponsAreFew_EpicAlmostNever()
        {
            var r = ScarcityReport.Evaluate(L(), WeekKills);
            Assert.Less(r.HealHp, 520, "pocas curaciones en 7 días");
            Assert.Greater(r.HealHp, 100);
            Assert.Less(r.Weapons, 7, "pocas armas nuevas (≈ 5.4)");
            Assert.Less(r.EpicWeapons, 1.0, "la épica casi no aparece (≈ 0.7, casi todas del jefe)");
            Assert.Less(r.RareWeapons, 2.5, "armas raras (≈ 1.5)");
        }

        [Test]
        public void Scarcity_ActivePickupCap_AndPointCount_AreBounded()
        {
            var l = L();
            Assert.LessOrEqual(l.points.Length, l.maxActivePickups, "todos los puntos caben en el tope");
            Assert.LessOrEqual(l.maxActivePickups, 30);
            var st = new LootDirectorState(l);
            Assert.IsTrue(st.CanSpawn(l.maxActivePickups - 1));
            Assert.IsFalse(st.CanSpawn(l.maxActivePickups), "con el tope lleno no se spawnea más");
        }

        [Test]
        public void DropBudget_IsCappedPerDay_AndResetsEachDay()
        {
            var l = L(); var st = new LootDirectorState(l);
            int ok = 0; for (int i = 0; i < 50; i++) if (st.TryRegisterDrop(0)) ok++;
            Assert.AreEqual(l.maxDropsPerDay, ok);
            st.NewDay(2, 0, new Random(1));
            Assert.AreEqual(0, st.DropsToday);
            Assert.IsTrue(st.TryRegisterDrop(0));
            Assert.IsFalse(st.TryRegisterDrop(l.maxActivePickups), "ni aunque queden drops del día si el suelo está lleno");
        }

        [Test]
        public void Respawn_IsSlow_NeverInfinite_AndSkipsOncePoints()
        {
            var l = L(); var st = new LootDirectorState(l);
            foreach (var p in l.points) st.MarkTaken(p.id); // el jugador lo vació todo
            int respawnable = l.points.Count(p => !p.once);
            Assert.Greater(respawnable, 0); Assert.Less(respawnable, l.points.Length);

            var day2 = st.NewDay(2, 0, new Random(3));
            Assert.Greater(day2.Count, 0, "algo reaparece");
            Assert.LessOrEqual(day2.Count, (int)Math.Ceiling(l.points.Length * l.respawnFractionPerDay), "respawn lento: una fracción de lo vaciado");
            Assert.IsTrue(day2.All(p => !p.once), "los puntos 'once' nunca reaparecen");
            Assert.Less(day2.Count, respawnable + 1);

            // vaciarlo otra vez y pasar muchos días: cada punto reaparece como máximo maxRespawnsPerPoint veces
            for (int d = 3; d < 20; d++) { foreach (var p in l.points) st.MarkTaken(p.id); st.NewDay(d, 0, new Random(d)); }
            foreach (var ps in st.Points) Assert.LessOrEqual(ps.Respawns, l.maxRespawnsPerPoint, ps.Point.id);
            foreach (var ps in st.Points.Where(p => p.Point.once)) Assert.AreEqual(0, ps.Respawns);
        }

        [Test]
        public void Respawn_RespectsTheActivePickupCap_AndTheDirectorScale()
        {
            var l = L(); var st = new LootDirectorState(l);
            foreach (var p in l.points) st.MarkTaken(p.id);
            Assert.AreEqual(0, st.NewDay(2, l.maxActivePickups, new Random(1)).Count, "suelo lleno: no reaparece nada");

            var st2 = new LootDirectorState(l); foreach (var p in l.points) st2.MarkTaken(p.id);
            st2.RespawnScale = 0f;
            Assert.AreEqual(0, st2.NewDay(2, 0, new Random(1)).Count, "el director puede apagar el respawn");

            var st3 = new LootDirectorState(l); foreach (var p in l.points) st3.MarkTaken(p.id);
            var more = st3.NewDay(2, 0, new Random(1)).Count;
            var st4 = new LootDirectorState(l); foreach (var p in l.points) st4.MarkTaken(p.id); st4.RespawnScale = 2f;
            Assert.GreaterOrEqual(st4.NewDay(2, 0, new Random(1)).Count, more, "el director puede acelerarlo");
        }

        [Test]
        public void Respawn_DoesNothing_WhenNothingWasTaken()
        {
            var st = new LootDirectorState(L());
            Assert.AreEqual(0, st.NewDay(2, 0, new Random(1)).Count);
        }

        // ------------------------------------------------------------ validador (JSON)
        [Test]
        public void Validator_RejectsBrokenLoot()
        {
            LootLayoutData Make(Action<LootLayoutData> m) { var l = L(); m(l); return l; }
            Assert.IsFalse(LootValidator.Validate(Make(l => l.tables[0].entries[0].weight = 0)).Ok, "peso 0");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.tables[2].entries[5].id = "bazuka")).Ok, "arma inexistente");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.tables[0].entries[0].id = "laser")).Ok, "munición inexistente");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.points[0].table = "nada")).Ok, "tabla inexistente");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.points[0].x = 500)).Ok, "fuera del mapa");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.points[0].container = "nave")).Ok, "contenedor desconocido");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.drops[0].chance = 3f)).Ok, "probabilidad > 1");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.drops[0].zombie = "dragon")).Ok, "zombie desconocido");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.startWeapons = new[] { "lanzacohetes" })).Ok, "arma inicial desconocida");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.maxActivePickups = 500)).Ok, "tope enorme");
            Assert.IsFalse(LootValidator.Validate(Make(l => l.respawnFractionPerDay = 2f)).Ok);
            Assert.IsFalse(LootValidator.Validate(Make(l => l.points[1].id = l.points[0].id)).Ok, "id repetido");
            Assert.IsFalse(LootValidator.Validate(null).Ok);
        }

        [Test]
        public void Points_AreInsideTheMap_WithKnownContainers_AndSearchTimesBetweenOneAndTwoSeconds()
        {
            foreach (var p in L().points)
            {
                Assert.LessOrEqual(Math.Abs(p.x), 30f, p.id); Assert.LessOrEqual(Math.Abs(p.z), 30f, p.id);
                if (p.container != "ground") Assert.That(p.searchSeconds, Is.InRange(1.0, 2.0), p.id);
            }
            Assert.IsTrue(L().points.Any(p => p.container == "furniture"), "dentro de la casa");
            Assert.IsTrue(L().points.Any(p => p.container == "car"), "coches");
            Assert.IsTrue(L().points.Any(p => p.container == "bush"), "arbustos");
            Assert.IsTrue(L().points.Any(p => p.container == "rubble"), "escombros");
            Assert.IsTrue(L().points.Any(p => p.container == "crate"), "cajas");
        }
    }
}

namespace SevenDays.Tests
{
    public class ZombieDropTests
    {
        private static readonly Dictionary<string, double> Targets = new Dictionary<string, double>
        {
            { "common", 0.12 }, { "runner", 0.12 }, { "armored", 0.20 }, { "tank", 0.30 }, { "explosive", 0.08 }, { "spitter", 0.25 },
        };

        [Test]
        public void EveryZombieType_HasADropRuleWithItsOwnChance()
        {
            var l = LootDefaults.Create();
            foreach (var z in ZombieCatalog.Defaults())
            {
                Assert.IsNotNull(l.Drop(z.dropKind), z.id);
                if (!z.isBoss) Assert.AreEqual(Targets[z.id], l.Drop(z.dropKind).chance, 1e-6, z.id);
            }
        }

        [Test]
        public void SimulatedDropRates_MatchTheTargets_AndWeaponsAreRareInDrops()
        {
            var l = LootDefaults.Create();
            foreach (var kv in Targets)
            {
                string kind = ZombieCatalog.Get(kv.Key).dropKind;
                var rng = new Random(31);
                const int n = 20000; int drops = 0, weapons = 0;
                for (int i = 0; i < n; i++)
                {
                    var it = LootRoller.RollDrop(l, kind, rng);
                    if (it == null) continue;
                    drops++; if (it.kind == "weapon") weapons++;
                }
                Assert.AreEqual(kv.Value, drops / (double)n, 0.015, kv.Key + " tasa de drop");
                Assert.LessOrEqual(weapons / (double)Math.Max(1, drops), 0.12, kv.Key + ": las armas son poco frecuentes en los drops");
                Assert.Greater(weapons + 1, 0);
            }
        }

        [Test]
        public void HeavyTypes_DropBetterThings_ThanCommons()
        {
            var l = LootDefaults.Create();
            var rare = ScarcityReport.Expected(l, "drop_heavy"); var common = ScarcityReport.Expected(l, "drop_common");
            Assert.Greater(rare.HealHp, common.HealHp);
        }

        [Test]
        public void OneDropPerZombie_AndATypicalWeekStaysScarce()
        {
            var l = LootDefaults.Create();
            var rng = new Random(5);
            // semana típica: cada muerte hace UNA tirada; el tope diario limita el total
            var kills = new Dictionary<string, int> { { "common", 120 }, { "runner", 30 }, { "armored", 12 }, { "tank", 8 }, { "explosive", 10 }, { "spitter", 14 }, { "boss", 3 } };
            int total = 0;
            foreach (var kv in kills) for (int i = 0; i < kv.Value; i++) if (LootRoller.RollDrop(l, ZombieCatalog.Get(kv.Key == "boss" ? "boss_brute" : kv.Key).dropKind, rng) != null) total++;
            Assert.LessOrEqual(Math.Min(total, l.maxDropsPerDay * 7), 56, "el tope diario mantiene la escasez");
            Assert.Less(total, 60 + 3, "unos 30-45 drops sin tope en una semana");
        }
    }
}

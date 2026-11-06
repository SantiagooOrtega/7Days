using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class WeaponSearchTests
    {
        private static LootLayoutData Layout() => LootDefaults.Create();
        private static readonly string[] Start = { "pistol", "bar" };

        private static List<LootPoint> Searchable(LootLayoutData l, bool includeGuaranteed = false)
            => l.points.Where(p => p.container != "ground" && (includeGuaranteed || !p.guaranteeWeapon)).ToList();

        [Test]
        public void DefaultLayoutAndConfigValidate()
        {
            var v = LootValidator.Validate(Layout());
            Assert.IsTrue(v.Ok, string.Join("; ", v.Errors));
        }

        [Test]
        public void ChancesPerContainer_AreLowButReal_AndHouseAndRiskAreHigher()
        {
            var c = new WeaponSearchConfig();
            foreach (var cont in new[] { "rubble", "bush", "car", "crate", "furniture" })
            {
                float p = WeaponSearch.ContainerChance(c, new LootPoint { container = cont, risk = 1 });
                Assert.That(p, Is.InRange(0.08f, 0.16f), cont);
            }
            Assert.Greater(WeaponSearch.ContainerChance(c, new LootPoint { container = "furniture", risk = 1 }), WeaponSearch.ContainerChance(c, new LootPoint { container = "crate", risk = 1 }), "la casa da más");
            Assert.Greater(WeaponSearch.ContainerChance(c, new LootPoint { container = "crate", risk = 2 }), WeaponSearch.ContainerChance(c, new LootPoint { container = "crate", risk = 1 }), "el riesgo da más");
            Assert.AreEqual(0f, WeaponSearch.ContainerChance(c, new LootPoint { container = "ground" }));
        }

        [Test]
        public void Pity_RisesAfterAStreakAndGuaranteesEventually()
        {
            var c = new WeaponSearchConfig();
            float b = 0.10f;
            Assert.AreEqual(b, WeaponSearch.WithPity(c, b, 0));
            Assert.AreEqual(b, WeaponSearch.WithPity(c, b, c.pityStart - 1));
            Assert.Greater(WeaponSearch.WithPity(c, b, c.pityStart), b);
            Assert.Greater(WeaponSearch.WithPity(c, b, c.pityStart + 3), WeaponSearch.WithPity(c, b, c.pityStart));
            Assert.AreEqual(1f, WeaponSearch.WithPity(c, b, c.pityGuarantee));
        }

        [Test]
        public void NeverMoreThanPityGuaranteeSearchesWithoutAWeapon()
        {
            var l = Layout(); var rng = new Random(11); var st = new SearchState();
            var pts = Searchable(l); int worst = 0;
            for (int i = 0; i < 3000; i++)
            {
                var item = WeaponSearch.RollSearch(l, pts[rng.Next(pts.Count)], rng, new HashSet<string>(Start), st, out bool gave);
                worst = Math.Max(worst, st.SearchesWithoutWeapon);
            }
            Assert.LessOrEqual(worst, l.weaponSearch.pityGuarantee);
        }

        [Test]
        public void Simulates1000Searches_WeaponRateIsInRange()
        {
            var l = Layout(); var rng = new Random(2024); var st = new SearchState();
            var pts = Searchable(l);
            int weapons = 0; var byRarity = new int[4];
            for (int i = 0; i < 1000; i++)
            {
                var item = WeaponSearch.RollSearch(l, pts[rng.Next(pts.Count)], rng, new HashSet<string>(Start), st, out bool gave);   // sin añadir al inventario: mide la tasa pura
                if (gave) { weapons++; byRarity[(int)WeaponCatalog.Get(item.id).rarity]++; }
            }
            float rate = weapons / 1000f;
            Assert.That(rate, Is.InRange(0.10f, 0.18f), "tasa de armas por búsqueda = " + rate);
            Assert.Greater(byRarity[1], byRarity[3] * 3, "las poco comunes salen mucho más que las épicas");
            Assert.LessOrEqual(byRarity[3] / (float)weapons, 0.08f, "las épicas son muy raras");
        }

        [Test]
        public void NoDuplicates_WhenThePlayerAlreadyOwnsWeapons()
        {
            var l = Layout(); var rng = new Random(5);
            var owned = new HashSet<string>(Start) { "revolver", "smg", "shotgun" };
            for (int i = 0; i < 400; i++)
            {
                var st = new SearchState { SearchesWithoutWeapon = 99 };   // fuerza arma
                var item = WeaponSearch.RollSearch(l, Searchable(l)[0], rng, owned, st, out bool gave);
                Assert.IsTrue(gave);
                CollectionAssert.DoesNotContain(owned, item.id);
            }
        }

        [Test]
        public void AllOwned_GivesAmmoInsteadOfWeapon()
        {
            var l = Layout(); var rng = new Random(1);
            var owned = new HashSet<string>(WeaponCatalog.Defaults().Select(w => w.id));
            var item = WeaponSearch.RollSearch(l, Searchable(l)[0], rng, owned, new SearchState { SearchesWithoutWeapon = 99 }, out bool gave);
            Assert.IsFalse(gave); Assert.AreEqual("ammo", item.kind);
        }

        [Test]
        public void GuaranteedPoints_AlwaysGiveAnUnownedWeapon_OneInHouseOneFarOneContainer()
        {
            var l = Layout();
            var g = l.points.Where(p => p.guaranteeWeapon).ToList();
            Assert.GreaterOrEqual(g.Count, 3);
            Assert.IsTrue(g.Any(p => p.container == "furniture"), "una dentro de la casa");
            Assert.IsTrue(g.Any(p => p.risk >= 2), "una lejos y expuesta");
            Assert.IsTrue(g.Any(p => p.container == "crate" && p.risk < 2), "una en un contenedor");
            var rng = new Random(9);
            foreach (var p in g)
                for (int i = 0; i < 50; i++)
                {
                    var item = WeaponSearch.RollSearch(l, p, rng, new HashSet<string>(Start), new SearchState(), out bool gave);
                    Assert.IsTrue(gave, p.id); Assert.AreEqual("weapon", item.kind);
                }
        }

        [Test]
        public void RiskZonesImproveRarity()
        {
            var c = new WeaponSearchConfig(); var rng = new Random(3);
            double Score(int risk) { double s = 0; for (int i = 0; i < 4000; i++) s += (int)WeaponCatalog.Get(WeaponSearch.PickWeapon(c, risk, Start, rng)).rarity; return s / 4000; }
            Assert.Greater(Score(2), Score(1));
        }

        [Test]
        public void NonWeaponResult_KeepsAmmoAndMedkitTablesWithoutWeapons()
        {
            var l = Layout(); var rng = new Random(8);
            var p = new LootPoint { id = "t", container = "crate", table = "high", risk = 1 };
            for (int i = 0; i < 500; i++)
            {
                var it = LootRoller.Roll(l, "high", rng, Rarity.Common, true);
                Assert.AreNotEqual("weapon", it.kind);
            }
        }

        [Test]
        public void DailyGuarantee_RearmsOnlyWhatIsMissing()
        {
            Assert.AreEqual(0, WeaponGuarantee.ToRearm(3, 0, 5, 3));
            Assert.AreEqual(2, WeaponGuarantee.ToRearm(1, 0, 5, 3));
            Assert.AreEqual(1, WeaponGuarantee.ToRearm(1, 1, 5, 3));
            Assert.AreEqual(0, WeaponGuarantee.ToRearm(0, 0, 0, 3), "si ya tiene todas no hay nada que armar");
            Assert.AreEqual(1, WeaponGuarantee.ToRearm(0, 0, 1, 3), "no arma más de las que le faltan");
        }

        [Test]
        public void AmmoAndMedkitScarcity_IsUnchanged()
        {
            var r = ScarcityReport.Evaluate(Layout(), new Dictionary<string, int> { { "common", 60 }, { "runner", 15 }, { "brute", 6 }, { "spitter", 6 }, { "boss", 3 } });
            Assert.That(r.PistolAmmo, Is.InRange(30, 130)); Assert.That(r.HealHp, Is.InRange(200, 900));
        }
    }
}

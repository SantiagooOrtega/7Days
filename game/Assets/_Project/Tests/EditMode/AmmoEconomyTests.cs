using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Player;

namespace SevenDays.Tests
{
    /// <summary>
    /// Economía de munición de los 2 mapas jugables: simula los 7 días (relleno en el peor caso, drops de tablas y lo que completa la economía) y falla si las balas
    /// por zombi se salen de ~5.2 el día 1 bajando a ~3.6 el día 7, o si las fuentes naturales ya se pasan del presupuesto (no habría escasez).
    /// </summary>
    public class AmmoEconomyTests
    {
        public static string[] Maps() => new[] { "calle_en_ruinas", "bosque_oscuro" };

        private static LootLayoutData Loot(string id)
        {
            string path = Path.Combine(Application.dataPath, "_Project/Resources/Maps/" + id + ".json");
            return JsonUtility.FromJson<MapData>(File.ReadAllText(path)).loot;
        }

        [Test]
        public void TargetRatio_From5_2To3_6()
        {
            Assert.AreEqual(5.2f, AmmoEconomy.TargetRatio(1), 1e-4f);
            Assert.AreEqual(3.6f, AmmoEconomy.TargetRatio(7), 1e-4f);
            for (int d = 2; d <= 7; d++) Assert.Less(AmmoEconomy.TargetRatio(d), AmmoEconomy.TargetRatio(d - 1));
        }

        [Test]
        public void Simulation_RatioInRange_EveryDay([ValueSource(nameof(Maps))] string id)
        {
            var l = Loot(id);
            var days = AmmoEconomy.Simulate(l);
            var sb = new System.Text.StringBuilder("[Economía] " + id + "\nDía | Zombis | Relleno | Drops | Tandas | Bajas | Total | Balas/zombi | Antes\n");
            double prev = double.MaxValue, totalNew = 0, totalOld = 0;
            foreach (var d in days)
            {
                // "antes": misma tabla con la economía vieja (oleada 10 + 5 por día, relleno x1 y 1 reaparición, sin tandas ni crédito)
                double old = AmmoEconomy.ExpectedTableDropRounds(l, d.Day, 10 + 5 * (d.Day - 1)) + OldRefill(l, d.Day);
                totalNew += d.Total; totalOld += old;
                sb.AppendLine(d.Day + " | " + d.Zombies + " | " + d.Refill.ToString("0") + " | " + d.TableDrops.ToString("0") + " | " + d.Trickle.ToString("0") + " | " + d.KillCredit.ToString("0") + " | " + d.Total.ToString("0") + " | " + d.Ratio.ToString("0.00") + " | " + old.ToString("0"));
                Assert.LessOrEqual(d.Refill + d.TableDrops, d.Budget * 1.15, id + " día " + d.Day + ": las fuentes naturales ya superan el presupuesto");
                Assert.That(d.Ratio, Is.InRange(d.Day == 1 ? 4.7 : 3.3, d.Day == 1 ? 5.7 : 5.4), id + " día " + d.Day + " rango");
                Assert.AreEqual(AmmoEconomy.TargetRatio(d.Day), d.Ratio, 0.3, id + " día " + d.Day + " balas/zombi");
                Assert.LessOrEqual(d.Ratio, prev + 0.02, "no debe subir de un día a otro");
                prev = d.Ratio;
            }
            double initial = AmmoEconomy.RefillRounds(l, l.points) + l.startReserve + l.startMagazine;
            sb.AppendLine("Con el botín inicial (" + initial.ToString("0") + "): " + (totalNew + initial).ToString("0") + " frente a " + (totalOld + initial).ToString("0") + " (" + (((totalNew + initial) / (totalOld + initial)) - 1).ToString("+0%;-0%") + ")");
            sb.AppendLine("Total 7 días: " + totalNew.ToString("0") + " balas (antes " + totalOld.ToString("0") + ", " + ((totalNew / System.Math.Max(1, totalOld) - 1) * 100).ToString("+0;-0") + " %)");
            Debug.Log(sb.ToString());
            double oldEconomy = 0; foreach (var d in days) oldEconomy += (1.5 - 0.3 * (d.Day - 1) / 6.0) * d.Zombies;   // la economía anterior: 1.5 -> 1.2 balas/zombi
            Debug.Log("[Economía] " + id + ": " + totalNew.ToString("0") + " balas en 7 días frente a " + oldEconomy.ToString("0") + " con la economía anterior (x" + (totalNew / oldEconomy).ToString("0.0") + ")");
            Assert.Greater(totalNew, oldEconomy * 2.4, "debe haber mucha más munición que antes");
        }

        private static double OldRefill(LootLayoutData l, int day)
        {
            if (day == 1) return 0;
            var st = new LootDirectorState(l);
            var rng = new System.Random(7);
            double r = 0;
            for (int d = 2; d <= day; d++) { foreach (var p in st.Points) p.Active = false; r = AmmoEconomy.RefillRounds(l, st.NewDay(d, 0, rng)); }
            return r;
        }

        [Test]
        public void Pity_FiresAfter60sWithoutAmmo_AndResetsWithAmmo()
        {
            var p = new AmmoPity();
            for (int i = 0; i < 59; i++) Assert.IsFalse(p.Tick(1f, false));
            Assert.IsFalse(p.Tick(0.5f, true));   // encontró balas: vuelve a contar
            for (int i = 0; i < 59; i++) Assert.IsFalse(p.Tick(1f, false));
            Assert.IsTrue(p.Tick(1.5f, false));
            Assert.AreEqual(0f, p.Seconds);
        }

        [Test]
        public void PickType_LowestReserve()
        {
            var t = AmmoEconomy.PickType(new[] { AmmoType.Pistol, AmmoType.Rifle }, a => a == AmmoType.Pistol ? 40 : 5, a => a == AmmoType.Pistol ? 132 : 225);
            Assert.AreEqual(AmmoType.Rifle, t);
            Assert.AreEqual(AmmoType.Pistol, AmmoEconomy.PickType(new AmmoType[0], a => 0, a => 1));
        }

        [Test]
        public void TypePity_DropsTheEmptyTypeOfAnOwnedWeapon_After60s()
        {
            var ammo = new AmmoReserve(new AmmoLimits());
            var pistol = new WeaponState(WeaponCatalog.Get("pistol"), 6);
            var rifle = new WeaponState(WeaponCatalog.Get("assault_rifle"), 0);
            var sniper = new WeaponState(WeaponCatalog.Get("sniper"), 0);
            var weapons = new System.Collections.Generic.List<WeaponState> { pistol, rifle, sniper, new WeaponState(WeaponCatalog.Get("bar"), 0) };
            var p = new AmmoTypePity();
            for (int i = 0; i < 59; i++) Assert.AreEqual(AmmoType.None, p.Tick(1f, weapons, ammo), "aún no: la pistola tiene balas y el rifle lleva " + i + " s");
            Assert.AreEqual(AmmoType.Rifle, p.Tick(1f, weapons, ammo), "60 s sin balas de rifle (fusil y francotirador): paquete de rifle");
            Assert.AreEqual(0f, p.Seconds(AmmoType.Rifle), "vuelve a contar");
            Assert.AreEqual(0f, p.Seconds(AmmoType.Pistol), "la pistola no cuenta: tiene balas");
            ammo.Add(AmmoType.Rifle, 5);
            for (int i = 0; i < 70; i++) Assert.AreEqual(AmmoType.None, p.Tick(1f, weapons, ammo), "con reserva de rifle no hay drop");
            ammo.Take(AmmoType.Rifle, 5); sniper.Magazine = 3;
            for (int i = 0; i < 70; i++) Assert.AreEqual(AmmoType.None, p.Tick(1f, weapons, ammo), "con balas en el cargador del francotirador tampoco");
            weapons.Add(new WeaponState(WeaponCatalog.Get("shotgun"), 0));
            AmmoType got = AmmoType.None; for (int i = 0; i < 60 && got == AmmoType.None; i++) got = p.Tick(1f, weapons, ammo);
            Assert.AreEqual(AmmoType.Shotgun, got, "la escopeta recién cogida y vacía también");
        }

        [Test]
        public void HeavyAmmoAndSmallHealing_ClearlyMoreThanBefore([ValueSource(nameof(Maps))] string id)
        {
            var l = Loot(id);
            double rifle = 0, shotgun = 0, pistol = 0, heal = 0;
            foreach (var t in new[] { "ground", "low", "mid", "high", "drop_common", "drop_special", "drop_heavy" })
            {
                var r = ScarcityReport.Expected(l, t);
                rifle += r.RifleAmmo; shotgun += r.ShotgunAmmo; pistol += r.PistolAmmo; heal += r.HealHp;
            }
            Debug.Log("[Economía] " + id + " por tirada de cada tabla: rifle " + rifle.ToString("0.0") + " escopeta " + shotgun.ToString("0.0") + " pistola " + pistol.ToString("0.0") + " curación " + heal.ToString("0"));
            Assert.Greater(rifle, pistol * 0.85, "el rifle ya no es el más escaso");
            Assert.Greater(shotgun, pistol * 0.3, "la escopeta sube (antes ~0.2 de la pistola)");
            Assert.Less(shotgun, pistol, "pero sigue por debajo de la pistola: no sobra");
        }
    }
}

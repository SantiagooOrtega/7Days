using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>
    /// Economía de munición de los 2 mapas jugables: simula los 7 días (relleno en el peor caso, drops de tablas y lo que completa la economía) y falla si las balas
    /// por zombi se salen de ~1.5 el día 1 bajando a ~1.2 el día 7, o si las fuentes naturales ya se pasan del presupuesto (no habría escasez).
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
        public void TargetRatio_From1_5To1_2()
        {
            Assert.AreEqual(1.5f, AmmoEconomy.TargetRatio(1), 1e-4f);
            Assert.AreEqual(1.2f, AmmoEconomy.TargetRatio(7), 1e-4f);
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
                Assert.LessOrEqual(d.Refill + d.TableDrops, d.Budget * 1.05, id + " día " + d.Day + ": las fuentes naturales ya superan el presupuesto");
                Assert.AreEqual(AmmoEconomy.TargetRatio(d.Day), d.Ratio, 0.08, id + " día " + d.Day + " balas/zombi");
                Assert.LessOrEqual(d.Ratio, prev + 0.02, "no debe subir de un día a otro");
                prev = d.Ratio;
            }
            double initial = AmmoEconomy.RefillRounds(l, l.points) + l.startReserve + l.startMagazine;
            sb.AppendLine("Con el botín inicial (" + initial.ToString("0") + "): " + (totalNew + initial).ToString("0") + " frente a " + (totalOld + initial).ToString("0") + " (" + (((totalNew + initial) / (totalOld + initial)) - 1).ToString("+0%;-0%") + ")");
            sb.AppendLine("Total 7 días: " + totalNew.ToString("0") + " balas (antes " + totalOld.ToString("0") + ", " + ((totalNew / System.Math.Max(1, totalOld) - 1) * 100).ToString("+0;-0") + " %)");
            Debug.Log(sb.ToString());
            Assert.Greater(totalNew, totalOld * 1.25, "debe haber bastante más munición que antes");
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
        public void Pity_FiresAfter90sWithoutAmmo_AndResetsWithAmmo()
        {
            var p = new AmmoPity();
            for (int i = 0; i < 89; i++) Assert.IsFalse(p.Tick(1f, false));
            Assert.IsFalse(p.Tick(0.5f, true));   // encontró balas: vuelve a contar
            for (int i = 0; i < 89; i++) Assert.IsFalse(p.Tick(1f, false));
            Assert.IsTrue(p.Tick(1.5f, false));
            Assert.AreEqual(0f, p.Seconds);
        }

        [Test]
        public void PickType_LowestReserve()
        {
            var t = AmmoEconomy.PickType(new[] { AmmoType.Pistol, AmmoType.Rifle }, a => a == AmmoType.Pistol ? 40 : 5, a => a == AmmoType.Pistol ? 48 : 90);
            Assert.AreEqual(AmmoType.Rifle, t);
            Assert.AreEqual(AmmoType.Pistol, AmmoEconomy.PickType(new AmmoType[0], a => 0, a => 1));
        }
    }
}

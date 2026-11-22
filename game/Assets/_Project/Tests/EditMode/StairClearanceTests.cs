using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>
    /// Paso libre en la escalera de las casas con piso 2: ancho mínimo 1.2 m y 2.3 m de altura libre sobre cada escalón, sin nada atravesado (la viga del
    /// techo que cruzaba el hueco a ~2.6 m bloqueaba al jugador y a los zombis).
    /// </summary>
    public class StairClearanceTests
    {
        [TestCase("calle_en_ruinas"), TestCase("bosque_oscuro")]
        public void Stairs_HaveFreePassage(string id)
        {
            var m = JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Maps/" + id + ".json")));
            int checkedHouses = 0;
            foreach (var h in m.houses)
            {
                if (!h.upperFloor) continue;
                checkedHouses++;
                var w = HousePlanner.Stairwell(h);
                Assert.GreaterOrEqual(w[3] - w[2], HousePlanner.StairMinWidth, id + ": ancho de la escalera");
                var bad = HousePlanner.StairObstructions(h, HousePlanner.Plan(h));
                CollectionAssert.IsEmpty(bad, id + ": piezas atravesadas en la escalera");
            }
            if (id == "calle_en_ruinas") Assert.AreEqual(1, checkedHouses, "la casa de la Calle tiene piso 2");
        }

        [Test]
        public void CeilingBeam_IsCutAtTheStairwell()
        {
            var h = JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Maps/calle_en_ruinas.json"))).houses[0];
            var plan = HousePlanner.Plan(h); var w = HousePlanner.Stairwell(h);
            int beams = 0;
            foreach (var b in plan.Boxes)
            {
                if (b.Tag != "beam") continue; beams++;
                bool inZ = b.Center.Z + b.Size.Z / 2f > w[2] && b.Center.Z - b.Size.Z / 2f < w[3];
                bool inX = b.Center.X + b.Size.X / 2f > w[0] && b.Center.X - b.Size.X / 2f < w[1];
                Assert.IsFalse(inZ && inX, "ninguna viga sobre el hueco de la escalera");
            }
            Assert.GreaterOrEqual(beams, 4, "la viga cortada queda en dos tramos");
        }
    }
}

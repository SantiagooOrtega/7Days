using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Suministros fuera del mapa: regla del perímetro (1 m de margen), JSON de los 2 mapas, reposición de 7 días y 30 drops que caen fuera.</summary>
    public class LootPlacementTests
    {
        public static string[] Maps() => new[] { "calle_en_ruinas", "bosque_oscuro" };
        private static MapData Map(string id) => JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Maps/" + id + ".json")));

        [Test]
        public void Inside_RespectsTheMargin_AndClampBringsPointsBack()
        {
            var sq = LootPlacement.Square(30f);
            Assert.IsTrue(LootPlacement.Inside(sq, 0f, 0f));
            Assert.IsTrue(LootPlacement.Inside(sq, 28.9f, -28.9f));
            Assert.IsFalse(LootPlacement.Inside(sq, 29.5f, 0f), "a menos de 1 m del muro");
            Assert.IsFalse(LootPlacement.Inside(sq, -33f, -17.4f), "detrás del muro");
            var c = LootPlacement.Clamp(sq, -33f, -17.4f);
            Assert.IsTrue(LootPlacement.Inside(sq, c[0], c[1]));
            Assert.Less(Vector2.Distance(new Vector2(c[0], c[1]), new Vector2(-33f, -17.4f)), 6f, "lo más cerca posible");
            var keep = LootPlacement.Clamp(sq, 3f, 4f); Assert.AreEqual(3f, keep[0]); Assert.AreEqual(4f, keep[1]);
            // polígono no cuadrado (por si un mapa futuro lo trae)
            var tri = new[] { new[] { 0f, 0f }, new[] { 20f, 0f }, new[] { 0f, 20f } };
            Assert.IsTrue(LootPlacement.Inside(tri, 4f, 4f)); Assert.IsFalse(LootPlacement.Inside(tri, 15f, 15f));
            var t = LootPlacement.Clamp(tri, 15f, 15f); Assert.IsTrue(LootPlacement.Inside(tri, t[0], t[1]));
        }

        [TestCaseSource(nameof(Maps))]
        public void Json_AllLootPointsInsideThePerimeter(string id)
        {
            var m = Map(id);
            CollectionAssert.IsEmpty(LootPlacement.Outside(m.loot, m.halfSize), id);
            CollectionAssert.IsEmpty(MapKit.Validate(m), id);
            var broken = Map(id); broken.loot.points[0].x = broken.halfSize + 3f;
            Assert.IsNotEmpty(MapKit.Validate(broken), "un punto fuera invalida el mapa");
        }

        [Test]
        public void Defaults_NeverPickBushesOrCarsBehindTheWall()
        {
            var props = new List<PropEntry>
            {
                new PropEntry { type = PropTypes.Bush, x = -33f, z = -17f, scale = 1.3f },
                new PropEntry { type = PropTypes.Bush, x = -32f, z = 33f, scale = 1.3f },
                new PropEntry { type = PropTypes.Bush, x = 10f, z = 5f, scale = 1.3f },
                new PropEntry { type = PropTypes.CarWreck, x = 40f, z = 0f },
            };
            var l = LootDefaults.Create(props, null, 30f);
            CollectionAssert.IsEmpty(LootPlacement.Outside(l, 30f));
        }

        [TestCaseSource(nameof(Maps))]
        public void SevenDaysOfRefill_And30DropsOutside_AllEndUpInside(string id)
        {
            var m = Map(id);
            var poly = LootPlacement.Square(m.halfSize);
            var st = new LootDirectorState(m.loot);
            var rng = new System.Random(3);
            for (int day = 2; day <= 7; day++)
            {
                foreach (var p in st.Points) p.Active = false;
                foreach (var p in st.NewDay(day, 0, rng)) Assert.IsTrue(LootPlacement.Inside(poly, p.x, p.z), id + " día " + day + " " + p.id);
            }
            for (int i = 0; i < 30; i++)
            {
                // zombis que mueren en el borde y drops empujados fuera por una explosión
                float x = (float)(rng.NextDouble() * 2 - 1) * (m.halfSize + 10f), z = (float)(rng.NextDouble() * 2 - 1) * (m.halfSize + 10f);
                var c = LootPlacement.Clamp(poly, x, z);
                Assert.IsTrue(LootPlacement.Inside(poly, c[0], c[1]), id + " drop " + i);
            }
        }
    }
}

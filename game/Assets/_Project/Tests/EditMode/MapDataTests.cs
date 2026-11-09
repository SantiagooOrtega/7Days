using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Mapas como datos: el kit del mapa 1, los spawns en los bordes y la validación de mapas que vienen de fuera.</summary>
    public class MapDataTests
    {
        private static MapData Valid()
        {
            var house = HouseLayoutData.Default();
            return new MapData
            {
                props = MapKit.StreetRuinsProps(), houses = new[] { house },
                spawns = MapKit.EdgeSpawns(30f, 3, 2f), loot = LootDefaults.Create(null, house), vegetation = new PropLayoutData(),
            };
        }

        [Test]
        public void StreetRuins_IsValid_AndKeepsItsPieces()
        {
            var m = Valid();
            CollectionAssert.IsEmpty(MapKit.Validate(m));
            var kinds = new HashSet<string>(); foreach (var p in m.props) kinds.Add(p.kind);
            CollectionAssert.IsSubsetOf(kinds, MapKit.Kinds);
            int containers = 0, barriers = 0, lamps = 0; foreach (var p in m.props) { if (p.kind == "container") containers++; if (p.kind == "barrier") barriers++; if (p.kind == "lamp") lamps++; }
            Assert.AreEqual(5, containers); Assert.AreEqual(5, barriers); Assert.AreEqual(4, lamps);
            Assert.AreEqual("Ground", m.props[0].name, "el orden importa: la semilla se consume pieza a pieza");
        }

        [Test]
        public void EdgeSpawns_AreOnTheBorder_OnFourSides()
        {
            var s = MapKit.EdgeSpawns(30f, 3, 2f);
            Assert.AreEqual(12, s.Length);
            var sides = new HashSet<string>();
            foreach (var p in s) { sides.Add(p.side); Assert.AreEqual(28f, System.Math.Max(System.Math.Abs(p.x), System.Math.Abs(p.z)), 0.01f); }
            Assert.AreEqual(4, sides.Count);
            // apartados de lo que bloquea el paso
            var moved = MapKit.EdgeSpawns(30f, 3, 2f, (x, z) => System.Math.Abs(x) < 1f && z > 0f);
            foreach (var p in moved) if (p.side == "n") Assert.GreaterOrEqual(System.Math.Abs(p.x), 1f);
        }

        [Test]
        public void Validation_RejectsBrokenMaps()
        {
            MapData Broken(System.Action<MapData> f) { var m = Valid(); f(m); return m; }
            Assert.IsNotEmpty(MapKit.Validate(Broken(m => m.props[0].kind = "castillo")));
            Assert.IsNotEmpty(MapKit.Validate(Broken(m => m.spawns = new MapSpawn[3])), "pocos spawns");
            Assert.IsNotEmpty(MapKit.Validate(Broken(m => m.spawns[0] = new MapSpawn { id = "c", x = 0f, z = 0f, side = "n" })), "spawn en el centro");
            Assert.IsNotEmpty(MapKit.Validate(Broken(m => m.houses = new HouseLayoutData[0])), "sin casa");
            Assert.IsNotEmpty(MapKit.Validate(Broken(m => m.playerStart = new[] { 99f, 0f, 0f, 0f })), "jugador fuera");
            Assert.IsNotEmpty(MapKit.Validate(Broken(m => m.ambience.fogDensity = 1f)));
            Assert.IsNotEmpty(MapKit.Validate(null));
        }
    }
}

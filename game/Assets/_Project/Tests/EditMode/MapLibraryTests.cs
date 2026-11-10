using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Mapas 2 (Barrio abandonado) y 3 (Bosque oscuro): válidos, con casas correctas, spawns en el borde y libres, botín con la misma escasez y bosque con LOD.</summary>
    public class MapLibraryTests
    {
        public static IEnumerable<string> Ids() { yield return MapLibrary.NeighborhoodId; yield return MapLibrary.ForestId; }
        private static MapData Get(string id) => id == MapLibrary.ForestId ? MapLibrary.Forest() : MapLibrary.Neighborhood();

        [TestCaseSource(nameof(Ids))]
        public void Map_IsValid_HousesValid_AndVegetationAccepted(string id)
        {
            var m = Get(id);
            CollectionAssert.IsEmpty(MapKit.Validate(m));
            foreach (var h in m.houses) CollectionAssert.IsEmpty(HouseValidator.Validate(h).Errors, id + " casa en " + h.x + "," + h.z);
            var v = PropLayoutValidator.Validate(m.vegetation);
            CollectionAssert.IsEmpty(v.Errors);
            Assert.AreEqual(id, m.id);
        }

        [TestCaseSource(nameof(Ids))]
        public void Loot_KeepsTheScarcityOfMap1(string id)
        {
            var m = Get(id); var d = LootDefaults.Create();
            Assert.AreEqual(d.maxActivePickups, m.loot.maxActivePickups); Assert.AreEqual(d.maxDropsPerDay, m.loot.maxDropsPerDay);
            Assert.AreEqual(d.respawnFractionPerDay, m.loot.respawnFractionPerDay); Assert.AreEqual(d.startReserve, m.loot.startReserve);
            Assert.AreEqual(d.tables.Length, m.loot.tables.Length); Assert.AreEqual(d.drops.Length, m.loot.drops.Length);
            Assert.That(m.loot.points.Length, Is.InRange(d.points.Length - 4, d.points.Length + 4), "más o menos los mismos puntos que el mapa 1");
            int guaranteed = 0; foreach (var p in m.loot.points) { if (p.guaranteeWeapon) guaranteed++; Assert.Less(Math.Max(Math.Abs(p.x), Math.Abs(p.z)), m.halfSize, p.id); }
            Assert.AreEqual(3, guaranteed, "3 armas garantizadas, como el mapa 1");
        }

        [TestCaseSource(nameof(Ids))]
        public void Spawns_AreFreeOfSolidPieces(string id)
        {
            var m = Get(id);
            foreach (var s in m.spawns)
            {
                foreach (var e in m.vegetation.items)
                    if (SolidCatalog.IsSolid(e.type, e.variant, e.scale)) Assert.Greater(Dist(s.x, s.z, e.x, e.z), 2f, id + " spawn " + s.id + " pegado a " + e.type);
                foreach (var p in m.props)
                    if (p.kind == "trunk" || p.kind == "boulder" || p.kind == "fence" || p.kind == "container") Assert.Greater(Dist(s.x, s.z, p.pos[0], p.pos[2]), 2.5f, id + " spawn " + s.id + " pegado a " + p.name);
            }
        }

        [Test]
        public void Forest_HasManyTrees_FiveVariants_ClearingsAndNaturalCover()
        {
            var m = MapLibrary.Forest();
            var kinds = new HashSet<string>(); int trees = 0;
            foreach (var e in m.vegetation.items) if (PropTypes.IsForestTree(e.type)) { trees++; kinds.Add(e.type + e.variant); foreach (var c in MapLibrary.ForestClearings) Assert.Greater(Dist(e.x, e.z, c[0], c[1]), c[2] - 0.5f, "árbol dentro de un claro"); }
            Assert.Greater(trees, 500); Assert.AreEqual(5, kinds.Count, "5 variantes de árbol");
            int trunks = 0, rocks = 0; foreach (var p in m.props) { if (p.kind == "trunk") trunks++; if (p.kind == "boulder") rocks++; }
            Assert.GreaterOrEqual(trunks, 15); Assert.GreaterOrEqual(rocks, 20);
            Assert.Greater(m.ambience.fogDensity, 0.03f, "niebla densa"); Assert.AreEqual("forest", m.ambience.soundscape); Assert.Greater(m.ambience.fireflies, 0f);
        }

        [Test]
        public void ForestTrees_HaveThreeLods_TheLastVeryCheap()
        {
            foreach (var (type, v) in ForestGeometry.TreeKinds())
            {
                int t0 = ForestGeometry.Tree(type, v, 0).Triangles, t1 = ForestGeometry.Tree(type, v, 1).Triangles, t2 = ForestGeometry.Tree(type, v, 2).Triangles;
                Assert.Greater(t0, t1, type + v); Assert.Greater(t1, t2, type + v);
                Assert.LessOrEqual(t2, 40, type + v + ": el LOD 2 es una cruz de pocas caras");
                Assert.LessOrEqual(t0, 900, type + v);
                Assert.Greater(Winding.SignedVolume(ForestGeometry.Tree(type, v, 0).Trunk), 0f, "tronco cerrado y hacia fuera");
            }
        }

        private static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }
}

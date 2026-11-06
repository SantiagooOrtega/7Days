using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class PropLayoutTests
    {
        private static readonly List<Zone> Zones = new List<Zone>
        {
            Zone.Circle(-19f, 4f, 4f), Zone.Circle(19f, -2f, 4f), Zone.Circle(0f, -4f, 1.5f),
            Zone.Rect(-14f, 8f, 10.5f, 17.5f), Zone.Rect(-24f, 19f, 24f, 29f),
        };

        private static PropLayoutData Make(float density = 1f, int seed = 3) =>
            VegetationLayout.Generate(new VegetationParams { Density = density, Seed = seed }, Zones);

        private static IEnumerable<PropEntry> Of(PropLayoutData l, string t) => l.items.Where(i => i.type == t);

        [Test]
        public void Generate_IsDeterministic_AndSeedChangesIt()
        {
            string a = string.Join(";", Make().items.Select(i => i.type + i.x + i.z));
            Assert.AreEqual(a, string.Join(";", Make().items.Select(i => i.type + i.x + i.z)));
            Assert.AreNotEqual(a, string.Join(";", Make(1f, 99).items.Select(i => i.type + i.x + i.z)));
        }

        [Test]
        public void EverythingGenerated_PassesTheValidator()
        {
            var res = PropLayoutValidator.Validate(Make());
            Assert.AreEqual(0, res.Errors.Count, string.Join("\n", res.Errors));
            Assert.AreEqual(Make().items.Length, res.Valid.Count);
        }

        [Test]
        public void Trees_FormARingOutsideTheWalls_AndNeverOverlapEachOther()
        {
            var trees = Of(Make(), PropTypes.TreeDead).ToList();
            Assert.Greater(trees.Count, 40);
            int ring = trees.Count(t => Math.Max(Math.Abs(t.x), Math.Abs(t.z)) >= 31.5f);
            Assert.Greater(ring / (double)trees.Count, 0.75, "la mayoría de los árboles tapa el borde del mapa");
            for (int i = 0; i < trees.Count; i++)
                for (int j = i + 1; j < trees.Count; j++)
                {
                    double d = Math.Sqrt(Math.Pow(trees[i].x - trees[j].x, 2) + Math.Pow(trees[i].z - trees[j].z, 2));
                    Assert.GreaterOrEqual(d, 2.0, "árboles encimados");
                }
        }

        [Test]
        public void BigPieces_AvoidTheStreet_AndTheExcludedZones()
        {
            var l = Make();
            foreach (var e in l.items.Where(i => i.type == PropTypes.TreeDead || i.type == PropTypes.Bush || i.type == PropTypes.CarWreck))
            {
                if (Math.Abs(e.x) <= 30f && Math.Abs(e.z) <= 30f)
                    Assert.Greater(Math.Abs(e.x), 4.3f, e.type + " en medio de la calle");
                foreach (var z in Zones) Assert.IsFalse(z.Contains(e.x, e.z, 0f), e.type + " dentro de una zona excluida");
            }
        }

        [Test]
        public void Grass_NeverGrowsInsideSolidObjects()
        {
            foreach (var g in Of(Make(), PropTypes.Grass))
                foreach (var z in Zones) Assert.IsFalse(z.Contains(g.x, g.z, 0f));
        }

        [Test]
        public void Density_ScalesCounts_ButRespectsTheCaps()
        {
            Assert.AreEqual(0, Make(0f).items.Length);
            int half = Make(0.5f).items.Length, one = Make(1f).items.Length, big = Make(10f).items.Length;
            Assert.Less(half, one);
            Assert.Less(one, big);
            var l = Make(10f);
            var p = new VegetationParams();
            Assert.LessOrEqual(Of(l, PropTypes.TreeDead).Count(), p.MaxTrees);
            Assert.LessOrEqual(Of(l, PropTypes.Bush).Count(), p.MaxBushes);
            Assert.LessOrEqual(Of(l, PropTypes.Grass).Count(), p.MaxGrass);
            int props = l.items.Count(i => i.type != PropTypes.TreeDead && i.type != PropTypes.Bush && i.type != PropTypes.Grass);
            Assert.LessOrEqual(props, p.MaxProps);
        }

        [Test]
        public void AllTypes_ArePresent_AtDefaultDensity()
        {
            var l = Make();
            foreach (var t in PropTypes.All) Assert.Greater(Of(l, t).Count(), 0, "falta el tipo " + t);
        }

        [Test]
        public void Validator_RejectsBadEntries_AndReportsWhy()
        {
            var layout = new PropLayoutData
            {
                items = new[]
                {
                    new PropEntry { type = "tree_dead", x = 5, z = 5, scale = 1, variant = 0 },
                    new PropEntry { type = "dragon", x = 0, z = 0, scale = 1 },
                    new PropEntry { type = "bush", x = 999, z = 0, scale = 1 },
                    new PropEntry { type = "bush", x = float.NaN, z = 0, scale = 1 },
                    new PropEntry { type = "bush", x = 1, z = 1, scale = 40 },
                    new PropEntry { type = "tree_dead", x = 1, z = 1, scale = 1, variant = 9 },
                    null,
                },
            };
            var r = PropLayoutValidator.Validate(layout);
            Assert.AreEqual(1, r.Valid.Count);
            Assert.AreEqual(6, r.Errors.Count);
            StringAssert.Contains("desconocido", r.Errors[0]);
        }

        [Test]
        public void Validator_AppliesPerTypeCaps()
        {
            var items = Enumerable.Range(0, 10).Select(i => new PropEntry { type = "tire", x = i, z = 0, scale = 1 }).ToArray();
            var r = PropLayoutValidator.Validate(new PropLayoutData { items = items }, new Dictionary<string, int> { { "tire", 4 } });
            Assert.AreEqual(4, r.Valid.Count);
            Assert.AreEqual(6, r.Errors.Count);
        }

        [Test]
        public void Obstacles_AreTheBigPieces_NotGrassOrBags()
        {
            Assert.IsTrue(PropTypes.IsObstacle(PropTypes.TreeDead));
            Assert.IsTrue(PropTypes.IsObstacle(PropTypes.CarWreck));
            Assert.IsFalse(PropTypes.IsObstacle(PropTypes.Grass));
            Assert.IsFalse(PropTypes.IsObstacle(PropTypes.Bag));
        }
    }
}

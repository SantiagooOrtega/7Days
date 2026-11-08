using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Decals de heridas pegados a la malla real: existen los 12 de daño y los 8 del individuo, miran hacia fuera y quedan sobre la superficie.</summary>
    public class ZombieWoundsTests
    {
        public static IEnumerable<string> Types() => ZombieAnatomies.Types;

        private static List<WoundPatch> Patches(string type, out ZombieModelResult model, out ZombieSkinned skinned)
        {
            var a = ZombieAnatomies.For(type);
            var v = ZombieVariants.ForType(ZombieVariants.Defaults(), type)[0];
            model = ZombieModel.Build(a, v, 0);
            skinned = ZombieSkinning.Flatten(model, a.Size);
            return ZombieWounds.Build(model, skinned, a);
        }

        [TestCaseSource(nameof(Types))]
        public void EveryType_GetsAllTwelveDamageDecals_AndEightIndividualOnes(string type)
        {
            var p = Patches(type, out _, out _);
            int damage = 0, indiv = 0;
            var zones = new HashSet<string>();
            foreach (var w in p) { if (w.Spot.Stage > 0) { damage++; zones.Add(w.Spot.Zone + "" + w.Spot.Stage); } else indiv++; }
            Assert.AreEqual(12, damage, type); Assert.AreEqual(8, indiv, type); Assert.AreEqual(12, zones.Count);
        }

        [TestCaseSource(nameof(Types))]
        public void Decals_HaveValidUvs_AndFaceOutward(string type)
        {
            foreach (var w in Patches(type, out _, out _))
            {
                Assert.Greater(w.Mesh.Triangles.Count, 0);
                foreach (var uv in w.Mesh.Uvs) { Assert.That(uv.X, Is.InRange(0f, 1f)); Assert.That(uv.Y, Is.InRange(0f, 1f)); }
                var outward = new V3((float)Math.Sin(w.Spot.Angle * Math.PI / 180.0), 0f, (float)Math.Cos(w.Spot.Angle * Math.PI / 180.0));
                for (int t = 0; t < w.Mesh.Triangles.Count; t += 3)
                {
                    V3 n = Winding.FrontNormal(w.Mesh.Positions[w.Mesh.Triangles[t]], w.Mesh.Positions[w.Mesh.Triangles[t + 1]], w.Mesh.Positions[w.Mesh.Triangles[t + 2]]).Normalized;
                    Assert.Greater(V3.Dot(n, outward), -0.2f, w.Spot.Id + " mira hacia fuera");
                }
            }
        }

        [Test]
        public void ClothSpots_BecomeTears()
        {
            var p = Patches(ZombieIds.Common, out _, out _);
            int tears = 0; foreach (var w in p) if (w.OnCloth && w.Spot.Stage > 0) tears++;
            Assert.Greater(tears, 3, "con ropa, las heridas de daño salen como desgarros");
        }

        [Test]
        public void Building_IsFastEnoughToDoOncePerType()
        {
            var sw = Stopwatch.StartNew();
            Patches(ZombieIds.Armored, out _, out _);
            TestContext.WriteLine("Proyectar 20 decals: " + sw.ElapsedMilliseconds + " ms");
            Assert.Less(sw.ElapsedMilliseconds, 1500);
        }
    }
}

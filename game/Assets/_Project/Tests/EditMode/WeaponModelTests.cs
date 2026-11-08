using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class WeaponModelTests
    {
        public static IEnumerable<WeaponData> Weapons()
        {
            foreach (var w in WeaponCatalog.Defaults()) yield return w;
            foreach (var id in new[] { "bat", "axe", "shovel" }) yield return new WeaponData { id = id, kind = WeaponKind.Melee, length = 0.9f, thickness = 0.05f };
        }

        [TestCaseSource(nameof(Weapons))]
        public void WithinBudget_ValidMeshes_ClosedParts(WeaponData w)
        {
            var m = WeaponModel.Build(w);
            Assert.LessOrEqual(m.Triangles, WeaponModel.TriangleBudget, w.id + ": " + m.Triangles + " triángulos");
            TestContext.WriteLine(w.id + ": " + m.Triangles + " triángulos, " + m.Parts.Count + " piezas");
            foreach (var p in m.Parts)
            {
                Assert.Greater(Winding.SignedVolume(p.Mesh), 0f, w.id + " " + p.Name + ": caras hacia dentro");
                for (int i = 0; i < p.Mesh.Positions.Count; i++)
                {
                    Assert.That(p.Mesh.Uvs[i].X, Is.InRange(0f, 1f)); Assert.That(p.Mesh.Uvs[i].Y, Is.InRange(0f, 1f));
                    Assert.AreEqual(1f, p.Mesh.Normals[i].Length, 0.02f, w.id + " " + p.Name);
                    Assert.AreEqual(1f, p.Mesh.Tangents[i].Length, 0.02f, w.id + " " + p.Name);
                }
            }
        }

        [TestCaseSource(nameof(Weapons))]
        public void AnimatedParts_KeepNamesAndRestPositions(WeaponData w)
        {
            var m = WeaponModel.Build(w);
            var names = new Dictionary<string, WeaponPart>();
            foreach (var p in m.Parts) names[p.Name] = p;
            Assert.IsTrue(names.ContainsKey("Body"), "cuerpo"); Assert.IsTrue(names.ContainsKey("Grip"), "empuñadura");
            if (w.IsMelee) return;
            switch (w.kind)
            {
                case WeaponKind.Pistol: Assert.IsTrue(names.ContainsKey("Slide") && names.ContainsKey("Mag")); break;
                case WeaponKind.Revolver: Assert.IsTrue(names.ContainsKey("Cylinder")); break;
                case WeaponKind.Shotgun:
                    Assert.IsTrue(names.ContainsKey("Pump") && names.ContainsKey("Stock"));
                    Assert.AreEqual(w.length * WeaponGrip.PumpAt, names["Pump"].Position.Z, 1e-4f, "la bomba donde la agarra la mano");
                    break;
                case WeaponKind.SniperRifle: Assert.IsTrue(names.ContainsKey("Bolt") && names.ContainsKey("Mag") && names.ContainsKey("Stock")); break;
                default: Assert.IsTrue(names.ContainsKey("Mag") && names.ContainsKey("Slide")); break;
            }
            // la empuñadura sigue donde la calibra el agarre
            var g = w.grip ?? WeaponGrip.For(w.kind, w.length, w.thickness);
            Assert.AreEqual(g.grip[1], names["Grip"].Position.Y, 1e-4f); Assert.AreEqual(g.grip[2], names["Grip"].Position.Z, 1e-4f);
        }

        [TestCaseSource(nameof(Weapons))]
        public void SightPoint_LiesOnTheLineOfSight_AboveTheBore(WeaponData w)
        {
            if (w.IsMelee) Assert.Pass("sin mira");
            var m = WeaponModel.Build(w);
            V3 a = m.SightLineA, b = m.SightLineB, s = m.Sight;
            V3 dir = (b - a).Normalized;
            V3 off = (s - a) - dir * V3.Dot(s - a, dir);
            Assert.Less(off.Length, 0.002f, w.id + ": el punto de mira se sale de la línea de mira (" + (off.Length * 1000f).ToString("0.0") + " mm)");
            Assert.AreEqual(0f, s.X, 1e-4f, "centrado");
            Assert.Greater(s.Y, 0.03f, "por encima del cañón");
            Assert.Less(s.Z, a.Z, "detrás de la mira (donde va el ojo)");
            Assert.AreEqual(w.kind == WeaponKind.SniperRifle || w.kind == WeaponKind.AssaultRifle || w.kind == WeaponKind.Smg, m.HasOptic, "óptica en fusil, subfusil y francotirador");
        }
    }
}

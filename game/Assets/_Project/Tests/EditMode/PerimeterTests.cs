using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Perímetro como trinchera/cerca: sin huecos a lo largo de cada lado, esquinas cerradas, nada dentro de la zona jugable, más de 1.8 m con el alambre y el muro collider debajo.</summary>
    public class PerimeterTests
    {
        public static IEnumerable<TestCaseData> Maps() { yield return new TestCaseData("calle_en_ruinas", "trench"); yield return new TestCaseData("bosque_oscuro", "logs"); }
        private static MapData Map(string id) => JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Maps/" + id + ".json")));
        private static readonly PerimeterPiece[] Solid = { PerimeterPiece.Sandbag, PerimeterPiece.Concrete, PerimeterPiece.Log };

        [TestCaseSource(nameof(Maps))]
        public void Barrier_NoGaps_CornersClosed_NothingInside_TallEnough(string id, string style)
        {
            var m = Map(id);
            Assert.AreEqual(style, m.ambience.perimeter);
            var parts = PerimeterLayout.Build(m.halfSize, style, m.seed + 101);
            for (int side = 0; side < 4; side++)
            {
                var spans = parts.Where(p => p.Side == side && Solid.Contains(p.Kind)).Select(p => { float a = p.Side < 2 ? p.Center.X : p.Center.Z; return (a - p.Size.X / 2f, a + p.Size.X / 2f); }).OrderBy(s => s.Item1).ToList();
                float lo = -m.halfSize - PerimeterLayout.Depth, hi = m.halfSize + PerimeterLayout.Depth, cur = lo;
                Assert.LessOrEqual(spans[0].Item1, lo + 0.05f, id + " lado " + side + ": esquina abierta al empezar");
                foreach (var s in spans) { Assert.LessOrEqual(s.Item1, cur + 0.08f, id + " lado " + side + ": hueco en " + cur); cur = Math.Max(cur, s.Item2); }
                Assert.GreaterOrEqual(cur, hi - 0.05f, id + " lado " + side + ": esquina abierta al final");
            }
            foreach (var p in parts.Where(p => p.Kind != PerimeterPiece.Barrel))
                Assert.GreaterOrEqual(PerimeterLayout.Outward(p, m.halfSize) - p.Size.Z / 2f, -0.05f, id + ": " + p.Kind + " invade la zona jugable");
            float top = parts.Where(p => p.Kind == PerimeterPiece.Wire).Max(p => p.Center.Y + p.Size.Y / 2f);
            Assert.Greater(top, 1.8f, "con el alambre pasa de 1.8 m");
            Assert.IsTrue(parts.Any(p => p.Kind == PerimeterPiece.Wire) && parts.Any(p => p.Kind == PerimeterPiece.Post));
            if (style == "trench") Assert.IsTrue(parts.Any(p => p.Kind == PerimeterPiece.Concrete) && parts.Any(p => p.Kind == PerimeterPiece.FencePanel) && parts.Any(p => p.Kind == PerimeterPiece.Plank), "variación por tramo");
            // el muro collider sigue ahí, más alto que lo que se puede saltar
            var walls = m.props.Where(p => (p.kind == "wall" || p.kind == "boundary") && Math.Max(Math.Abs(p.pos[0]), Math.Abs(p.pos[2])) > m.halfSize - 0.5f).ToList();
            Assert.AreEqual(4, walls.Count, id + ": 4 muros collider");
            foreach (var w in walls) Assert.Greater(w.size[1], 1.8f);
        }

        [Test]
        public void Layout_IsDeterministic_AndCoilHasTurns()
        {
            var a = PerimeterLayout.Build(30f, "trench", 7); var b = PerimeterLayout.Build(30f, "trench", 7);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i].Kind, b[i].Kind); Assert.AreEqual(a[i].Center.X, b[i].Center.X); }
            var c = PerimeterLayout.Coil(3f, 0.28f);
            Assert.Greater(c.Count, 100);
            Assert.AreEqual(-1.5f, c[0].X, 1e-4f); Assert.AreEqual(1.5f, c[c.Count - 1].X, 1e-4f);
        }
    }
}

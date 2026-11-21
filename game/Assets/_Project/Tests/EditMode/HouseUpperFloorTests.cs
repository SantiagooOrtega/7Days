using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Segundo piso de la casa de la Calle: escalera que se sube andando, hueco libre, suelo arriba, ventanas con línea de tiro, barandillas, balcón y techo caído.</summary>
    public class HouseUpperFloorTests
    {
        private static HouseLayoutData L() => HouseLayoutData.WithUpperFloor(HouseLayoutData.Default());
        private static HousePlan P() => HousePlanner.Plan(L());

        [Test]
        public void Layout_IsValid_AndGroundOnlyHouseHasNoUpperFloor()
        {
            CollectionAssert.IsEmpty(HouseValidator.Validate(L()).Errors);
            Assert.IsFalse(HousePlanner.Plan(HouseLayoutData.Default()).Boxes.Any(b => b.Tag == "upper_floor" || b.Tag == "stair"), "sin upperFloor la casa no cambia");
        }

        [Test]
        public void Stairs_StepsTheCharacterControllerCanClimb_EndingAtTheUpperFloor()
        {
            var l = L(); var steps = P().Boxes.Where(b => b.Tag == "stair").OrderBy(b => b.Size.Y).ToList();
            Assert.AreEqual(HousePlanner.StairSteps, steps.Count);
            float prev = 0f;
            foreach (var s in steps) { Assert.LessOrEqual(s.Size.Y - prev, 0.3f, "más alto que el stepOffset del jugador (0.3)"); Assert.IsTrue(s.Collider); prev = s.Size.Y; }
            Assert.AreEqual(HousePlanner.UpperFloorY(l), prev, 1e-4f);
            Assert.GreaterOrEqual(steps[0].Size.Z, 0.95f, "ancho para jugador y zombis");
        }

        [Test]
        public void Stairwell_IsOpen_RestOfTheUpperFloorIsSolid()
        {
            var l = L(); var plan = P(); float y = HousePlanner.UpperFloorY(l);
            var slabs = plan.Boxes.Where(b => b.Tag == "upper_floor").ToList();
            var w = HousePlanner.Stairwell(l);
            bool Hits(float x, float z) => slabs.Any(b => HousePlanner.SegmentHitsBox(new V3(x, y + 0.3f, z), new V3(x, y - 0.5f, z), b.Center, b.Size));
            Assert.IsFalse(Hits((w[0] + w[1]) / 2f, (w[2] + w[3]) / 2f), "el hueco de la escalera está libre");
            foreach (var (x, z) in new[] { (-3f, -2f), (2f, -2f), (-3f, 3f), (4.3f, 3f), (0f, 0f) }) Assert.IsTrue(Hits(x, z), "suelo arriba en " + x + "," + z);
            foreach (var b in slabs) Assert.IsTrue(b.Collider && b.Nav == NavFlag.Default, "suelo con collider y caminable");
        }

        [Test]
        public void UpperWindows_HaveLineOfFire_AndWallsGiveCover()
        {
            var l = L(); var plan = P(); float y = HousePlanner.UpperFloorY(l);
            var walls = plan.Boxes.Where(b => b.Kind == HouseBoxKind.Wall && b.Center.Y > y).ToList();
            Assert.Greater(walls.Count, 8);
            foreach (var info in plan.Openings.Where(o => o.Opening.id.StartsWith("up_win")))
            {
                Assert.That(info.Opening.sill, Is.InRange(0.9f, 1.1f), "antepecho a la altura de la cadera");
                var inside = info.Center - info.Normal * 1.5f; var outside = info.Center + info.Normal * 6f;
                Assert.IsFalse(walls.Any(b => HousePlanner.SegmentHitsBox(inside, outside, b.Center, b.Size)), info.Opening.id + ": se dispara por la ventana");
                var low = new V3(inside.X, y + 0.5f, inside.Z); var lowOut = new V3(outside.X, y + 0.5f, outside.Z);
                Assert.IsTrue(walls.Any(b => b.Cover && HousePlanner.SegmentHitsBox(low, lowOut, b.Center, b.Size)), info.Opening.id + ": agachado, el antepecho cubre");
            }
        }

        [Test]
        public void Rails_AroundTheHoleAndTheBalcony_AreCover_AndTheBalconyIsReachable()
        {
            var l = L(); var plan = P();
            var rails = plan.Boxes.Where(b => b.Tag == "rail" || b.Tag == "rail_balcony").ToList();
            Assert.GreaterOrEqual(rails.Count, 5);
            foreach (var r in rails) { Assert.IsTrue(r.Cover); Assert.AreEqual(HousePlanner.RailHeight, r.Size.Y, 1e-4f); }
            var b = HousePlanner.Balcony(l);
            var door = l.upperOpenings.First(o => o.id == "up_balcony_door");
            Assert.That(door.offset, Is.InRange(b[0] + door.width / 2f, b[1] - door.width / 2f), "la puerta da al balcón");
            Assert.IsTrue(plan.Boxes.Any(x => x.Tag == "balcony" && x.Nav == NavFlag.Default));
        }

        [Test]
        public void Roof_IsPartlyCollapsed_OnTopOfTheUpperFloor_AndFallenSlabsLieUpstairs()
        {
            var l = L(); var plan = P(); float y = HousePlanner.UpperFloorY(l);
            var tiles = plan.Boxes.Where(b => b.Kind == HouseBoxKind.Roof && b.Tag.StartsWith("tile")).ToList();
            Assert.Greater(tiles.Count, 0);
            foreach (var t in tiles) Assert.Greater(t.Center.Y, y + l.upperHeight - 0.01f, "el techo va encima del piso 2");
            int cols = (int)System.Math.Round(l.width / 2f), rows = (int)System.Math.Round(l.depth / 2f);
            Assert.Less(tiles.Count, cols * rows, "parcialmente caído");
            foreach (var f in plan.Boxes.Where(b => b.Tag == "fallen")) Assert.Greater(f.Center.Y, y, "las losas caídas quedan arriba");
        }

        [Test]
        public void StairFoot_IsClearOfFurnitureAndDoors()
        {
            var l = L(); var plan = P(); var foot = HousePlanner.StairFoot(l);
            foreach (var b in plan.Boxes.Where(x => x.Kind == HouseBoxKind.Furniture && x.Center.Y < 1.5f))
            {
                float hx = (b.Yaw % 180f == 0f ? b.Size.X : b.Size.Z) / 2f + 0.45f, hz = (b.Yaw % 180f == 0f ? b.Size.Z : b.Size.X) / 2f + 0.45f;
                Assert.IsFalse(System.Math.Abs(foot.X - b.Center.X) < hx && System.Math.Abs(foot.Z - b.Center.Z) < hz, "pie de la escalera libre de " + b.Tag);
            }
            Assert.Less(foot.X, l.width / 2f - l.wallThickness - 0.4f, "dentro de la casa");
        }
    }
}

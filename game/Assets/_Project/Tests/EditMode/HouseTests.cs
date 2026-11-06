using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class HouseTests
    {
        private static HouseLayoutData L() => HouseLayoutData.Default();
        private static HousePlan P() => HousePlanner.Plan(L());

        [Test]
        public void DefaultLayout_IsValid()
        {
            var v = HouseValidator.Validate(L());
            Assert.IsTrue(v.Ok, string.Join("\n", v.Errors));
        }

        [Test]
        public void Has_AtLeastThreeEntrances_FrontDoorBackDoorAndAVaultWindow_OnDifferentWalls()
        {
            var ops = L().openings;
            var front = ops.First(o => o.id == "front_door");
            var back = ops.First(o => o.id == "back_door");
            var big = ops.First(o => o.kind == "window_large");
            Assert.AreEqual("door", front.kind); Assert.AreEqual("door", back.kind);
            Assert.AreEqual(3, new[] { front.wall, back.wall, big.wall }.Distinct().Count(), "las 3 entradas están en muros distintos");
            Assert.GreaterOrEqual(ops.Count(o => o.kind == "door" && o.wall != "interior"), 2);
        }

        [Test]
        public void Doors_AreWideAndTallEnoughToWalkThrough()
        {
            foreach (var o in L().openings.Where(o => o.kind == "door"))
            {
                Assert.GreaterOrEqual(o.width, 0.9f, o.id);
                Assert.GreaterOrEqual(o.height, 2.0f, o.id);
                Assert.AreEqual(0f, o.sill, o.id);
            }
        }

        [Test]
        public void BigWindow_IsVaultable_BySillHeightAndClearance()
        {
            var big = L().openings.First(o => o.kind == "window_large");
            Assert.IsTrue(big.vaultable);
            var s = new MovementSettings();
            Assert.AreEqual(VaultVerdict.Vault, VaultMath.Classify(big.sill, big.height >= 1.1f, s), "el jugador la salta (antepecho 0.9 m, hueco libre de 1.3 m)");
        }

        [Test]
        public void SmallWindows_GiveLineOfFire_ButCannotBeVaulted()
        {
            var s = new MovementSettings();
            foreach (var o in L().openings.Where(o => o.kind == "window"))
            {
                Assert.GreaterOrEqual(o.sill, 1.0f, o.id + ": el antepecho protege el cuerpo");
                Assert.AreNotEqual(VaultVerdict.Vault, VaultMath.Classify(o.sill, o.height >= 1.1f, s), o.id + " no se debe poder saltar (hueco de " + o.height + " m)");
            }
        }

        [Test]
        public void EveryWindowAndDoor_HasAClearLineOfSight_ThroughWallsAndFrames()
        {
            var plan = P();
            foreach (var info in plan.Openings.Where(i => i.Opening.wall != "interior"))
            {
                float eye = info.Center.Y; // altura media del hueco
                V3 outside = new V3(info.Center.X + info.Normal.X * 3f, eye, info.Center.Z + info.Normal.Z * 3f);
                V3 inside = new V3(info.Center.X - info.Normal.X * 3f, eye, info.Center.Z - info.Normal.Z * 3f);
                foreach (var b in plan.Boxes.Where(b => b.Kind == HouseBoxKind.Wall || b.Kind == HouseBoxKind.Frame))
                    Assert.IsFalse(HousePlanner.SegmentHitsBox(outside, inside, b.Center, b.Size), info.Opening.id + ": un muro o marco tapa la línea de tiro (" + b.Tag + ")");
            }
        }

        [Test]
        public void SolidWalls_BlockBullets_ASegmentAcrossAWallPieceIsBlocked()
        {
            var plan = P();
            // un disparo horizontal a la altura de 1.5 m, atravesando el muro norte lejos de sus ventanas (x = 0), debe chocar
            V3 a = new V3(0f, 1.5f, 6f), b = new V3(0f, 1.5f, 1f);
            Assert.IsTrue(plan.Boxes.Any(w => w.Kind == HouseBoxKind.Wall && HousePlanner.SegmentHitsBox(a, b, w.Center, w.Size)));
        }

        [Test]
        public void WallPieces_PlusOpenings_CoverTheWholeWall()
        {
            var l = L();
            foreach (var wall in HouseTypes.Walls)
            {
                var ops = l.openings.Where(o => o.wall == wall).ToList();
                float len = WallPlanner.WallLength(l, wall), area = 0f;
                foreach (var p in WallPlanner.Segments(len, l.wallHeight, ops)) area += (p.T1 - p.T0) * (p.Y1 - p.Y0);
                foreach (var o in ops) area += o.width * o.height;
                Assert.AreEqual(len * l.wallHeight, area, 1e-3f, "área del muro " + wall);
            }
        }

        [Test]
        public void WallBoxes_DoNotOverlap()
        {
            var walls = P().Boxes.Where(b => b.Kind == HouseBoxKind.Wall).ToList();
            for (int i = 0; i < walls.Count; i++)
                for (int j = i + 1; j < walls.Count; j++)
                {
                    float ox = Overlap(walls[i].Center.X, walls[i].Size.X, walls[j].Center.X, walls[j].Size.X);
                    float oy = Overlap(walls[i].Center.Y, walls[i].Size.Y, walls[j].Center.Y, walls[j].Size.Y);
                    float oz = Overlap(walls[i].Center.Z, walls[i].Size.Z, walls[j].Center.Z, walls[j].Size.Z);
                    Assert.LessOrEqual(ox * oy * oz, 1e-4f, "muros " + i + " y " + j + " se solapan");
                }
        }

        [Test]
        public void TwoRooms_SeparatedByAnInteriorWallWithADoorway()
        {
            var l = L(); var plan = P();
            Assert.IsTrue(plan.Boxes.Any(b => b.Kind == HouseBoxKind.Wall && Math.Abs(b.Center.X - l.interiorX) < 0.01f), "muro interior");
            Assert.IsTrue(l.openings.Any(o => o.wall == "interior" && o.kind == "door"), "paso entre habitaciones");
            Assert.IsTrue(l.furniture.Any(f => f.x < l.interiorX) && l.furniture.Any(f => f.x > l.interiorX), "muebles en las dos habitaciones");
        }

        [Test]
        public void Roof_IsPartiallyCollapsed_WithThickSlabsBeamsAndFallenDebris()
        {
            var plan = P();
            var roof = plan.Boxes.Where(b => b.Kind == HouseBoxKind.Roof).ToList();
            var standing = roof.Where(b => b.Tag != "fallen").ToList();
            int grid = 5 * 4;
            Assert.That(standing.Count / (double)grid, Is.InRange(0.3, 0.9), "techo parcialmente caído");
            Assert.Less(standing.Count, grid);
            Assert.IsTrue(roof.Any(b => b.Tag == "fallen"), "losa caída en el piso");
            Assert.IsTrue(standing.Any(b => b.Tag == "tile_tilted"), "alguna losa inclinada");
            Assert.GreaterOrEqual(plan.Boxes.Count(b => b.Kind == HouseBoxKind.Beam), 3);
            foreach (var r in roof) Assert.GreaterOrEqual(r.Size.Y, 0.2f, "el techo tiene grosor real (sin backface culling)");
        }

        [Test]
        public void Roof_IsDeterministic_AndSeedChangesIt()
        {
            string K(HouseLayoutData l) => string.Join(";", HousePlanner.Plan(l).Boxes.Where(b => b.Kind == HouseBoxKind.Roof).Select(b => b.Tag + b.Center.X + b.Center.Z));
            Assert.AreEqual(K(L()), K(L()));
            var other = L(); other.roofSeed = 99;
            Assert.AreNotEqual(K(L()), K(other));
        }

        [Test]
        public void EveryBox_HasAnAxisAlignedBoxCollider_FloorWallsRoofFramesFurniture()
        {
            var plan = P();
            foreach (var kind in new[] { HouseBoxKind.Floor, HouseBoxKind.Wall, HouseBoxKind.Roof, HouseBoxKind.Frame, HouseBoxKind.Furniture, HouseBoxKind.Beam, HouseBoxKind.Barricade })
                Assert.IsTrue(plan.Boxes.Any(b => b.Kind == kind), "falta " + kind);
            foreach (var b in plan.Boxes)
            {
                Assert.IsTrue(b.Collider, b.Kind + " " + b.Tag + " sin collider");
                Assert.Greater(b.Size.X * b.Size.Y * b.Size.Z, 1e-5f, "caja degenerada");
            }
        }

        [Test]
        public void Furniture_IsCover_AndVaultableAccordingToItsHeight()
        {
            var f = P().Boxes.Where(b => b.Kind == HouseBoxKind.Furniture).ToList();
            Assert.GreaterOrEqual(f.Count, 6);
            foreach (var b in f)
            {
                Assert.IsTrue(b.Cover, b.Tag);
                bool expected = b.Size.Y >= 0.5f && b.Size.Y <= 1.3f;
                Assert.AreEqual(expected, b.Vaultable, b.Tag);
            }
            Assert.IsFalse(f.First(b => b.Tag == "furniture:shelf").Vaultable);
            Assert.IsTrue(f.First(b => b.Tag == "furniture:crate").Vaultable);
        }

        [Test]
        public void Furniture_DoesNotBlockTheDoorways()
        {
            var l = L(); var plan = P();
            foreach (var info in plan.Openings.Where(i => i.Opening.kind == "door" || i.Opening.kind == "window_large"))
            {
                // zona despejada: el ancho de la abertura + 0.6 m, y 1.1 m hacia adentro del muro
                bool alongX = Math.Abs(info.Normal.Z) > 0.5f;
                float cx = info.Center.X - info.Normal.X * 0.9f, cz = info.Center.Z - info.Normal.Z * 0.9f;
                float hx = alongX ? info.Opening.width / 2f + 0.3f : 0.6f, hz = alongX ? 0.6f : info.Opening.width / 2f + 0.3f;
                foreach (var b in plan.Boxes.Where(b => b.Kind == HouseBoxKind.Furniture))
                {
                    double a = b.Yaw * Math.PI / 180.0;
                    float fx = (float)(Math.Abs(Math.Cos(a)) * b.Size.X / 2 + Math.Abs(Math.Sin(a)) * b.Size.Z / 2), fz = (float)(Math.Abs(Math.Sin(a)) * b.Size.X / 2 + Math.Abs(Math.Cos(a)) * b.Size.Z / 2);
                    bool overlap = Math.Abs(b.Center.X - cx) < hx + fx && Math.Abs(b.Center.Z - cz) < hz + fz;
                    Assert.IsFalse(overlap, b.Tag + " bloquea " + info.Opening.id);
                }
            }
        }

        [Test]
        public void NavMesh_WallsAndFramesAreNotWalkable_RoofIsIgnored_DoorsHaveInfoForWalkableVolumes()
        {
            var plan = P();
            foreach (var b in plan.Boxes.Where(b => b.Kind == HouseBoxKind.Wall || b.Kind == HouseBoxKind.Frame)) Assert.AreEqual(NavFlag.NotWalkable, b.Nav);
            foreach (var b in plan.Boxes.Where(b => b.Kind == HouseBoxKind.Roof && b.Tag != "fallen")) Assert.AreEqual(NavFlag.Ignore, b.Nav);
            Assert.AreEqual(L().openings.Length, plan.Openings.Count, "cada abertura tiene su info (centro, normal) para el volumen transitable y el enlace");
            foreach (var info in plan.Openings) Assert.AreEqual(1f, info.Normal.Length, 1e-4f);
        }

        [Test]
        public void EachOpening_HasABarricade_ForTheDirectorToBlockIt()
        {
            var plan = P();
            foreach (var o in L().openings) Assert.IsTrue(plan.Boxes.Any(b => b.Kind == HouseBoxKind.Barricade && b.OpeningId == o.id), o.id);
        }

        [Test]
        public void Director_BlocksOpeningsByDay()
        {
            var o = new HouseOpening { id = "x", blockedDays = new[] { 2, 4 } };
            Assert.IsFalse(HouseTypes.IsBlocked(o, 1)); Assert.IsTrue(HouseTypes.IsBlocked(o, 2)); Assert.IsTrue(HouseTypes.IsBlocked(o, 4)); Assert.IsFalse(HouseTypes.IsBlocked(o, 7));
            Assert.IsFalse(HouseTypes.IsBlocked(null, 1));
            Assert.IsFalse(HouseTypes.IsBlocked(new HouseOpening(), 3), "sin lista: nunca bloqueada");
        }

        [Test]
        public void Validator_RejectsBrokenLayouts()
        {
            HouseLayoutData Make(Action<HouseLayoutData> mutate) { var l = L(); mutate(l); return l; }
            Assert.IsFalse(HouseValidator.Validate(Make(l => l.openings[0].wall = "techo")).Ok, "muro desconocido");
            Assert.IsFalse(HouseValidator.Validate(Make(l => l.openings[0].offset = 99f)).Ok, "fuera del muro");
            Assert.IsFalse(HouseValidator.Validate(Make(l => { l.openings[3].wall = "west"; l.openings[3].offset = 0.5f; })).Ok, "se solapan");
            Assert.IsFalse(HouseValidator.Validate(Make(l => l.openings[0].width = 0.4f)).Ok, "puerta muy angosta");
            Assert.IsFalse(HouseValidator.Validate(Make(l => l.width = 50f)).Ok, "casa gigante");
            Assert.IsFalse(HouseValidator.Validate(Make(l => l.yaw = 45f)).Ok, "yaw no múltiplo de 90");
            Assert.IsFalse(HouseValidator.Validate(Make(l => l.furniture[0].x = 40f)).Ok, "mueble fuera");
            Assert.IsFalse(HouseValidator.Validate(Make(l => l.openings[1].blockedDays = new[] { 9 })).Ok, "día inválido");
            Assert.IsFalse(HouseValidator.Validate(null).Ok);
        }

        [Test]
        public void Torches_AreInsideTheHouse_OrBesideTheFrontDoor()
        {
            var l = L();
            foreach (var t in l.torches) Assert.That(Math.Abs(t.x), Is.LessThanOrEqualTo(l.width / 2f + 0.8f));
            Assert.GreaterOrEqual(l.torches.Count(t => Math.Abs(t.x) < l.width / 2f && Math.Abs(t.z) < l.depth / 2f), 3, "antorchas en el interior");
        }

        private static float Overlap(float c1, float s1, float c2, float s2) => Math.Max(0f, Math.Min(c1 + s1 / 2f, c2 + s2 / 2f) - Math.Max(c1 - s1 / 2f, c2 - s2 / 2f));
    }
}

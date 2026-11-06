using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class GiantTests
    {
        [Test]
        public void OnlyTankCannotEnterBuildings_OthersCan()
        {
            foreach (var z in ZombieCatalog.Defaults())
                Assert.AreEqual(z.id != ZombieIds.Tank, z.canEnterBuildings, z.id);
            Assert.GreaterOrEqual(ZombieCatalog.Get(ZombieIds.Tank).navRadius * 2f, 1.4f, "no cabe por una puerta de 1.2 m");
        }

        [Test]
        public void Validator_RequiresBigRadiusForNoEntry()
        {
            var list = ZombieCatalog.Defaults();
            list.Find(z => z.id == ZombieIds.Tank).navRadius = 0.4f;
            Assert.IsFalse(ZombieValidator.Validate(list).Ok);
            list.Find(z => z.id == ZombieIds.Tank).navRadius = 0.8f;
            Assert.IsTrue(ZombieValidator.Validate(list).Ok);
        }

        [Test]
        public void Footprint_RotatesWithYawAndContains()
        {
            var f = Footprint.FromHouse(11f, 0.5f, 10f, 8f, 0f);
            Assert.IsTrue(f.Contains(11f, 0.5f)); Assert.IsFalse(f.Contains(17f, 0.5f)); Assert.IsTrue(f.Contains(15.9f, 0.5f));
            var r = Footprint.FromHouse(11f, 0.5f, 10f, 8f, 90f);
            Assert.IsTrue(r.Contains(11f, 4.9f)); Assert.IsFalse(r.Contains(15f, 0.5f));
        }

        [Test]
        public void PushOut_PutsPointOnNearestEdgeAndLeavesOutsidersAlone()
        {
            var f = new Footprint(0f, 0f, 5f, 4f);
            f.PushOut(4f, 0f, 0.5f, out float x, out float z);
            Assert.AreEqual(5.5f, x, 1e-4f); Assert.AreEqual(0f, z, 1e-4f);
            f.PushOut(0f, -3.5f, 0.5f, out x, out z);
            Assert.AreEqual(-4.5f, z, 1e-4f);
            f.PushOut(9f, 9f, 0.5f, out x, out z);
            Assert.AreEqual(9f, x); Assert.AreEqual(9f, z);
            f.PushOut(0f, 0f, 0.5f, out x, out z);
            Assert.IsFalse(f.Contains(x, z, 0.4f) && x == 0f && z == 0f, "el centro sale por algún borde");
        }

        [Test]
        public void Perimeter_PointsAreOutsideAndNearestIsClose()
        {
            var f = new Footprint(0f, 0f, 5f, 4f);
            for (int i = 0; i < 8; i++) { f.PerimeterPoint(i, 2f, out float px, out float pz); Assert.IsFalse(f.Contains(px, pz, 1.5f), "punto " + i); }
            f.PerimeterPoint(11, 2f, out float ax, out float az); f.PerimeterPoint(3, 2f, out float bx, out float bz);
            Assert.AreEqual(bx, ax); Assert.AreEqual(bz, az);
            int near = f.NearestPerimeter(12f, 0f, 2f);
            f.PerimeterPoint(near, 2f, out float nx, out _);
            Assert.AreEqual(7f, nx, 1e-4f);
        }

        [Test]
        public void Siege_ApproachesThenPoundsWithHysteresis()
        {
            var b = new SiegeBrain();
            Assert.AreEqual(SiegeAction.Approach, b.Tick(0.1f, 10f, false));
            Assert.AreEqual(SiegeAction.Pound, b.Tick(0.1f, 3.4f, true));
            Assert.AreEqual(SiegeAction.Pound, b.Tick(0.1f, 3.6f, true), "sigue golpeando en el borde: no vibra");
            Assert.AreEqual(SiegeAction.Pound, b.Tick(0.1f, 4.5f, true));
            Assert.AreNotEqual(SiegeAction.Pound, b.Tick(0.1f, 5.0f, true));
        }

        [Test]
        public void Siege_FlanksAfterWaitingAndHoldsTheFlank()
        {
            var b = new SiegeBrain();
            SiegeAction a = SiegeAction.Approach; int startIdx = b.FlankIndex;
            for (int i = 0; i < 100 && a != SiegeAction.Flank; i++) a = b.Tick(0.1f, 10f, true);
            Assert.AreEqual(SiegeAction.Flank, a);
            Assert.AreEqual(startIdx + 1, b.FlankIndex);
            for (int i = 0; i < 10; i++) Assert.AreEqual(SiegeAction.Flank, b.Tick(0.1f, 10f, false), "mientras camina al nuevo punto mantiene el rodeo");
            Assert.AreEqual(SiegeAction.Flank, b.Tick(0.1f, 10f, true), "llegó pero aún no pasó el tiempo mínimo");
            for (int i = 0; i < 40; i++) a = b.Tick(0.1f, 10f, true);
            Assert.AreEqual(SiegeAction.Wait, a);
        }

        [Test]
        public void Siege_NeverFlipsEveryFrame()
        {
            var b = new SiegeBrain();
            int changes = 0; var last = b.Action;
            var rng = new System.Random(2);
            for (int i = 0; i < 600; i++)
            {
                var a = b.Tick(0.016f, 3.5f + (float)(rng.NextDouble() - 0.5) * 0.4f, rng.Next(2) == 0);
                if (a != last) { changes++; last = a; }
            }
            Assert.LessOrEqual(changes, 3, "el ruido en la distancia no debe hacer vibrar la decisión");
        }
    }
}

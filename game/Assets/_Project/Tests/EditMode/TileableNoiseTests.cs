using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class TileableNoiseTests
    {
        [Test]
        public void Perlin_IsDeterministic_AndDependsOnSeed()
        {
            float a = TileableNoise.Perlin(3.37f, 5.91f, 16, 42);
            Assert.AreEqual(a, TileableNoise.Perlin(3.37f, 5.91f, 16, 42));
            Assert.AreNotEqual(a, TileableNoise.Perlin(3.37f, 5.91f, 16, 43));
        }

        [Test]
        public void Perlin_StaysInRange()
        {
            for (int i = 0; i < 2000; i++)
            {
                float v = TileableNoise.Perlin(i * 0.173f, i * 0.291f, 64, 7);
                Assert.That(v, Is.InRange(-1f, 1f));
            }
        }

        [Test]
        public void Perlin_RepeatsAfterOnePeriod()
        {
            const int period = 12;
            for (int i = 0; i < 200; i++)
            {
                float x = i * 0.137f % period, y = i * 0.211f % period;
                float a = TileableNoise.Perlin(x, y, period, 5);
                Assert.AreEqual(a, TileableNoise.Perlin(x + period, y, period, 5), 1e-4f, "eje X");
                Assert.AreEqual(a, TileableNoise.Perlin(x, y + period, period, 5), 1e-4f, "eje Y");
            }
        }

        [Test]
        public void Fbm_IsTileableAndInUnitRange()
        {
            for (int i = 0; i < 200; i++)
            {
                float u = i * 0.0047f, v = i * 0.0031f;
                float a = TileableNoise.Fbm(u, v, 4, 4, 9);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.AreEqual(a, TileableNoise.Fbm(u + 1f, v, 4, 4, 9), 1e-4f);
                Assert.AreEqual(a, TileableNoise.Fbm(u, v + 1f, 4, 4, 9), 1e-4f);
            }
        }

        [Test]
        public void Voronoi_IsTileable_AndNearestIsNotFartherThanSecond()
        {
            for (int i = 0; i < 200; i++)
            {
                float u = i * 0.0049f, v = i * 0.0037f;
                TileableNoise.Voronoi(u, v, 6, 3, out float a1, out float a2, out float aid);
                TileableNoise.Voronoi(u + 1f, v + 1f, 6, 3, out float b1, out float b2, out float bid);
                Assert.LessOrEqual(a1, a2);
                Assert.AreEqual(a1, b1, 1e-3f);
                Assert.AreEqual(a2, b2, 1e-3f);
                Assert.AreEqual(aid, bid);
            }
        }

        [Test]
        public void Mod_HandlesNegatives()
        {
            Assert.AreEqual(3, TileableNoise.Mod(-1, 4));
            Assert.AreEqual(0, TileableNoise.Mod(8, 4));
        }
    }
}

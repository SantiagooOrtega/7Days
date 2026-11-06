using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class SurfaceGeneratorTests
    {
        private const int N = 96; // pequeño: las pruebas deben ser rápidas

        private static readonly SurfaceKind[] AllKinds =
            { SurfaceKind.Asphalt, SurfaceKind.Wall, SurfaceKind.Concrete, SurfaceKind.Metal };

        private static SurfaceParams Params() => new SurfaceParams { Size = N };

        [TestCaseSource(nameof(AllKinds))]
        public void Generate_ProducesFullSizeOpaqueMaps(SurfaceKind kind)
        {
            var r = SurfaceGenerator.Generate(kind, Params());
            Assert.AreEqual(N, r.Size);
            Assert.AreEqual(N * N * 4, r.Diffuse.Length);
            Assert.AreEqual(N * N * 4, r.Normal.Length);
            Assert.AreEqual(N * N, r.Height.Length);
            for (int i = 3; i < r.Diffuse.Length; i += 4)
            {
                Assert.AreEqual(255, r.Diffuse[i]);
                Assert.AreEqual(255, r.Normal[i]);
            }
        }

        [TestCaseSource(nameof(AllKinds))]
        public void Generate_IsDeterministic_AndSeedChangesResult(SurfaceKind kind)
        {
            var a = SurfaceGenerator.Generate(kind, Params());
            var b = SurfaceGenerator.Generate(kind, Params());
            CollectionAssert.AreEqual(a.Diffuse, b.Diffuse);
            CollectionAssert.AreEqual(a.Normal, b.Normal);

            var other = Params();
            other.Seed = 99;
            CollectionAssert.AreNotEqual(a.Diffuse, SurfaceGenerator.Generate(kind, other).Diffuse);
        }

        [TestCaseSource(nameof(AllKinds))]
        public void Palette_IsDarkAndWorn(SurfaceKind kind)
        {
            var r = SurfaceGenerator.Generate(kind, Params());
            double luma = 0;
            for (int i = 0; i < r.Diffuse.Length; i += 4)
                luma += (0.299 * r.Diffuse[i] + 0.587 * r.Diffuse[i + 1] + 0.114 * r.Diffuse[i + 2]) / 255.0;
            luma /= N * N;
            Assert.That(luma, Is.InRange(0.08, 0.45), "luminancia media de " + kind);
        }

        [TestCaseSource(nameof(AllKinds))]
        public void NormalMap_VectorsAreUnitLengthAndPointOutwards(SurfaceKind kind)
        {
            var r = SurfaceGenerator.Generate(kind, Params());
            for (int i = 0; i < r.Normal.Length; i += 4)
            {
                double x = r.Normal[i] / 255.0 * 2 - 1, y = r.Normal[i + 1] / 255.0 * 2 - 1, z = r.Normal[i + 2] / 255.0 * 2 - 1;
                Assert.AreEqual(1.0, Math.Sqrt(x * x + y * y + z * z), 0.03);
                Assert.Greater(z, 0.0, "la normal debe apuntar hacia afuera de la superficie");
            }
        }

        [TestCaseSource(nameof(AllKinds))]
        public void Texture_TilesWithoutVisibleSeam(SurfaceKind kind)
        {
            var r = SurfaceGenerator.Generate(kind, Params());
            double seam = 0, inner = 0;
            for (int y = 0; y < N; y++)
            {
                seam += Diff(r.Diffuse, y * N + N - 1, y * N);       // último y primer píxel de la fila (se tocan al repetir)
                inner += Diff(r.Diffuse, y * N + N / 2, y * N + N / 2 + 1);
            }
            for (int x = 0; x < N; x++)
            {
                seam += Diff(r.Diffuse, (N - 1) * N + x, x);
                inner += Diff(r.Diffuse, (N / 2) * N + x, (N / 2 + 1) * N + x);
            }
            // La diferencia en la costura debe ser del mismo orden que entre píxeles vecinos normales.
            Assert.LessOrEqual(seam, inner * 3.0 + 1.0, "costura visible en " + kind);
        }

        [Test]
        public void MoreRust_MakesMetalMoreOrange()
        {
            var clean = Params();
            clean.RustAmount = 0f;
            var rusty = Params();
            rusty.RustAmount = 1f;
            Assert.Greater(Redness(SurfaceGenerator.Generate(SurfaceKind.Metal, rusty)),
                Redness(SurfaceGenerator.Generate(SurfaceKind.Metal, clean)) + 10.0);
        }

        [Test]
        public void MoreCracks_MakesConcreteHaveMoreDarkPixels()
        {
            var none = Params();
            none.CrackAmount = 0f;
            var many = Params();
            many.CrackAmount = 1f;
            Assert.Greater(DarkPixels(SurfaceGenerator.Generate(SurfaceKind.Concrete, many)),
                DarkPixels(SurfaceGenerator.Generate(SurfaceKind.Concrete, none)));
        }

        [Test]
        public void Cracks_AreZeroWhenAmountIsZero_AndBounded()
        {
            for (int i = 0; i < 300; i++)
            {
                float u = i * 0.0033f, v = i * 0.0021f;
                Assert.AreEqual(0f, SurfaceGenerator.Cracks(u, v, 0f, 1));
                Assert.That(SurfaceGenerator.Cracks(u, v, 1f, 1), Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Clone_DoesNotShareState()
        {
            var a = Params();
            var b = a.Clone();
            b.RustAmount = 0.99f;
            Assert.AreNotEqual(a.RustAmount, b.RustAmount);
        }

        private static double Diff(byte[] d, int pa, int pb)
        {
            double s = 0;
            for (int c = 0; c < 3; c++) s += Math.Abs(d[pa * 4 + c] - d[pb * 4 + c]);
            return s;
        }

        private static double Redness(SurfaceResult r)
        {
            double s = 0;
            for (int i = 0; i < r.Diffuse.Length; i += 4) s += r.Diffuse[i] - r.Diffuse[i + 2]; // R - B
            return s / (N * N);
        }

        private static int DarkPixels(SurfaceResult r)
        {
            int n = 0;
            for (int i = 0; i < r.Diffuse.Length; i += 4)
                if (r.Diffuse[i] < 40 && r.Diffuse[i + 1] < 40 && r.Diffuse[i + 2] < 40) n++;
            return n;
        }
    }
}

using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class DecalGeneratorTests
    {
        private const int N = 96;
        private static readonly DecalKind[] Kinds = { DecalKind.Blood, DecalKind.Crack, DecalKind.Puddle, DecalKind.DashLine, DecalKind.Zebra, DecalKind.Arrow, DecalKind.Stain, DecalKind.Burn };
        private static readonly DecalKind[] DarkKinds = { DecalKind.Blood, DecalKind.Crack, DecalKind.Puddle, DecalKind.Stain, DecalKind.Burn };
        private static readonly DecalKind[] RoadKinds = { DecalKind.DashLine, DecalKind.Zebra, DecalKind.Arrow };

        [TestCaseSource(nameof(Kinds))]
        public void Generate_HasRightSize_AndIsDeterministic(DecalKind kind)
        {
            var a = DecalGenerator.Generate(kind, N, 5);
            Assert.AreEqual(N * N * 4, a.Length);
            CollectionAssert.AreEqual(a, DecalGenerator.Generate(kind, N, 5));
            CollectionAssert.AreNotEqual(a, DecalGenerator.Generate(kind, N, 6));
        }

        [TestCaseSource(nameof(Kinds))]
        public void Border_IsFullyTransparent_SoQuadEdgesNeverShow(DecalKind kind)
        {
            var px = DecalGenerator.Generate(kind, N, 11);
            for (int i = 0; i < N; i++)
            {
                Assert.AreEqual(0, px[(i) * 4 + 3], "borde inferior");
                Assert.AreEqual(0, px[((N - 1) * N + i) * 4 + 3], "borde superior");
                Assert.AreEqual(0, px[(i * N) * 4 + 3], "borde izquierdo");
                Assert.AreEqual(0, px[(i * N + N - 1) * 4 + 3], "borde derecho");
            }
        }

        [TestCaseSource(nameof(Kinds))]
        public void Coverage_IsPartial_NotEmptyNorFull(DecalKind kind)
        {
            var px = DecalGenerator.Generate(kind, N, 3);
            int visible = 0;
            for (int i = 3; i < px.Length; i += 4) if (px[i] > 40) visible++;
            double frac = visible / (double)(N * N);
            Assert.That(frac, Is.InRange(0.01, 0.6), "cobertura de " + kind);
        }

        [TestCaseSource(nameof(DarkKinds))]
        public void VisiblePixels_AreDark(DecalKind kind)
        {
            // sangre seca, grietas y charcos: nada brillante
            var px = DecalGenerator.Generate(kind, N, 3);
            double sum = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i + 3] < 100) continue;
                sum += (px[i] + px[i + 1] + px[i + 2]) / (3.0 * 255.0);
                n++;
            }
            Assert.Greater(n, 0);
            Assert.Less(sum / n, 0.25);
        }

        [TestCaseSource(nameof(RoadKinds))]
        public void RoadMarkings_ArePaintedWhiteOrYellow_AndWorn(DecalKind kind)
        {
            var px = DecalGenerator.Generate(kind, N, 3);
            double lum = 0; int n = 0, full = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i + 3] < 60) continue;
                lum += (px[i] + px[i + 1] + px[i + 2]) / (3.0 * 255.0); n++;
                if (px[i + 3] > 220) full++;
            }
            Assert.Greater(n, 0);
            Assert.Greater(lum / n, 0.25, "pintura clara");
            Assert.Less(lum / n, 0.8, "pintura gastada, no blanco puro");
            Assert.Less(full / (double)n, 0.9, "tiene zonas desvanecidas");
        }

        [Test]
        public void DashLine_IsYellow_ZebraAndArrowAreWhite()
        {
            double Warmth(DecalKind k)
            {
                var px = DecalGenerator.Generate(k, N, 4);
                double d = 0; int n = 0;
                for (int i = 0; i < px.Length; i += 4) { if (px[i + 3] < 100) continue; d += px[i] - px[i + 2]; n++; }
                return d / n;
            }
            Assert.Greater(Warmth(DecalKind.DashLine), 60.0);
            Assert.Less(Warmth(DecalKind.Zebra), 25.0);
            Assert.Less(Warmth(DecalKind.Arrow), 25.0);
        }

        [Test]
        public void Arrow_IsMoreFadedThanZebra()
        {
            double Alpha(DecalKind k)
            {
                var px = DecalGenerator.Generate(k, N, 4);
                double a = 0; int n = 0;
                for (int i = 3; i < px.Length; i += 4) if (px[i] > 5) { a += px[i]; n++; }
                return a / Math.Max(1, n);
            }
            Assert.Less(Alpha(DecalKind.Arrow), Alpha(DecalKind.Zebra) + 5);
        }

        [Test]
        public void Blood_IsReddish_Puddle_IsNeutralBlueGray()
        {
            double R(DecalKind k)
            {
                var px = DecalGenerator.Generate(k, N, 4);
                double d = 0; int n = 0;
                for (int i = 0; i < px.Length; i += 4) { if (px[i + 3] < 100) continue; d += px[i] - px[i + 2]; n++; }
                return d / n;
            }
            Assert.Greater(R(DecalKind.Blood), 10.0, "la sangre tiene más rojo que azul");
            Assert.Less(R(DecalKind.Puddle), 0.0, "el charco es gris azulado");
        }
    }
}

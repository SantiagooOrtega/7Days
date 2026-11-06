using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class GroundGeneratorTests
    {
        private static GroundParams P(int size = 128) => new GroundParams { Size = size };

        [Test]
        public void Weights_SumToOne_AndAreNonNegative()
        {
            var p = P();
            for (int i = 0; i < 400; i++)
            {
                var w = GroundGenerator.Weights(p, -28f + i * 0.14f, 25f - i * 0.12f);
                float sum = 0f;
                foreach (var x in w) { Assert.GreaterOrEqual(x, 0f); sum += x; }
                Assert.AreEqual(1f, sum, 1e-4f);
            }
        }

        [Test]
        public void Generate_HasRightSize_IsDeterministic_AndSeedChangesIt()
        {
            var a = GroundGenerator.Generate(P());
            Assert.AreEqual(128 * 128 * 4, a.Diffuse.Length);
            CollectionAssert.AreEqual(a.Diffuse, GroundGenerator.Generate(P()).Diffuse);
            var other = P(); other.Seed = 99;
            CollectionAssert.AreNotEqual(a.Diffuse, GroundGenerator.Generate(other).Diffuse);
        }

        [Test]
        public void Zones_AreLarge_NeighboursAgree_FarPointsDiffer()
        {
            // "zonas grandes, no ruido uniforme": puntos a 0.5 m casi siempre tienen la misma capa dominante; a 20 m, a menudo no
            var p = P();
            int agreeNear = 0, agreeFar = 0, total = 0;
            for (int i = 0; i < 300; i++)
            {
                float x = 6f + (i % 20) * 1.1f, z = -26f + (i / 20) * 3.4f;
                int a = Dominant(GroundGenerator.Weights(p, x, z));
                int near = Dominant(GroundGenerator.Weights(p, x + 0.5f, z));
                int far = Dominant(GroundGenerator.Weights(p, -(x + 14f) % 26f - 4f, z));
                if (a == near) agreeNear++;
                if (a == far) agreeFar++;
                total++;
            }
            Assert.Greater(agreeNear / (double)total, 0.9, "las zonas son grandes");
            Assert.Less(agreeFar / (double)total, 0.6, "pero cambian al alejarse");
        }

        [Test]
        public void ManyLayers_AppearAcrossTheMap()
        {
            var p = P();
            var seen = new bool[GroundGenerator.LayerCount];
            for (float x = -29f; x <= 29f; x += 0.7f)
                for (float z = -29f; z <= 29f; z += 0.7f)
                    if (Math.Abs(x) > 5f) seen[Dominant(GroundGenerator.Weights(p, x, z))] = true;
            for (int l = 0; l < seen.Length; l++) Assert.IsTrue(seen[l], "la capa " + (GroundLayer)l + " nunca domina");
        }

        [Test]
        public void NearTheRoad_HasMoreRubbleAndGravel_ThanFarAway()
        {
            var p = P();
            double Debris(float xMin, float xMax)
            {
                double s = 0; int n = 0;
                for (float x = xMin; x <= xMax; x += 0.4f)
                    for (float z = -28f; z <= 28f; z += 0.9f)
                    {
                        var w = GroundGenerator.Weights(p, x, z);
                        s += w[(int)GroundLayer.Concrete] + w[(int)GroundLayer.Gravel]; n++;
                    }
                return s / n;
            }
            Assert.Greater(Debris(4.5f, 9f), Debris(20f, 28f) * 1.3, "más escombro y grava junto al borde de la vía");
        }

        [Test]
        public void RoadDebrisParameter_ControlsTheNearRoadBias()
        {
            var none = P(); none.RoadDebris = 0f;
            var lots = P(); lots.RoadDebris = 2f;
            double Debris(GroundParams p)
            {
                double s = 0; int n = 0;
                for (float x = 4.5f; x <= 8f; x += 0.5f) for (float z = -28f; z <= 28f; z += 1f)
                { var w = GroundGenerator.Weights(p, x, z); s += w[(int)GroundLayer.Concrete] + w[(int)GroundLayer.Gravel]; n++; }
                return s / n;
            }
            Assert.Greater(Debris(lots), Debris(none));
        }

        [Test]
        public void LayerAbundance_Parameters_Work()
        {
            double Share(GroundParams p, GroundLayer l)
            {
                double s = 0; int n = 0;
                for (float x = -28f; x <= 28f; x += 1.2f) for (float z = -28f; z <= 28f; z += 1.2f)
                { if (Math.Abs(x) < 5f) continue; s += GroundGenerator.Weights(p, x, z)[(int)l]; n++; }
                return s / n;
            }
            var few = P(); few.Mud = 0f; var many = P(); many.Mud = 2f;
            Assert.AreEqual(0.0, Share(few, GroundLayer.Mud), 1e-6);
            Assert.Greater(Share(many, GroundLayer.Mud), Share(P(), GroundLayer.Mud));
        }

        [Test]
        public void Smoothness_IsHighOnMud_AndLowOnDryLayers()
        {
            var r = GroundGenerator.Generate(P(192));
            double mud = 0, dry = 0; int nm = 0, nd = 0;
            var p = P(192);
            for (int y = 0; y < 192; y++) for (int x = 0; x < 192; x++)
            {
                float wx = (x + .5f) / 192 * 60f - 30f, wz = (y + .5f) / 192 * 60f - 30f;
                var w = GroundGenerator.Weights(p, wx, wz);
                int d = Dominant(w);
                int a = r.Diffuse[(y * 192 + x) * 4 + 3];
                if (d == (int)GroundLayer.Mud && w[d] > 0.9f) { mud += a; nm++; }
                else if (d == (int)GroundLayer.Earth && w[d] > 0.9f) { dry += a; nd++; }
            }
            Assert.Greater(nm, 20); Assert.Greater(nd, 20);
            Assert.Greater(mud / nm, 120, "el barro mojado brilla (alfa = suavidad)");
            Assert.Less(dry / nd, 60, "la tierra seca es mate");
        }

        [Test]
        public void NormalMap_HasRelief_AndGrowsWithPebblesAndCracks()
        {
            double Relief(SurfaceResult r) { double s = 0; for (int i = 0; i < r.Normal.Length; i += 4) { double x = r.Normal[i] / 255.0 - .5, y = r.Normal[i + 1] / 255.0 - .5; s += x * x + y * y; } return s / (r.Size * r.Size); }
            var flat = P(); flat.Pebbles = 0f; flat.Cracks = 0f; flat.Ruts = 0f; flat.Footprints = 0f;
            var rough = P(); rough.Pebbles = 1.5f; rough.Cracks = 1.5f;
            Assert.Greater(Relief(GroundGenerator.Generate(rough)), Relief(GroundGenerator.Generate(flat)));
            Assert.Greater(Relief(GroundGenerator.Generate(P())), 1e-4, "hay relieve");
        }

        [Test]
        public void NormalVectors_AreUnitAndPointUp()
        {
            var r = GroundGenerator.Generate(P());
            for (int i = 0; i < r.Normal.Length; i += 4)
            {
                double x = r.Normal[i] / 255.0 * 2 - 1, y = r.Normal[i + 1] / 255.0 * 2 - 1, z = r.Normal[i + 2] / 255.0 * 2 - 1;
                Assert.AreEqual(1.0, Math.Sqrt(x * x + y * y + z * z), 0.03);
                Assert.Greater(z, 0.0);
            }
        }

        [Test]
        public void Ruts_CarveTroughsAlongTheRoadShoulder()
        {
            var with = P(256); var without = P(256); without.Ruts = 0f; without.Footprints = 0f; with.Footprints = 0f;
            var a = GroundGenerator.Generate(with); var b = GroundGenerator.Generate(without);
            // la altura media a lo largo de la rodada (x ~ 5.5 m) es menor con rodadas
            double H(SurfaceResult r) { double s = 0; int n = 0; for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++) { float wx = (x + .5f) / 256 * 60f - 30f; if (wx > 5.2f && wx < 5.9f) { s += r.Height[y * 256 + x]; n++; } } return s / n; }
            Assert.Less(H(a), H(b), "hay rodadas");
        }

        [Test]
        public void Footprints_StampDepressions()
        {
            var with = P(256); var without = P(256); without.Footprints = 0f; with.Ruts = 0f; without.Ruts = 0f;
            var a = GroundGenerator.Generate(with); var b = GroundGenerator.Generate(without);
            int lowered = 0; for (int i = 0; i < a.Height.Length; i++) if (a.Height[i] < b.Height[i] - 0.05f) lowered++;
            Assert.Greater(lowered, 30, "huellas hundidas en el piso");
        }

        [Test]
        public void Map_DoesNotRepeat_TwoFarPatchesDiffer()
        {
            var r = GroundGenerator.Generate(P(256));
            double diff = 0; int n = 0;
            for (int y = 20; y < 84; y++) for (int x = 20; x < 84; x++)
            { int a = (y * 256 + x) * 4, b = (y * 256 + x + 128) * 4; diff += Math.Abs(r.Diffuse[a] - r.Diffuse[b]) + Math.Abs(r.Diffuse[a + 1] - r.Diffuse[b + 1]); n++; }
            Assert.Greater(diff / n, 8.0, "dos zonas lejanas no son copia una de otra");
        }

        [Test]
        public void Brightness_ScalesTheAlbedo()
        {
            var dim = P(); dim.Brightness = 0.6f; var bright = P(); bright.Brightness = 1.4f;
            double M(SurfaceResult r) { double s = 0; for (int i = 0; i < r.Diffuse.Length; i += 4) s += r.Diffuse[i] + r.Diffuse[i + 1] + r.Diffuse[i + 2]; return s; }
            Assert.Greater(M(GroundGenerator.Generate(bright)), M(GroundGenerator.Generate(dim)) * 1.5);
        }

        [Test]
        public void StripGeneration_EqualsFullGeneration()
        {
            var p = P(96);
            var full = GroundGenerator.Generate(p);
            var b = new GroundBuffers(96);
            GroundGenerator.FillRows(p, b, 48, 96); GroundGenerator.FillRows(p, b, 0, 48); // orden distinto, como harían varios hilos
            var strips = GroundGenerator.Finish(p, b);
            CollectionAssert.AreEqual(full.Diffuse, strips.Diffuse);
        }

        [Test]
        public void DetailTexture_IsMidGray_Tileable_AndHasUnitNormals()
        {
            var d = GroundDetail.Generate(96, 3);
            double mean = 0; for (int i = 0; i < d.Diffuse.Length; i += 4) mean += d.Diffuse[i] / 255.0; mean /= 96 * 96;
            Assert.That(mean, Is.InRange(0.4, 0.62), "gris medio: el modo Detail x2 de URP multiplica por 2 y debe quedar neutro");
            double seam = 0, inner = 0;
            for (int y = 0; y < 96; y++) { seam += Math.Abs(d.Diffuse[(y * 96 + 95) * 4] - d.Diffuse[(y * 96) * 4]); inner += Math.Abs(d.Diffuse[(y * 96 + 48) * 4] - d.Diffuse[(y * 96 + 49) * 4]); }
            Assert.LessOrEqual(seam, inner * 3 + 1);
            for (int i = 0; i < d.Normal.Length; i += 4)
            {
                double x = d.Normal[i] / 255.0 * 2 - 1, y = d.Normal[i + 1] / 255.0 * 2 - 1, z = d.Normal[i + 2] / 255.0 * 2 - 1;
                Assert.AreEqual(1.0, Math.Sqrt(x * x + y * y + z * z), 0.03);
            }
        }

        private static int Dominant(float[] w) { int b = 0; for (int i = 1; i < w.Length; i++) if (w[i] > w[b]) b = i; return b; }
    }
}

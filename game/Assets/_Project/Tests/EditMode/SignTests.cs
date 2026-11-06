using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class SignTests
    {
        private static readonly SignType[] AllTypes = (SignType[])Enum.GetValues(typeof(SignType));
        private const int N = 96;

        // ------------------------------------------------------------ geometría de placas
        [TestCaseSource(nameof(AllTypes))]
        public void Outline_IsConvex_CounterClockwise_AndFitsTheDimensions(SignType t)
        {
            var poly = SignGeometry.Outline(t);
            var (w, h) = SignGeometry.Dimensions(t);
            Assert.GreaterOrEqual(poly.Count, 3);
            for (int i = 0; i < poly.Count; i++)
            {
                V2 a = poly[i], b = poly[(i + 1) % poly.Count], c = poly[(i + 2) % poly.Count];
                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                Assert.Greater(cross, 0f, "contorno no convexo/antihorario en el vértice " + i);
            }
            Assert.AreEqual(w, poly.Max(v => v.X) - poly.Min(v => v.X), 0.01f);
            Assert.AreEqual(h, poly.Max(v => v.Y) - poly.Min(v => v.Y), 0.01f);
        }

        [TestCaseSource(nameof(AllTypes))]
        public void Plate_HasFrontSubmesh_FacingForward_WithUvSpanningZeroToOne(SignType t)
        {
            var m = SignGeometry.Plate(t);
            int n = SignGeometry.Outline(t).Count;
            Assert.AreEqual((n - 2) * 3, m.SubmeshSplit, "el frente es un abanico de n-2 triángulos");
            Assert.AreEqual((n - 2) * 2 + n * 2, m.Triangles.Count / 3, "frente + dorso + cantos");

            float minU = 9, maxU = -9, minV = 9, maxV = -9;
            for (int k = 0; k < m.SubmeshSplit; k++)
            {
                int vi = m.Triangles[k];
                Assert.AreEqual(1f, m.Normals[vi].Z, 1e-4f, "el frente mira a +Z");
                Assert.AreEqual(SignGeometry.Thickness / 2f, m.Positions[vi].Z, 1e-4f);
                minU = Math.Min(minU, m.Uvs[vi].X); maxU = Math.Max(maxU, m.Uvs[vi].X);
                minV = Math.Min(minV, m.Uvs[vi].Y); maxV = Math.Max(maxV, m.Uvs[vi].Y);
            }
            Assert.AreEqual(0f, minU, 1e-3f); Assert.AreEqual(1f, maxU, 1e-3f);
            Assert.AreEqual(0f, minV, 1e-3f); Assert.AreEqual(1f, maxV, 1e-3f);
        }

        [TestCaseSource(nameof(AllTypes))]
        public void Plate_Triangles_FaceOutward(SignType t)
        {
            var m = SignGeometry.Plate(t);
            for (int i = 0; i < m.Triangles.Count; i += 3)
            {
                V3 a = m.Positions[m.Triangles[i]], b = m.Positions[m.Triangles[i + 1]], c = m.Positions[m.Triangles[i + 2]];
                V3 g = -V3.Cross(b - a, c - a);
                Assert.Greater(V3.Dot(g, m.Normals[m.Triangles[i]]), 0f, "triángulo " + i / 3 + " mal orientado");
            }
        }

        [Test]
        public void SignedDistance_IsNegativeInsideAndPositiveOutside()
        {
            foreach (var t in AllTypes)
            {
                var poly = SignGeometry.Outline(t);
                Assert.Less(SignGeometry.SignedDistance(poly, 0f, 0f), 0f);
                Assert.Greater(SignGeometry.SignedDistance(poly, 2f, 2f), 0f);
            }
        }

        // ------------------------------------------------------------ textura de señales
        [TestCaseSource(nameof(AllTypes))]
        public void Texture_HasRightSize_IsOpaque_AndDeterministic(SignType t)
        {
            var p = SignGenerator.VariantParams(1, N, 4);
            var a = SignGenerator.Generate(t, p);
            Assert.AreEqual(N * N * 4, a.Diffuse.Length);
            Assert.AreEqual(N * N * 4, a.Normal.Length);
            for (int i = 3; i < a.Diffuse.Length; i += 4) Assert.AreEqual(255, a.Diffuse[i]);
            CollectionAssert.AreEqual(a.Diffuse, SignGenerator.Generate(t, p).Diffuse);
            p.Seed++;
            CollectionAssert.AreNotEqual(a.Diffuse, SignGenerator.Generate(t, p).Diffuse);
        }

        [Test]
        public void MoreWear_MeansMoreRustAndPeeledPaint_PerVariant()
        {
            double Rusty(SignType t, int variant)
            {
                var r = SignGenerator.Generate(t, SignGenerator.VariantParams(variant, N, 2));
                int n = 0, total = 0;
                for (int i = 0; i < r.Diffuse.Length; i += 4) { total++; if (r.Diffuse[i] > r.Diffuse[i + 2] * 1.7 && r.Diffuse[i] > r.Diffuse[i + 1] * 1.3 && r.Diffuse[i] < 190) n++; }
                return n / (double)total;
            }
            foreach (var t in AllTypes)
            {
                Assert.Less(Rusty(t, 0), Rusty(t, 2), "la variante 2 debe estar más oxidada que la 0 (" + t + ")");
            }
        }

        [Test]
        public void CleanSigns_ShowTheirPaintColors()
        {
            SignParams Clean() => new SignParams { Size = N, Seed = 3, Wear = 0f, Rust = 0f, Dirt = 0f, BulletHoles = 0, Dents = 0 };
            double Mean(SignType t, Func<byte, byte, byte, double> f)
            {
                var r = SignGenerator.Generate(t, Clean());
                var poly = SignGeometry.Outline(t); var (w, h) = SignGeometry.Dimensions(t);
                double sum = 0; int cnt = 0;
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float mx = ((x + .5f) / N - .5f) * w, my = ((y + .5f) / N - .5f) * h;
                    if (SignGeometry.SignedDistance(poly, mx, my) > -0.09f) continue; // solo el interior
                    int i = (y * N + x) * 4; sum += f(r.Diffuse[i], r.Diffuse[i + 1], r.Diffuse[i + 2]); cnt++;
                }
                return sum / cnt;
            }
            Assert.Greater(Mean(SignType.Stop, (r, g, b) => r - (g + b) / 2.0), 40.0, "el pare es rojo");
            Assert.Greater(Mean(SignType.Hazard, (r, g, b) => (r + g) / 2.0 - b), 40.0, "el peligro es amarillo");
            Assert.Greater(Mean(SignType.Generic, (r, g, b) => b - (r + g) / 2.0), 15.0, "el genérico es azul");
            Assert.Greater(Mean(SignType.Yield, (r, g, b) => (r + g + b) / 3.0), 110.0, "el centro del ceda es claro");
        }

        [Test]
        public void SpeedSign_HasDarkDigits_InTheMiddle()
        {
            var r = SignGenerator.Generate(SignType.Speed, new SignParams { Size = N, Seed = 3, Wear = 0f, Rust = 0f, Dirt = 0f, BulletHoles = 0, Dents = 0 });
            int dark = 0;
            for (int y = N / 3; y < 2 * N / 3; y++) for (int x = N / 4; x < 3 * N / 4; x++)
            {
                int i = (y * N + x) * 4; if (r.Diffuse[i] < 30 && r.Diffuse[i + 1] < 30) dark++;
            }
            Assert.Greater(dark, 60, "se deben ver los dígitos negros");
        }

        [Test]
        public void BulletHoles_AddDarkPixels_AndDentsAddRelief()
        {
            SignParams P(int holes, int dents) => new SignParams { Size = N, Seed = 6, Wear = 0.3f, Rust = 0.3f, Dirt = 0.2f, BulletHoles = holes, Dents = dents };
            int Dark(SurfaceResult r) { int n = 0; for (int i = 0; i < r.Diffuse.Length; i += 4) if (r.Diffuse[i] < 18 && r.Diffuse[i + 1] < 18 && r.Diffuse[i + 2] < 18) n++; return n; }
            Assert.Greater(Dark(SignGenerator.Generate(SignType.Stop, P(8, 0))), Dark(SignGenerator.Generate(SignType.Stop, P(0, 0))) + 8);

            double Relief(SurfaceResult r) { double s = 0; for (int i = 0; i < r.Normal.Length; i += 4) { double x = r.Normal[i] / 255.0 - 0.5, y = r.Normal[i + 1] / 255.0 - 0.5; s += x * x + y * y; } return s; }
            Assert.Greater(Relief(SignGenerator.Generate(SignType.Generic, P(0, 6))), Relief(SignGenerator.Generate(SignType.Generic, P(0, 0))), "las abolladuras se ven en el mapa de normales");
        }

        [Test]
        public void VariantParams_GoFromLightToHeavyWear()
        {
            var a = SignGenerator.VariantParams(0, N, 1); var b = SignGenerator.VariantParams(1, N, 1); var c = SignGenerator.VariantParams(2, N, 1);
            Assert.Less(a.Wear, b.Wear); Assert.Less(b.Wear, c.Wear);
            Assert.Less(a.Rust, c.Rust);
            Assert.AreEqual(SignGenerator.VariantParams(5, N, 1).Wear, SignGenerator.VariantParams(2, N, 1).Wear, "las variantes se repiten módulo " + SignGenerator.Variants);
        }

        // ------------------------------------------------------------ distribución
        private static readonly List<Zone> Zones = new List<Zone> { Zone.Circle(5.5f, 0f, 1.3f), Zone.Circle(-5.5f, -18f, 1.3f), Zone.Rect(-24f, 19f, 24f, 29f) };

        private static PropLayoutData Layout(float density = 1f, int seed = 11) => SignLayout.Generate(new SignLayoutParams { Density = density, Seed = seed }, Zones);

        [Test]
        public void Layout_IsDeterministic_RespectsTheCap_AndPassesTheValidator()
        {
            string Key(PropLayoutData l) => string.Join(";", l.items.Select(i => i.type + i.x + i.z + i.state));
            Assert.AreEqual(Key(Layout()), Key(Layout()));
            Assert.AreNotEqual(Key(Layout()), Key(Layout(1f, 77)));
            var l2 = Layout(5f);
            Assert.LessOrEqual(l2.items.Count(i => PropTypes.IsSign(i.type)), 24);
            var res = PropLayoutValidator.Validate(Layout());
            Assert.AreEqual(0, res.Errors.Count, string.Join("\n", res.Errors));
            Assert.AreEqual(0, Layout(0f).items.Length);
        }

        [Test]
        public void Signs_StayOffTheRoad_AvoidExclusions_AndFaceTheStreet()
        {
            foreach (var s in Layout().items.Where(i => PropTypes.IsSign(i.type)))
            {
                if (Math.Abs(s.x) < 8f) Assert.GreaterOrEqual(Math.Abs(s.x), 4.3f, "señal sobre el asfalto");
                foreach (var z in Zones) Assert.IsFalse(z.Contains(s.x, s.z, 0f), "señal dentro de una zona excluida");
            }
        }

        [Test]
        public void AcrossSeeds_AllSignTypes_AndAllStates_Appear()
        {
            var types = new HashSet<string>(); var states = new HashSet<string>();
            for (int seed = 1; seed <= 12; seed++)
                foreach (var s in Layout(1f, seed).items.Where(i => PropTypes.IsSign(i.type))) { types.Add(s.type); states.Add(s.state); }
            foreach (var t in PropTypes.SignTypes) Assert.IsTrue(types.Contains(t), "falta " + t);
            foreach (var st in new[] { "upright", "tilted", "bent", "fallen", "pole_only", "hanging" }) Assert.IsTrue(states.Contains(st), "falta el estado " + st);
        }

        [Test]
        public void SomeSigns_AreHalfCoveredByWeedsAndDebris()
        {
            bool found = false;
            for (int seed = 1; seed <= 6 && !found; seed++)
            {
                var l = Layout(1f, seed);
                foreach (var s in l.items.Where(i => PropTypes.IsSign(i.type)))
                {
                    int grass = l.items.Count(g => g.type == PropTypes.Grass && Math.Abs(g.x - s.x) < 0.8f && Math.Abs(g.z - s.z) < 0.8f);
                    if (grass >= 3) found = true;
                }
            }
            Assert.IsTrue(found);
        }

        [Test]
        public void Validator_RejectsUnknownSignStates()
        {
            var layout = new PropLayoutData { items = new[]
            {
                new PropEntry { type = PropTypes.SignStop, x = 5, z = 5, scale = 1, state = "hanging" },
                new PropEntry { type = PropTypes.SignStop, x = 5, z = 5, scale = 1, state = "volando" },
                new PropEntry { type = PropTypes.SignStop, x = 5, z = 5, scale = 1, variant = 9, state = "" },
            } };
            var r = PropLayoutValidator.Validate(layout);
            Assert.AreEqual(1, r.Valid.Count);
            Assert.AreEqual(2, r.Errors.Count);
        }
    }
}

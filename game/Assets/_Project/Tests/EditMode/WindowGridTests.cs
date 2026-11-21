using System;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Ventanas de los edificios lejanos: pocas encendidas, solo colores de la paleta, parpadeo que mantiene la proporción y baliza intermitente.</summary>
    public class WindowGridTests
    {
        [Test]
        public void Generate_MostOff_AFewLit_Deterministic()
        {
            var a = WindowGrid.Generate(11); var b = WindowGrid.Generate(11);
            Assert.That(WindowGrid.LitRatio(a), Is.InRange(0.025f, 0.06f), "pocas encendidas");
            for (int i = 0; i < a.Length; i++) { Assert.AreEqual(a[i].Lit, b[i].Lit); Assert.That(a[i].Color, Is.InRange(0, WindowGrid.Palette.Length - 1)); }
        }

        [Test]
        public void Pixels_WindowsOnEvenPixels_WallBetween_PaletteOnlyWhenLit()
        {
            var c = WindowGrid.Generate(3);
            var p = WindowGrid.Pixels(c);
            Assert.AreEqual(WindowGrid.Size * WindowGrid.Size * 4, p.Length);
            int lit = 0;
            for (int y = 0; y < WindowGrid.Size; y++)
                for (int x = 0; x < WindowGrid.Size; x++)
                {
                    int o = (y * WindowGrid.Size + x) * 4;
                    if (x % 2 == 1 || y % 2 == 1) { Assert.AreEqual(WindowGrid.Wall[0], p[o]); continue; }
                    var cell = c[(y / 2) * WindowGrid.Cols + x / 2];
                    var want = WindowGrid.ColorOf(cell);
                    Assert.AreEqual(want[0], p[o]); Assert.AreEqual(want[2], p[o + 2]);
                    if (cell.Lit) lit++;
                }
            Assert.Greater(lit, 0);
        }

        [Test]
        public void Flicker_SomeGoOff_SomeGoOn_RatioStays()
        {
            var c = WindowGrid.Generate(5);
            var rng = new Random(1);
            int on = 0, off = 0;
            for (int k = 0; k < 2000; k++)
            {
                foreach (int i in WindowGrid.Flicker(c, rng, 2)) { if (c[i].Lit) on++; else off++; }
                Assert.That(WindowGrid.LitRatio(c), Is.InRange(0.025f, 0.06f));
            }
            Assert.Greater(on, 50); Assert.Greater(off, 50);
        }

        [Test]
        public void FacadeST_InsideTheTexture_AndScalesWithTheSize()
        {
            for (int s = 0; s < 50; s++)
            {
                var st = WindowGrid.FacadeST(4f + s * 0.4f, 10f + s * 0.6f, s);
                Assert.Greater(st[0], 0f); Assert.Greater(st[1], 0f);
                Assert.LessOrEqual(st[0] + st[2], 1f + 1e-5f); Assert.LessOrEqual(st[1] + st[3], 1f + 1e-5f);
            }
            Assert.Greater(WindowGrid.FacadeST(18f, 40f, 1)[1], WindowGrid.FacadeST(18f, 12f, 1)[1], "más alto = más plantas");
        }

        [Test]
        public void Beacon_BlinksSlowly()
        {
            int on = 0; for (int i = 0; i < 2200; i++) if (WindowGrid.BeaconOn(i / 1000.0, 0f)) on++;
            Assert.That(on / 2200.0, Is.InRange(0.18, 0.28));
        }
    }
}

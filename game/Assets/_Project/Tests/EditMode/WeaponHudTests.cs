using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class WeaponHudTests
    {
        public static IEnumerable<WeaponData> Weapons()
        {
            foreach (var w in WeaponCatalog.Defaults()) yield return w;
            foreach (var id in new[] { "bat", "axe", "shovel" }) yield return new WeaponData { id = id, kind = WeaponKind.Melee, length = 0.9f, thickness = 0.05f };
        }

        private static (float fill, int minX, int maxX, int minY, int maxY, int outline) Stats(byte[] img)
        {
            int w = WeaponIcons.Width, h = WeaponIcons.Height, filled = 0, outline = 0, minX = w, maxX = -1, minY = h, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 4;
                    if (img[o + 3] > 128) { if (img[o] > 70) filled++; else outline++; if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
                }
            return (filled / (float)(w * h), minX, maxX, minY, maxY, outline);
        }

        [TestCaseSource(nameof(Weapons))]
        public void EveryWeapon_GeneratesItsIcon_WhiteSilhouetteWithDarkOutline_InsideTheTexture(WeaponData d)
        {
            var img = WeaponIcons.Rasterize(WeaponModel.Build(d));
            Assert.AreEqual(WeaponIcons.Width * WeaponIcons.Height * 4, img.Length);
            var s = Stats(img);
            TestContext.WriteLine(d.id + ": relleno " + (s.fill * 100f).ToString("0.0") + " %, caja " + s.minX + "-" + s.maxX + " x " + s.minY + "-" + s.maxY);
            Assert.That(s.fill, Is.InRange(0.02f, 0.5f), d.id + ": silueta vacía o enorme");
            Assert.Greater(s.outline, 100, d.id + ": sin contorno");
            Assert.GreaterOrEqual(s.minX, 0); Assert.LessOrEqual(s.maxX, WeaponIcons.Width - 1);
            Assert.GreaterOrEqual(s.minX, 2, "con margen para el contorno"); Assert.LessOrEqual(s.maxX, WeaponIcons.Width - 3);
            Assert.GreaterOrEqual(s.minY, 2); Assert.LessOrEqual(s.maxY, WeaponIcons.Height - 3);
            // ocupa buena parte del ancho (se ve grande en la barra)
            Assert.Greater(s.maxX - s.minX, WeaponIcons.Width * 0.5f, d.id + ": icono demasiado pequeño");
        }

        [Test]
        public void IconsAreDistinct_AndFollowWeaponShape()
        {
            var hashes = new HashSet<int>();
            foreach (var d in Weapons())
            {
                var img = WeaponIcons.Rasterize(WeaponModel.Build(d));
                int h = 17; foreach (var b in img) h = h * 31 + b;
                Assert.IsTrue(hashes.Add(h), d.id + ": icono repetido");
            }
            // pistola: alta y corta; francotirador y escopeta: largas y estrechas (más ancho que alto)
            var pistol = WeaponCatalog.Defaults().Find(w => w.id == "pistol"); var sniper = WeaponCatalog.Defaults().Find(w => w.id == "sniper");
            var a = Stats(WeaponIcons.Rasterize(WeaponModel.Build(pistol))); var b2 = Stats(WeaponIcons.Rasterize(WeaponModel.Build(sniper)));
            float ra = (a.maxX - a.minX) / (float)(a.maxY - a.minY), rb = (b2.maxX - b2.minX) / (float)(b2.maxY - b2.minY);
            Assert.Greater(rb, ra * 1.5f, "el rifle es mucho más alargado que la pistola");
        }

        [Test]
        public void Rasterize_IsDeterministic_AndEmptyIconIsFaint()
        {
            var m = WeaponModel.Build(WeaponCatalog.Defaults()[0]);
            CollectionAssert.AreEqual(WeaponIcons.Rasterize(m), WeaponIcons.Rasterize(m));
            var e = WeaponIcons.Empty(); int opaque = 0;
            for (int i = 3; i < e.Length; i += 4) if (e[i] > 0) opaque++;
            Assert.That(opaque, Is.InRange(50, 3000), "marco de esquinas, no una silueta");
        }

        // ------------------------------------------------------------------ barra de ranuras
        [Test]
        public void ActiveSlot_GrowsAndLightsUp_OthersShrinkAndFade()
        {
            var bar = new WeaponBarState();
            bar.Set(4, 0, instant: true);
            for (int i = 0; i < 60; i++) bar.Tick(1f / 60f);
            Assert.AreEqual(WeaponBarState.ActiveScale, bar.ScaleOf(0), 0.01f);
            Assert.AreEqual(1f, bar.AlphaOf(0), 0.01f);
            for (int i = 1; i < 4; i++) { Assert.AreEqual(1f, bar.ScaleOf(i), 0.01f); Assert.AreEqual(WeaponBarState.InactiveAlpha, bar.AlphaOf(i), 0.01f); }
            Assert.AreEqual(WeaponBarState.EmptyAlpha, bar.AlphaOf(4), 1e-4f, "ranura vacía tenue");
        }

        [Test]
        public void SwitchingWeapon_AnimatesSmoothly_WithoutJumps_AndShowsNameFor15Seconds()
        {
            var bar = new WeaponBarState();
            bar.Set(3, 0, instant: true);
            for (int i = 0; i < 30; i++) bar.Tick(1f / 60f);
            Assert.AreEqual(0f, bar.NameAlpha, "sin cambio no hay nombre");
            Assert.IsTrue(bar.Set(3, 2), "el cambio se detecta");
            Assert.AreEqual(2, bar.Active);
            float prevScale = bar.ScaleOf(2), maxStep = 0f;
            for (int i = 0; i < 60; i++)
            {
                bar.Tick(1f / 60f);
                maxStep = Math.Max(maxStep, Math.Abs(bar.ScaleOf(2) - prevScale)); prevScale = bar.ScaleOf(2);
            }
            Assert.Less(maxStep, 0.08f, "la escala crece sin saltos");
            Assert.AreEqual(WeaponBarState.ActiveScale, bar.ScaleOf(2), 0.02f); Assert.AreEqual(1f, bar.ScaleOf(0), 0.02f, "la anterior vuelve a su tamaño");
            // nombre: visible ~1.5 s y luego fuera
            var b2 = new WeaponBarState(); b2.Set(3, 0, true); b2.Set(3, 1);
            b2.Tick(0.5f); Assert.AreEqual(1f, b2.NameAlpha, 1e-4f);
            b2.Tick(0.7f); Assert.AreEqual(1f, b2.NameAlpha, 1e-4f, "a 1.2 s sigue entero");
            b2.Tick(0.2f); Assert.That(b2.NameAlpha, Is.InRange(0.01f, 0.99f), "a 1.4 s se está fundiendo");
            b2.Tick(0.2f); Assert.AreEqual(0f, b2.NameAlpha, 1e-4f, "a 1.6 s ya no");
        }

        [Test]
        public void Layout_NeighborsMakeRoom_NoOverlap_AlignedRight()
        {
            var bar = new WeaponBarState();
            bar.Set(5, 2, instant: true);
            for (int i = 0; i < 60; i++) bar.Tick(1f / 60f);
            var r = new SlotRect[WeaponBarState.Slots];
            bar.Layout(r);
            Assert.AreEqual(0f, r[4].Right, "la última, pegada al borde derecho");
            for (int i = 0; i < 4; i++) Assert.GreaterOrEqual(r[i].Right, r[i + 1].Right + r[i + 1].Width + WeaponBarState.Gap - 0.01f, "sin solaparse");
            Assert.Greater(r[2].Width, r[1].Width * 1.4f); Assert.Greater(r[2].Height, r[3].Height * 1.4f);
            Assert.Less(WeaponBarState.TotalWidth(r), 700f, "barra compacta");
        }

        [Test]
        public void Set_ClampsActiveToTheOccupiedSlots()
        {
            var bar = new WeaponBarState();
            bar.Set(2, 4, true);
            Assert.AreEqual(1, bar.Active);
            bar.Set(0, 3, true);
            Assert.AreEqual(0, bar.Active);
        }

        // ------------------------------------------------------------------ rueda radial
        [TestCase(0f, 100f, 5, 0), TestCase(95f, 30f, 5, 1), TestCase(60f, -90f, 5, 2), TestCase(-60f, -90f, 5, 3), TestCase(-95f, 30f, 5, 4)]
        public void Wheel_PicksTheSliceInTheMouseDirection(float x, float y, int count, int expected)
            => Assert.AreEqual(expected, RadialWheel.IndexAt(x, y, count));

        [Test]
        public void Wheel_DeadZoneKeepsCurrent_AndSlotCentersMatchTheirDirection()
        {
            Assert.AreEqual(-1, RadialWheel.IndexAt(10f, 10f, 5));
            for (int n = 2; n <= 5; n++)
                for (int i = 0; i < n; i++)
                {
                    var c = RadialWheel.SlotCenter(i, n, 100f);
                    Assert.AreEqual(i, RadialWheel.IndexAt(c.X, c.Y, n), "n=" + n + " i=" + i);
                }
            var acc = RadialWheel.Accumulate(new V2(0, 0), 500f, 0f, 120f);
            Assert.AreEqual(120f, Math.Sqrt(acc.X * acc.X + acc.Y * acc.Y), 0.01f, "el cursor virtual no sale del radio");
        }

        [Test]
        public void SlowMotion_EasesDown_NeverBelowTheLimit_AndBackToNormal()
        {
            var s = new SlowMotion();
            float prev = 1f;
            for (int i = 0; i < 90; i++) { float v = s.Tick(1f / 60f, true); Assert.LessOrEqual(v, prev + 1e-4f, "baja sin rebotar"); Assert.GreaterOrEqual(v, SlowMotion.Min); prev = v; }
            Assert.AreEqual(SlowMotion.Min, prev, 0.02f);
            for (int i = 0; i < 90; i++) prev = s.Tick(1f / 60f, false);
            Assert.AreEqual(1f, prev, 0.02f);
        }

        [Test]
        public void HudText_AmmoAndMode()
        {
            var rifle = WeaponCatalog.Defaults().Find(w => w.id == "assault_rifle"); var bar = WeaponCatalog.Defaults().Find(w => w.id == "bar");
            Assert.AreEqual("30 / 120", WeaponHudText.Ammo(rifle, 30, 120));
            Assert.AreEqual("", WeaponHudText.Ammo(bar, 0, 0), "cuerpo a cuerpo: sin munición");
            StringAssert.Contains("[X]", WeaponHudText.Mode(rifle, FireModes.Auto));
            Assert.AreEqual("", WeaponHudText.Mode(bar, FireModes.Semi));
        }
    }
}

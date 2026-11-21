using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Arte del menú (fondo en llamas, calavera propia, marco, resaltado, silueta, vista de mapa) y perfil local (ranking, Continuar).</summary>
    public class MenuArtTests
    {
        [Test]
        public void Backdrop_Deterministic_RedAndFire_CityDarkBelow_SkullFaint()
        {
            int w = 192, h = 108;
            var a = MenuArt.Backdrop(w, h, 7); var b = MenuArt.Backdrop(w, h, 7);
            CollectionAssert.AreEqual(a, b, "se genera igual siempre (cacheable)");
            float r = 0, g = 0, bl = 0; for (int i = 0; i < w * h; i++) { r += a[i * 4]; g += a[i * 4 + 1]; bl += a[i * 4 + 2]; }
            Assert.Greater(r, g * 1.5f, "rojo dominante"); Assert.Greater(g, bl, "naranja, no azul");
            int dark = 0; for (int x = 0; x < w; x++) if (a[(1 * w + x) * 4] < 40) dark++;
            Assert.Greater(dark, w / 3, "silueta de la ciudad abajo");
        }

        [Test]
        public void Emblem_RoundBadge_BoneSkull_EmberEyes_Transparent()
        {
            int n = 128; var e = MenuArt.Emblem(n, 11);
            CollectionAssert.AreEqual(e, MenuArt.Emblem(n, 11));
            Assert.AreEqual(0, e[3], "esquina transparente (insignia redonda)");
            int c = ((n / 2 + 16) * n + n / 2) * 4; Assert.Greater(e[c], 110, "hueso claro en la frente"); Assert.AreEqual(255, e[c + 3]);
            int ember = 0; for (int i = 0; i < n * n; i++) if (e[i * 4] > 180 && e[i * 4 + 1] > 40 && e[i * 4 + 1] < 140 && e[i * 4 + 2] < 60) ember++;
            Assert.Greater(ember, 5, "brasas en las cuencas");
        }

        [Test]
        public void FlameAtlas_LoopsSeamlessly_HotBottom_ClearTop()
        {
            MenuArt.Flame(0.3f, 0.2f, 0f, 3, out var r0, out var g0, out _, out var a0);
            MenuArt.Flame(0.3f, 0.2f, 1f, 3, out var r1, out var g1, out _, out var a1);
            Assert.AreEqual(a0, a1, 1e-4f, "la fase 1 es igual a la 0: bucle sin salto"); Assert.AreEqual(g0, g1, 1e-4f);
            MenuArt.Flame(0f, 0.3f, 0.4f, 3, out _, out _, out _, out var l); MenuArt.Flame(1f, 0.3f, 0.4f, 3, out _, out _, out _, out var rr);
            Assert.AreEqual(l, rr, 1e-4f, "se repite en horizontal sin costura");
            float bottom = 0, top = 0; for (int i = 0; i < 64; i++) { MenuArt.Flame(i / 64f, 0.02f, 0.5f, 3, out _, out _, out _, out var a); bottom += a; MenuArt.Flame(i / 64f, 0.98f, 0.5f, 3, out _, out _, out _, out a); top += a; }
            Assert.Greater(bottom, 50f, "base llena de fuego"); Assert.Less(top, 6f, "arriba casi transparente");
            Assert.AreEqual(MenuArt.FlameW * MenuArt.FlameH * MenuArt.FlameFrames * 4, MenuArt.FlameAtlas(3).Length);
        }

        [Test]
        public void RedSkull_GradientMap_RimOrange_Alpha60to70_CutsFlatBackground()
        {
            SkullGradientIs(0f, 0x1A, 0x05, 0x04); SkullGradientIs(1f, 0xFF, 0x5A, 0x2A);
            Assert.That(MenuArt.SkullAlpha, Is.InRange(0.6f, 0.7f));
            // fondo blanco plano: se recorta; el dibujo oscuro del centro queda
            int w = 32; var img = new byte[w * w * 4];
            for (int i = 0; i < w * w; i++) { bool c = System.Math.Abs(i % w - 16) < 8 && System.Math.Abs(i / w - 16) < 8; byte v = c ? (byte)90 : (byte)255; img[i * 4] = img[i * 4 + 1] = img[i * 4 + 2] = v; img[i * 4 + 3] = 255; }
            MenuArt.Cutout(img, w, w, out var lum, out var al);
            Assert.AreEqual(0f, al[0], 1e-3f, "fondo fuera"); Assert.AreEqual(1f, al[16 * w + 16], 1e-3f, "cráneo dentro");
            MenuArt.ProceduralSkull(96, out lum, out al);
            var px = MenuArt.RedSkull(lum, al, 96, 96, 5);
            Assert.AreEqual(0, px[3], "esquina transparente");
            int maxA = 0, rimish = 0, red = 0; for (int i = 0; i < 96 * 96; i++) { maxA = System.Math.Max(maxA, px[i * 4 + 3]); if (px[i * 4 + 3] > 0 && px[i * 4] > 200 && px[i * 4 + 1] > 90) rimish++; if (px[i * 4] > px[i * 4 + 1] * 2 && px[i * 4 + 3] > 100) red++; }
            Assert.LessOrEqual(maxA, 180, "60-70 % de opacidad"); Assert.Greater(rimish, 10, "borde de luz naranja"); Assert.Greater(red, 1500, "teñida en rojo");
        }

        private static void SkullGradientIs(float l, int r, int g, int b)
        { MenuArt.SkullGradient(l, out var rr, out var gg, out var bb); Assert.AreEqual(r / 255f, rr, 0.01f); Assert.AreEqual(g / 255f, gg, 0.01f); Assert.AreEqual(b / 255f, bb, 0.01f); }

        [Test]
        public void Frame_CornersBroken_LineAndDarkFill()
        {
            var f = MenuArt.Frame(64, 12, 2);
            Assert.AreEqual(0, f[3], "esquina rota transparente");
            int mid = (32 * 64 + 32) * 4; Assert.That(f[mid + 3], Is.InRange(150, 230), "relleno oscuro semitransparente"); Assert.Less(f[mid], 80);
            int line = (32 * 64 + 2) * 4; Assert.Greater(f[line + 3], 100, "línea fina del marco");
            var hl = MenuArt.Highlight(128, 16, 4); Assert.Greater(hl[3], hl[(127) * 4 + 3], "resaltado más fuerte a la izquierda");
        }

        [Test]
        public void Soldier_AndMapPreview_HaveContent()
        {
            var s = MenuArt.Soldier(48, 80, new[] { 0.9f, 0.8f, 0.7f }); int solid = 0; for (int i = 3; i < s.Length; i += 4) if (s[i] > 128) solid++;
            Assert.That(solid, Is.InRange(400, 2500));
            var m = new MapData { halfSize = 30f, props = new[] { new MapProp { kind = "wall", name = "W", pos = new[] { 0f, 2f, 30.5f }, size = new[] { 61f, 4f, 1f }, rot = new[] { 0f, 0f, 0f } } }, houses = new[] { new HouseLayoutData() }, spawns = new MapSpawn[0] };
            var p = MenuArt.MapPreview(m, 64); Assert.AreEqual(64 * 64 * 4, p.Length);
        }

        [Test]
        public void Profile_RankingByPoints_ContinueLastUnfinishedDay_Capped()
        {
            var pr = new LocalProfile();
            pr.Add(new MatchRecord { player = "a", points = 100, dayReached = 2, date = "1" });
            pr.Add(new MatchRecord { player = "b", points = 900, dayReached = 7, victory = true, date = "2" });
            Assert.AreEqual(0, pr.ContinueDay, "la última se ganó: nada que continuar");
            pr.Add(new MatchRecord { player = "a", points = 500, dayReached = 4, date = "3", mapId = "bosque_oscuro" });
            Assert.AreEqual(4, pr.ContinueDay);
            var r = pr.Ranking(2); Assert.AreEqual(2, r.Count); Assert.AreEqual(900, r[0].points); Assert.AreEqual(500, r[1].points);
            Assert.AreEqual(1, pr.Victories); Assert.AreEqual(900, pr.BestPoints);
            for (int i = 0; i < 60; i++) pr.Add(new MatchRecord());
            Assert.AreEqual(LocalProfile.MaxRecords, pr.matches.Count);
            var ms = new MatchStats { mapId = "x", victory = false }; ms.days.Add(new DayStats { day = 1, kills = 12, headshotKills = 3, score = 50f });
            var rec = LocalProfile.From(ms, "yo", 2, "hoy"); Assert.AreEqual(12, rec.kills); Assert.AreEqual(ms.RankingPoints(), rec.points);
        }
    }
}

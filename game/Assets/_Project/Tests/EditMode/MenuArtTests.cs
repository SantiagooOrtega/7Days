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
            Assert.Less(MenuArt.SkullOpacity, 0.45f, "calavera a baja opacidad");
            Assert.Less(MenuArt.SkullSd(0f, 0.3f), 0f, "frente de la calavera dentro"); Assert.Greater(MenuArt.SkullSd(0.3f, 0.12f), 0f, "cuenca hueca");
        }

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

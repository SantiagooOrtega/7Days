using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Luna y estrellas: textura con cráteres y disco recortado, estrellas sobre el horizonte, parpadeo sutil, fugaces ocasionales, atenuación por niebla/horizonte/nubes y valores por mapa.</summary>
    public class NightSkyTests
    {
        private static MapAmbience A(string id) => JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Maps/" + id + ".json"))).ambience;

        [Test]
        public void MoonTexture_Disc_WithCraters()
        {
            var px = NightSky.MoonTexture(13); int n = NightSky.MoonSize;
            Assert.AreEqual(n * n * 4, px.Length);
            Assert.AreEqual(0, px[3], "esquina transparente");
            int c = (n / 2 * n + n / 2) * 4; Assert.AreEqual(255, px[c + 3], "centro opaco");
            int min = 255, max = 0;
            for (int y = n / 4; y < 3 * n / 4; y++) for (int x = n / 4; x < 3 * n / 4; x++) { int v = px[(y * n + x) * 4]; min = Mathf.Min(min, v); max = Mathf.Max(max, v); }
            Assert.Greater(max - min, 40, "cráteres y mares: contraste");
        }

        [Test]
        public void Stars_AboveHorizon_TwinkleSubtly_ShootingStarsAreRare()
        {
            var s = NightSky.Stars(500, 2);
            foreach (var st in s) { Assert.Greater(st[1], 0.05f); Assert.AreEqual(1f, st[0] * st[0] + st[1] * st[1] + st[2] * st[2], 1e-3f); }
            for (double t = 0; t < 10; t += 0.1) Assert.That(NightSky.Twinkle(t, 0.3f), Is.InRange(0.79f, 1.01f));
            int shots = 0; for (int sec = 0; sec < 2000; sec++) if (NightSky.ShootingStarAt(sec, 13)) shots++;
            Assert.That(shots, Is.InRange(40, 130), "una cada ~25 s");
        }

        [Test]
        public void Visibility_FogHorizonAndClouds_Attenuate()
        {
            Assert.Greater(NightSky.Visibility(0.0135f, 50f, 0f), NightSky.Visibility(0.047f, 50f, 0f), "más niebla, menos cielo");
            Assert.Greater(NightSky.Visibility(0.02f, 50f, 0f), NightSky.Visibility(0.02f, 5f, 0f), "cerca del horizonte se apaga");
            Assert.Greater(NightSky.Visibility(0.02f, 50f, 0f), NightSky.Visibility(0.02f, 50f, 0.8f), "nubes y humo la tapan");
            for (double t = 0; t < 300; t += 3) Assert.That(NightSky.CloudCover(t, 0.65f), Is.InRange(0f, 1f));
        }

        [Test]
        public void Maps_ForestMoonMoreVisible_StreetMoonBetweenClouds()
        {
            var c = A("calle_en_ruinas"); var b = A("bosque_oscuro");
            Assert.Greater(c.moon, 0f); Assert.Greater(b.moon, 0f);
            Assert.Greater(c.moonClouds, b.moonClouds, "la Calle: luna entre nubes y humo");
            Assert.Greater(b.moonSize, c.moonSize); Assert.Greater(b.stars, c.stars);
            // brillo efectivo con su niebla, su luna a la altura de la luz principal y sus nubes: el Bosque la ve más
            float Eff(MapAmbience a) => NightSky.Visibility(a.fogDensity, a.sunPitch, a.moonClouds) * a.moon;
            Assert.Greater(Eff(b), Eff(c) * 1.5f, "luna más visible en el Bosque");
        }
    }
}

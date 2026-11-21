using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Más niebla tras probar: densa, con pared de bruma en el horizonte, pero sin tapar zombis a menos de 25 m.</summary>
    public class FogTests
    {
        private static MapAmbience A(string id) => JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Maps/" + id + ".json"))).ambience;

        [Test]
        public void Model_VisibilityAndMaxDensity_AreConsistent()
        {
            Assert.AreEqual(1f, FogModel.Visibility(0f, 100f), 1e-6f);
            float max = FogModel.MaxDensity();
            Assert.AreEqual(FogModel.MinVisibility, FogModel.Visibility(max, FogModel.SafeDistance), 1e-4f);
            Assert.Less(FogModel.Visibility(0.02f, 60f), FogModel.Visibility(0.02f, 30f));
            Assert.Less(FogModel.FarDistance(0.0108f), FogModel.FarDistance(0.008f), "más densa = se ve menos lejos");
            var c = FogModel.HorizonColor(new[] { 0f, 0f, 0f }, new[] { 1f, 1f, 1f }, 0.5f);
            Assert.AreEqual(0.5f, c[0], 1e-6f);
        }

        [TestCase("calle_en_ruinas", 0.008f, 1.0f)]
        [TestCase("bosque_oscuro", 0.045f, 1.8f)]
        public void Maps_HaveMoreFog_ButZombiesAt25mStayVisible(string id, float oldDensity, float oldGround)
        {
            var a = A(id);
            Assert.LessOrEqual(a.fogDensity, FogModel.MaxDensity(), id + ": no debe tapar zombis a 25 m");
            Assert.GreaterOrEqual(FogModel.Visibility(a.fogDensity, 25f), FogModel.MinVisibility - 1e-4f);
            Assert.GreaterOrEqual(a.fogDensity, oldDensity, id + ": nunca menos niebla que antes");
            // +35 %: en la densidad global si cabe (Calle) o, si no, en la niebla baja (Bosque, ya al límite de los 25 m)
            Assert.That(a.fogDensity >= oldDensity * 1.3f || a.groundFogDensity >= oldGround * 1.3f, id + ": ~+35 % de niebla");
            Assert.GreaterOrEqual(a.groundFogDensity, oldGround * 1.3f, id + ": más bruma baja");
            Assert.Greater(a.horizonFog, 0f, id + ": pared de bruma en el horizonte");
            CollectionAssert.IsEmpty(MapKit.Validate(JsonUtility.FromJson<MapData>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Maps/" + id + ".json")))));
        }

        [Test]
        public void SecondRound_Plus25Percent_StillVisibleAt25m()
        {
            var c = A("calle_en_ruinas"); var b = A("bosque_oscuro");
            Assert.GreaterOrEqual(c.fogDensity, 0.0108f * 1.24f, "Calle: +25 % sobre 0.0108");
            Assert.GreaterOrEqual(b.groundFogDensity, 2.45f * 1.24f, "Bosque: +25 % en la bruma baja (la densidad ya está al máximo de 25 m)");
            Assert.Greater(b.horizonFog, 1.2f);
            foreach (var a in new[] { c, b }) Assert.GreaterOrEqual(FogModel.Visibility(a.fogDensity, 25f), FogModel.MinVisibility - 1e-4f);
        }

        [Test]
        public void ThirdRound_MoreFog_Darker_StillVisibleAt25m()
        {
            var c = A("calle_en_ruinas"); var b = A("bosque_oscuro");
            Assert.GreaterOrEqual(c.fogDensity, 0.0135f * 1.19f, "Calle: +20 %");
            Assert.GreaterOrEqual(b.groundFogDensity, 3.05f * 1.19f, "Bosque: +20 % en la bruma baja");
            Assert.LessOrEqual(c.brightness, 1.25f * 0.86f); Assert.LessOrEqual(b.brightness, 1.3f * 0.86f);
            Assert.LessOrEqual(c.sunIntensity, 0.85f); Assert.LessOrEqual(b.sunIntensity, 0.8f * 0.85f);
            foreach (var a in new[] { c, b }) Assert.GreaterOrEqual(FogModel.Visibility(a.fogDensity, 25f), FogModel.MinVisibility - 1e-4f);
        }

        [Test]
        public void FourthRound_Plus20Percent_StillVisibleAt25m()
        {
            var c = A("calle_en_ruinas"); var b = A("bosque_oscuro");
            Assert.GreaterOrEqual(c.fogDensity, 0.0162f * 1.19f, "Calle: +20 % de densidad");
            Assert.GreaterOrEqual(c.groundFogDensity, 2.04f * 1.19f, "Calle: +20 % de bruma baja");
            Assert.GreaterOrEqual(b.groundFogDensity, 3.66f * 1.19f, "Bosque: +20 % en la bruma baja (la densidad sigue en el máximo de 25 m)");
            Assert.GreaterOrEqual(b.horizonFog, 1.8f * 1.1f, "Bosque: más pared de bruma lejana");
            foreach (var a in new[] { c, b }) Assert.GreaterOrEqual(FogModel.Visibility(a.fogDensity, 25f), FogModel.MinVisibility - 1e-4f, "zombis visibles a 25 m");
        }

        [Test]
        public void FifthRound_Plus20Percent_StillVisibleAt25m()
        {
            var c = A("calle_en_ruinas"); var b = A("bosque_oscuro");
            Assert.GreaterOrEqual(c.fogDensity, 0.0195f * 1.19f, "Calle: +20 % de densidad");
            Assert.GreaterOrEqual(c.groundFogDensity, 2.45f * 1.19f, "Calle: +20 % de bruma baja");
            Assert.GreaterOrEqual(b.groundFogDensity, 4.4f * 1.19f, "Bosque: +20 % en la bruma baja (densidad en su tope)");
            Assert.GreaterOrEqual(b.horizonFog, 2.0f * 1.19f, "Bosque: +20 % en la bruma del horizonte");
            Assert.LessOrEqual(b.fogDensity, FogModel.MaxDensity() + 1e-4f, "Bosque: la densidad no pasa su tope");
            foreach (var a in new[] { c, b }) Assert.GreaterOrEqual(FogModel.Visibility(a.fogDensity, 25f), FogModel.MinVisibility - 1e-4f, "zombis visibles a 25 m");
        }

        [Test]
        public void SixthRound_Plus20Percent_StillVisibleAt25m()
        {
            var c = A("calle_en_ruinas"); var b = A("bosque_oscuro");
            Assert.GreaterOrEqual(c.fogDensity, 0.0234f * 1.19f, "Calle: +20 % de densidad");
            Assert.GreaterOrEqual(c.groundFogDensity, 2.94f * 1.19f, "Calle: +20 % de bruma baja");
            Assert.GreaterOrEqual(b.groundFogDensity, 5.28f * 1.19f, "Bosque: +20 % en la bruma baja");
            Assert.GreaterOrEqual(b.horizonFog, 2.4f * 1.19f, "Bosque: +20 % en el horizonte");
            Assert.LessOrEqual(b.horizonFog, 3f, "dentro de la validación");
            foreach (var a in new[] { c, b }) Assert.GreaterOrEqual(FogModel.Visibility(a.fogDensity, 25f), FogModel.MinVisibility - 1e-4f, "zombis visibles a 25 m");
        }
    }
}

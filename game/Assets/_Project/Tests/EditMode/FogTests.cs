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
    }
}

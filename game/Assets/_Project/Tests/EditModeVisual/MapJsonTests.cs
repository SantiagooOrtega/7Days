using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>El JSON del mapa 1 que va en el proyecto (respaldo local sin servidor) se carga, es válido y coincide con lo que genera el código (no se desincroniza).</summary>
    public class MapJsonTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);

        [Test]
        public void StreetRuinsJson_LoadsValid_AndMatchesTheGenerator()
        {
            var io = T("SevenDays.World.MapIO");
            var map = (MapData)io.GetMethod("LoadLocal").Invoke(null, new object[] { "calle_en_ruinas" });
            Assert.IsNotNull(map, "Resources/Maps/calle_en_ruinas.json");
            CollectionAssert.IsEmpty(MapKit.Validate(map));
            Assert.AreEqual("Calle en ruinas", map.name);
            var look = Activator.CreateInstance(T("SevenDays.Look.LookSettings"));
            var generated = T("SevenDays.World.MapDefaults").GetMethod("StreetRuins").Invoke(null, new[] { look });
            string a = (string)io.GetMethod("ToJson").Invoke(null, new[] { (object)map }), b = (string)io.GetMethod("ToJson").Invoke(null, new[] { generated });
            Assert.AreEqual(b, a, "el JSON guardado ya no coincide con el generador: vuelve a exportarlo (7Days > Mapas)");
        }
    }
}

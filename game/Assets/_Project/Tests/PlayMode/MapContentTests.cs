using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Cada mapa construye su perímetro (trinchera o cerca) y su vegetación (arbustos, hierba, árboles; se dibujan con GPU instancing): se cuentan las piezas.
    /// Si faltaran en la build, la causa sería el recorte de variantes de instancing (BuildShadersTests lo vigila).
    /// </summary>
    public class MapContentTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private GameObject _arena;

        [UnitySetUp]
        public IEnumerator CleanStart()
        {
            foreach (var a in Object.FindObjectsByType(Game.GetType("SevenDays.World.TestArena"), FindObjectsSortMode.None)) Object.Destroy(((Component)a).gameObject);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) Object.Destroy(pl);
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_arena != null) Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) Object.Destroy(hud);
            Game.GetType("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            yield return null; yield return null;
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Map_HasPerimeterAndVegetation([Values("calle_en_ruinas", "bosque_oscuro")] string id)
        {
            Game.GetType("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, id);
            _arena = new GameObject("Arena"); _arena.AddComponent(Game.GetType("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var pb = Game.GetType("SevenDays.World.PerimeterBarrier");
            var per = pb.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(per, id + ": perímetro construido");
            int inst = (int)pb.GetProperty("Instances").GetValue(per), calls = (int)pb.GetProperty("DrawCalls").GetValue(per);
            Assert.Greater(inst, 100, id + ": piezas de la cerca/trinchera"); Assert.Greater(calls, 0, id + ": se dibujan");
            int veg = 0; foreach (var b in Object.FindObjectsByType(Game.GetType("SevenDays.World.InstancedBatch"), FindObjectsSortMode.None)) veg += (int)b.GetType().GetProperty("Count").GetValue(b);
            Assert.Greater(veg, 20, id + ": arbustos, hierba y señales");
            Debug.Log("[Mapa] " + id + ": perímetro " + inst + " instancias en " + calls + " lotes, vegetación " + veg);
        }
    }
}

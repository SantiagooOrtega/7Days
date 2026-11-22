using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Ningún bloque flotando en los mapas jugables: tras cargar cada mapa, la auditoría de piezas flotantes (renderers a más de 0.5 m del suelo sin nada visible
    /// debajo, salvo luces, árboles y la estructura de las casas) da 0. Cubre los cascotes que quedaban sobre la trinchera y las azoteas del fondo.
    /// </summary>
    public class FloatingPiecesTests
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
            foreach (var p in Object.FindObjectsByType(Game.GetType("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) Object.Destroy(((Component)p).gameObject);
            if (_arena != null) Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) Object.Destroy(pl);
            var hud = GameObject.Find("HUD"); if (hud != null) Object.Destroy(hud);
            Game.GetType("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator NoFloatingPieces([Values("calle_en_ruinas", "bosque_oscuro")] string id)
        {
            Game.GetType("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, id);
            _arena = new GameObject("Arena"); _arena.AddComponent(Game.GetType("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var level = _arena.transform.Find("Level");
            Assert.IsNotNull(level);
            var bad = (System.Collections.Generic.List<string>)Game.GetType("SevenDays.World.FloatingAudit").GetMethod("Run").Invoke(null, new object[] { level, false });
            Assert.AreEqual(0, bad.Count, id + ": piezas flotantes\n" + string.Join("\n", bad.GetRange(0, Mathf.Min(40, bad.Count))));
            foreach (Transform c in level) Assert.IsFalse(c.name.StartsWith("Crumble "), id + ": quedan cascotes sobre el muro oculto");
        }
    }
}

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>Levanta la arena completa y comprueba que la auditoría de colisiones termina en orden, que no hay blancos de práctica y que el jugador existe.</summary>
    public class ArenaAuditTests
    {
        [UnityTest]
        public IEnumerator Arena_BootsWithoutPills_AndCollisionAuditIsClean()
        {
            var log = new System.Collections.Generic.List<string>();
            Application.LogCallback cb = (msg, st, type) => log.Add(type + ": " + msg);
            Application.logMessageReceived += cb;
            var arenaType = Assembly.Load("Assembly-CSharp").GetType("SevenDays.World.TestArena", true);
            new GameObject("Arena").AddComponent(arenaType);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsNotNull(GameObject.FindWithTag("Player"), "el jugador existe");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                Assert.IsFalse(t.name.StartsWith("Dummy"), "queda un blanco de práctica: " + t.name);
            Application.logMessageReceived -= cb;
            string audit = log.Find(l => l.Contains("Auditoría de colisiones"));
            Debug.Log("[Auditoría en la arena] " + audit);
            Assert.IsNotNull(audit, "la auditoría corrió");
            Assert.IsTrue(audit.Contains("Todo en orden"), "auditoría con avisos: " + audit);
            var m = System.Text.RegularExpressions.Regex.Match(audit, @"(\d+)/(\d+) sólidos");
            Assert.AreEqual(m.Groups[1].Value, m.Groups[2].Value, "N/N");
        }
    }
}

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

            // HUD de armas integrado: ranuras con iconos, activa resaltada y cambio de arma que actualiza el HUD
            var asm = Assembly.Load("Assembly-CSharp");
            var hudType = asm.GetType("SevenDays.UI.WeaponHud", true);
            var hud = Object.FindFirstObjectByType(hudType) as Component;
            Assert.IsNotNull(hud, "el HUD de armas existe");
            var view = hudType.GetProperty("View").GetValue(hud);
            var vt = view.GetType();
            var player = GameObject.FindWithTag("Player");
            var shooting = player.GetComponent(asm.GetType("SevenDays.Player.PlayerShooting"));
            var inv = player.GetComponent(asm.GetType("SevenDays.Player.PlayerInventory"));
            int count = ((System.Collections.ICollection)inv.GetType().GetField("Weapons").GetValue(inv)).Count;
            Assert.GreaterOrEqual(count, 2, "el jugador empieza con al menos 2 armas");
            for (int i = 0; i < 90; i++) yield return null;
            var bar = (SevenDays.Procedural.WeaponBarState)vt.GetProperty("Bar").GetValue(view);
            Assert.AreEqual(0, bar.Active);
            for (int i = 0; i < count; i++) Assert.IsNotNull(vt.GetMethod("IconOf").Invoke(view, new object[] { i }), "icono de la ranura " + i);
            string ammo0 = (string)vt.GetProperty("AmmoText").GetValue(view);
            Assert.IsTrue((bool)shooting.GetType().GetMethod("SelectSlot").Invoke(shooting, new object[] { 1 }), "cambiar a la ranura 2");
            for (int i = 0; i < 90; i++) yield return null;
            Assert.AreEqual(1, bar.Active, "el HUD sigue el cambio de arma");
            var rect0 = (RectTransform)vt.GetMethod("SlotRect").Invoke(view, new object[] { 0 });
            var rect1 = (RectTransform)vt.GetMethod("SlotRect").Invoke(view, new object[] { 1 });
            Assert.Greater(rect1.sizeDelta.x, rect0.sizeDelta.x * 1.4f, "la ranura 2 es la grande");
            Debug.Log("[HUD en la arena] munición antes '" + ammo0 + "' y después '" + vt.GetProperty("AmmoText").GetValue(view) + "', nombre '" + vt.GetProperty("NameText").GetValue(view) + "'");
        }
    }
}

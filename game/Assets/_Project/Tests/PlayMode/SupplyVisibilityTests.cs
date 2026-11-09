using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Suministros en la arena real: cada tipo con su modelo, flotando 20-30 cm sobre el suelo, sin colliders (no estorban ni paran balas), marcador visible cerca y
    /// oculto a más de 15 m, columna de luz solo en los drops, luces con su tope, y la recogida con E. Con ZOMBIE_SHOTS guarda capturas de cerca y de lejos.
    /// </summary>
    public class SupplyVisibilityTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);   // el jugador vive fuera de la arena: si no, la siguiente prueba encuentra el viejo
            yield return null; yield return null;
        }

        private static object P(object o, string n) => o.GetType().GetProperty(n).GetValue(o);

        [UnityTest]
        public IEnumerator EverySupply_HasItsModel_FloatsWithoutColliders_AndShowsItsMarker()
        {
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 120; i++) yield return null;
            var player = GameObject.FindWithTag("Player");
            var mgrT = T("SevenDays.World.Loot.LootManager");
            var mgr = mgrT.GetProperty("Instance").GetValue(null);
            var spawn = mgrT.GetMethod("Spawn");
            var cam = Camera.main;
            var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized; var right = Vector3.Cross(Vector3.up, fwd);
            var items = new[] { LootItem.Ammo(AmmoType.Pistol, 6), LootItem.Ammo(AmmoType.Shotgun, 4), LootItem.Ammo(AmmoType.Rifle, 10), LootItem.Medkit(MedkitKind.Bandage),
                                LootItem.Medkit(MedkitKind.Small), LootItem.Medkit(MedkitKind.Medium), LootItem.Medkit(MedkitKind.Large), LootItem.Weapon("smg"), LootItem.Weapon("sniper") };
            var spawned = new Component[items.Length];
            // los ponemos lejos del jugador (más de 1.5 m) para que no se recojan solos
            for (int i = 0; i < items.Length; i++)
            {
                Vector3 pos = player.transform.position + fwd * 4.5f + right * ((i - 4f) * 0.75f) + Vector3.up * 0.35f;
                spawned[i] = (Component)spawn.Invoke(mgr, new object[] { items[i], pos, i % 2 == 0 ? null : "prueba" + i });
                Assert.IsNotNull(spawned[i], "tope de pickups");
            }
            for (int i = 0; i < 40; i++) yield return null;
            int solid = ~((1 << 2) | (1 << LayerMask.NameToLayer("Hitbox") < 0 ? 0 : 1 << LayerMask.NameToLayer("Hitbox")));
            for (int i = 0; i < items.Length; i++)
            {
                var p = spawned[i];
                Assert.AreEqual(0, p.GetComponentsInChildren<Collider>(true).Length, items[i].id + ": sin colliders");
                var visual = p.transform.Find("Loot Visual");
                Assert.IsNotNull(visual);
                int tris = 0; foreach (var mf in visual.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null && mf.gameObject.name != "Contorno") tris += mf.sharedMesh.triangles.Length / 3;
                Assert.Greater(tris, 60, items[i].kind + " " + items[i].id + ": modelo propio");
                float hover = visual.position.y - p.transform.position.y;
                Assert.That(hover, Is.InRange(0.18f, 0.32f), "flota 20-30 cm");
                Assert.IsTrue(Physics.Raycast(p.transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit g, 1f, solid, QueryTriggerInteraction.Ignore) && Mathf.Abs(g.point.y - p.transform.position.y) < 0.05f, "apoyado en el suelo real");
                Assert.IsTrue((bool)P(p, "MarkerVisible"), items[i].id + ": marcador a 4.5 m");
                Assert.AreEqual(i % 2 == 0, p.transform.Find("Columna de luz").gameObject.activeSelf, "columna de luz solo en los drops");
            }
            Assert.LessOrEqual((int)P(mgr, "LightsOn"), 8, "tope de luces");
            Shot("suministros_cerca", cam);
            // lejos (más de 15 m): sin marcador
            var far = (Component)spawn.Invoke(mgr, new object[] { LootItem.Ammo(AmmoType.Rifle, 5), player.transform.position + fwd * 19f + Vector3.up * 0.35f, "lejos" });
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsFalse((bool)P(far, "MarkerVisible"), "a 19 m no hay marcador");
            // recoger con E (sin pasar por encima): el objeto del medio
            var inv = player.GetComponent(T("SevenDays.Player.PlayerInventory"));
            int before = (int)P(mgr, "ActiveCount");
            spawned[7].GetType().GetMethod("Interact").Invoke(spawned[7], new object[] { inv });
            yield return null;
            Assert.AreEqual(before - 1, (int)P(mgr, "ActiveCount"), "recogido con E");
            for (int i = 0; i < 60; i++) yield return null;
            // de lejos (12 m) y con el jugador delante de la cámara: los marcadores siguen visibles (el jugador no tapa la línea de visión)
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position -= fwd * 4.5f;   // la cámara va ~3 m detrás: queda a unos 12 m de los objetos
            player.GetComponent<CharacterController>().enabled = true;
            for (int i = 0; i < 90; i++) yield return null;
            int shown = 0; for (int i = 0; i < items.Length; i++) if (i != 7 && (bool)P(spawned[i], "MarkerVisible")) shown++;
            Debug.Log("[Suministros] cámara a " + Vector3.Distance(cam.transform.position, spawned[4].transform.position).ToString("0.0") + " m, marcadores " + shown);
            Assert.GreaterOrEqual(shown, 6, "a ~12 m con el jugador delante siguen los marcadores");
            Shot("suministros_lejos", cam);
        }

        private static void Shot(string name, Camera cam)
        {
            string dir = Environment.GetEnvironmentVariable("ZOMBIE_SHOTS");
            if (string.IsNullOrEmpty(dir)) return;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = prev; RenderTexture.active = null; UnityEngine.Object.Destroy(tex); rt.Release();
        }
    }
}

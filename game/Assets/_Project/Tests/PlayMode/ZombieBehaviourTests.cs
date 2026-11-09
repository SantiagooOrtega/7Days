using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Los zombis con el cuerpo procedural en la arena real: siguen persiguiendo y golpeando al jugador, mueren de un headshot (cuerpo caído y sin cabeza), el pool reutiliza el cuerpo
    /// y lo deja limpio, el explosivo detona con su mecha, el lanzador lanza y cambiar de tipo con el pool lleno reconstruye el cuerpo.
    /// </summary>
    public class ZombieBehaviourTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private object _mgr; private GameObject _player; private GameObject _arena;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // cada prueba levanta su propia arena: se limpia (si no, los proyectiles de una prueba chocan con los pools de la siguiente)
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);   // el jugador vive fuera de la arena
            yield return null; yield return null;
        }

        private static float HipsY(object rig)
        {
            var pr = (Component)P(rig, "Rig");
            return ((Transform)pr.GetType().GetMethod("Bone").Invoke(pr, new object[] { BoneId.Hips })).position.y;
        }

        private IEnumerator Boot()
        {
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 240; i++) yield return null;
            _player = GameObject.FindWithTag("Player"); Assert.IsNotNull(_player);
            _mgr = T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null); Assert.IsNotNull(_mgr);
        }

        private Component Spawn(string id, Vector3 pos, bool ignoreCap = true)
        {
            var m = Array.Find(_mgr.GetType().GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            return (Component)m.Invoke(_mgr, new object[] { id, pos, 1f, 1f, 1f, ignoreCap });
        }

        private static object P(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private float PlayerHealth() { var h = P(_mgr, "PlayerHealth"); return (float)h.GetType().GetProperty("Current").GetValue(h); }

        private void Hit(Component zombie, HitZone zone, float amount)
        {
            var rig = P(zombie, "Rig");
            foreach (var hb in (Array)P(rig, "Hitboxes"))
            {
                if ((HitZone)hb.GetType().GetField("Zone").GetValue(hb) != zone) continue;
                var info = Activator.CreateInstance(T("SevenDays.Core.DamageInfo"));
                void Set(string f, object v) => info.GetType().GetField(f).SetValue(info, v);
                Set("Amount", amount); Set("Point", zombie.transform.position + Vector3.up); Set("Direction", -zombie.transform.forward);
                ((Component)hb).GetType().GetMethod("TakeDamage").Invoke(hb, new[] { info });
                return;
            }
            Assert.Fail("sin hitbox " + zone);
        }

        [UnityTest]
        public IEnumerator CommonZombie_ChasesAndHitsThePlayer_ThenDiesWithoutHead_AndPoolReusesTheBody()
        {
            yield return Boot();
            var fwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            float hp0 = PlayerHealth();
            var z = Spawn("common", _player.transform.position + fwd * 6f);
            Assert.IsNotNull(z);
            Assert.AreEqual("common", P(z, "BodyKey"));
            Assert.IsTrue((bool)P(z, "Procedural"));
            var rig = P(z, "Rig");
            Assert.IsTrue((bool)P(rig, "Emerging"), "empieza saliendo del suelo");
            Vector3 start = z.transform.position;
            for (int i = 0; i < 120; i++) yield return null;
            Assert.IsFalse((bool)P(rig, "Emerging"));
            for (int i = 0; i < 360; i++) yield return null;
            Assert.Less(PlayerHealth(), hp0, "el zombi llegó y golpeó al jugador");
            Assert.Greater(Vector3.Distance(start, z.transform.position), 2f, "se movió persiguiendo");
            // headshot: muere sin cabeza y queda en el suelo
            Hit(z, HitZone.Head, 5000f);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue((bool)P(z, "HeadDestroyed"));
            var rigT = (Component)P(rig, "Rig");
            var head = rigT.GetType().GetMethod("Bone").Invoke(rigT, new object[] { BoneId.Head }) as Transform;
            Assert.AreEqual(Vector3.zero, head.localScale, "la cabeza desaparece");
            for (int i = 0; i < 150; i++) yield return null;
            Assert.Less(HipsY(rig), 0.5f, "el cuerpo queda caído (no de pie)");
            // vuelve al pool y se reutiliza limpio
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < 20f && (bool)P(z, "InUse")) yield return null;
            Assert.IsFalse((bool)P(z, "InUse"));
            var z2 = Spawn("common", _player.transform.position + fwd * 12f);
            Assert.AreSame(z, z2, "el pool reutiliza el cuerpo del mismo tipo");
            Assert.AreEqual(Vector3.one, head.localScale, "cabeza restaurada");
            for (int i = 0; i < 160; i++) yield return null;
            Assert.IsFalse((bool)P(P(z2, "Health"), "IsDead"));
            Assert.Greater(HipsY(P(z2, "Rig")), 0.7f, "de pie otra vez");
        }

        [UnityTest]
        public IEnumerator Explosive_DetonatesWithItsFuse_AndSpitterThrows()
        {
            yield return Boot();
            var fwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            var ex = Spawn("explosive", _player.transform.position + fwd * 5f);
            Assert.IsNotNull(ex);
            float t0 = Time.realtimeSinceStartup;
            bool sawGlow = false;
            var rig = P(ex, "Rig");
            while (Time.realtimeSinceStartup - t0 < 25f && !(bool)P(P(ex, "Health"), "IsDead")) { yield return null; }
            Assert.IsTrue((bool)P(P(ex, "Health"), "IsDead"), "explotó con su mecha");
            _ = sawGlow; _ = rig;
            var sp = Spawn("spitter", _player.transform.position - fwd * 14f);
            Assert.IsNotNull(sp);
            int projectiles = 0;
            t0 = Time.realtimeSinceStartup;
            var projType = T("SevenDays.Combat.Projectile");
            while (Time.realtimeSinceStartup - t0 < 25f && projectiles == 0)
            {
                projectiles = UnityEngine.Object.FindObjectsByType(projType, FindObjectsSortMode.None).Length;
                yield return null;
            }
            Assert.Greater(projectiles, 0, "el lanzador lanzó un proyectil");
        }

        [UnityTest]
        public IEnumerator FullPool_ChangesBodyWhenTheTypeChanges()
        {
            yield return Boot();
            var mgrType = _mgr.GetType();
            mgrType.GetField("MaxZombies").SetValue(_mgr, 2);
            var fwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            var created = new List<Component>();
            for (int i = 0; i < 6; i++) { var z = Spawn("common", _player.transform.position + fwd * (10f + i), true); if (z != null && !created.Contains(z)) created.Add(z); }
            Assert.AreEqual(6, created.Count, "tope del pool: 2 + 4 extra");
            foreach (var z in created) Hit(z, HitZone.Head, 5000f);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 25f && created.Exists(c => (bool)P(c, "InUse"))) yield return null;
            var r = Spawn("runner", _player.transform.position + fwd * 12f, true);
            Assert.IsNotNull(r);
            Assert.AreEqual("runner", P(r, "BodyKey"), "el cuerpo se reconstruyó para el nuevo tipo");
            for (int i = 0; i < 120; i++) yield return null;
            Assert.IsTrue((bool)P(r, "InUse"));
        }
    }
}

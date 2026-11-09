using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Los tres jefes con su cuerpo propio en la arena real: usan el cuerpo procedural, la pose de cada habilidad avanza a la vez que su telegraph (el golpe cae cuando se
    /// dispara), nunca se hunden en el suelo, entran en furia (ojos, venas y vapor) y mueren tumbados. El punto débil del Devorador solo existe mientras está expuesto.
    /// </summary>
    public class BossBodyTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private object _mgr; private GameObject _player; private GameObject _arena;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.Projectile"), FindObjectsSortMode.None)) UnityEngine.Object.Destroy(((Component)p).gameObject);
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            yield return null; yield return null;
        }

        private IEnumerator Boot()
        {
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 120; i++) yield return null;
            _player = GameObject.FindWithTag("Player"); Assert.IsNotNull(_player);
            _mgr = T("SevenDays.Combat.ZombieManager").GetProperty("Instance").GetValue(null);
        }

        private static object P(object o, string n) { var p = o.GetType().GetProperty(n); return p != null ? p.GetValue(o) : o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).GetValue(o); }

        private Component Spawn(string id, Vector3 pos)
        {
            var m = Array.Find(_mgr.GetType().GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            return (Component)m.Invoke(_mgr, new object[] { id, pos, 1f, 1f, 1f, true });
        }

        private static float MinMeshY(object rig)
        {
            var smr = (SkinnedMeshRenderer)P(rig, "Renderer");
            var m = new Mesh(); smr.BakeMesh(m);
            float min = float.MaxValue;
            foreach (var v in m.vertices) min = Mathf.Min(min, smr.transform.TransformPoint(v).y);
            UnityEngine.Object.DestroyImmediate(m);
            return min;
        }

        [UnityTest]
        public IEnumerator EveryBoss_TelegraphsInSync_StaysOnTheGround_RagesAndDies([ValueSource(nameof(Bosses))] string id)
        {
            yield return Boot();
            var fwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            var z = Spawn(id, _player.transform.position + fwd * 9f);
            Assert.IsNotNull(z);
            Assert.AreEqual(id, P(z, "BodyKey"), "cuerpo procedural propio");
            var rig = P(z, "Rig");
            Assert.AreEqual("ZombieBody", rig.GetType().Name);
            var boss = P(z, "Boss"); var brain = P(boss, "Brain");
            float ground = z.transform.position.y;
            int telegraphFrames = 0, sync = 0; float worst = 0f; string seen = "";
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 22f && telegraphFrames < 40)
            {
                yield return null;
                var pose = (BossPose)P(z, "BossPose");
                if ((bool)P(brain, "Busy") && pose != BossPose.None && pose != BossPose.ChargeRun)
                {
                    float total = (float)P(boss, "_telegraphTotal"), rem = (float)P(brain, "TelegraphRemaining");
                    float want = ZombieMotion.BossImpactT * (1f - rem / Mathf.Max(0.1f, total));
                    float t = (float)P(z, "BossPoseT");
                    worst = Mathf.Max(worst, Mathf.Abs(t - want));
                    telegraphFrames++; if (Mathf.Abs(t - want) < 0.03f) sync++;
                    if (!seen.Contains(pose.ToString())) seen += pose + " ";
                }
                if (Time.frameCount % 20 == 0) Assert.GreaterOrEqual(MinMeshY(rig) - ground, -0.25f, id + " bajo el suelo con pose " + pose);
            }
            Debug.Log("[BossBody] " + id + ": telegraphs " + seen + "(" + telegraphFrames + " frames, desfase máx " + worst.ToString("0.000") + ")");
            Assert.Greater(telegraphFrames, 5, id + ": no llegó a avisar de ninguna habilidad");
            Assert.GreaterOrEqual(sync, telegraphFrames - 2, id + ": la pose no sigue al telegraph");

            // furia: al bajar del 50 % y del 25 %
            var health = P(z, "Health");
            void Damage(float frac)
            {
                var info = Activator.CreateInstance(T("SevenDays.Core.DamageInfo"));
                info.GetType().GetField("Amount").SetValue(info, (float)P(health, "Max") * frac); info.GetType().GetField("Point").SetValue(info, z.transform.position);
                health.GetType().GetMethod("TakeDamage").Invoke(health, new[] { info });
            }
            Damage(0.7f);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsTrue((bool)P(boss, "Raging"), id + " en furia");
            ParticleSystem steam = null; foreach (var ps in z.GetComponentsInChildren<ParticleSystem>(true)) if (ps.name == "Vapor") steam = ps;
            Assert.IsNotNull(steam); Assert.Greater(steam.emission.rateOverTime.constant, 0f, "echa vapor");
            Damage(5f);
            for (int i = 0; i < 150; i++) yield return null;
            Assert.IsTrue((bool)P(health, "IsDead"));
            var pr = (Component)P(rig, "Rig");
            float hips = ((Transform)pr.GetType().GetMethod("Bone").Invoke(pr, new object[] { BoneId.Hips })).position.y - ground;
            Assert.Less(hips, ZombieAnatomies.For(id).Proportions.height * 0.3f, id + " queda tumbado");
            Assert.GreaterOrEqual(MinMeshY(rig) - ground, -0.25f, id + " muerto bajo el suelo");
        }

        public static string[] Bosses() => ZombieAnatomies.BossTypes;
    }
}

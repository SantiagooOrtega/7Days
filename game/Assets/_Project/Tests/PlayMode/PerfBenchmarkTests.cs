using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Banco de rendimiento con muchos zombis: en los dos mapas, 30, 40 y 60 zombis persiguiendo al jugador (invulnerable), ms por sistema, frame medio,
    /// p95/p99, memoria por frame y recolecciones. Escribe la tabla en el log ("[Rendimiento]") y en el panel CPU de F6.
    /// </summary>
    public class PerfBenchmarkTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena;

        private string _prevMap;
        [SetUp] public void Remember() { _prevMap = (string)T("SevenDays.World.MapIO").GetField("SelectedId").GetValue(null); }

        [TearDown]
        public void Clean()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, _prevMap);   // no dejar elegido otro mapa a las pruebas siguientes
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var p in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(p);
        }

        public static IEnumerator Arena(string map, Action<GameObject> set)
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, map);
            var arena = new GameObject("Arena"); arena.AddComponent(T("SevenDays.World.TestArena"));
            set(arena);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop")) == null) yield return null;
            var loop = (Behaviour)UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop"));
            Assert.IsNotNull(loop, "arena lista");
            loop.enabled = false;   // sin oleadas propias: los zombis los pone la prueba
            var player = GameObject.FindWithTag("Player");
            var h = player.GetComponent(T("SevenDays.Core.Health"));
            var di = T("SevenDays.Core.DamageInfo"); var ft = typeof(Func<,>).MakeGenericType(di, typeof(float));
            var none = System.Linq.Expressions.Expression.Lambda(ft, System.Linq.Expressions.Expression.Constant(0f), System.Linq.Expressions.Expression.Parameter(di)).Compile();
            h.GetType().GetField("DamageScale").SetValue(h, none);   // jugador invulnerable durante la medición
            for (int i = 0; i < 60; i++) yield return null;
        }

        /// <summary>Pone n zombis comunes en anillos de 12 a 50 m alrededor del jugador, sobre el NavMesh.</summary>
        public static int Spawn(int n, int seed)
        {
            var zm = T("SevenDays.Combat.ZombieManager"); var mgr = zm.GetProperty("Instance").GetValue(null);
            zm.GetField("MaxZombies").SetValue(mgr, Math.Max(n, (int)zm.GetField("MaxZombies").GetValue(mgr)));
            var spawn = Array.Find(zm.GetMethods(), x => x.Name == "Spawn" && x.GetParameters()[0].ParameterType == typeof(string));
            var p = GameObject.FindWithTag("Player").transform.position;
            var rng = new System.Random(seed); int made = 0;
            for (int i = 0; i < n * 4 && made < n; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2), r = 12f + (float)rng.NextDouble() * 38f;
                var q = p + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!NavMesh.SamplePosition(q, out var hit, 3f, NavMesh.AllAreas)) continue;
                var z = spawn.Invoke(mgr, new object[] { "common", hit.position, 1f, 1f, 1f, true });
                if (z == null) continue;
                z.GetType().GetMethod("JoinHorde").Invoke(z, new object[] { (float)rng.NextDouble() * 1.5f, 1f });   // como en una oleada: todos van a por el jugador
                made++;
            }
            return made;
        }

        public static void Despawn()
        {
            var zm = T("SevenDays.Combat.ZombieManager"); var mgr = zm.GetProperty("Instance").GetValue(null);
            zm.GetMethod("DespawnAll").Invoke(mgr, null);
        }

        public static IEnumerator Measure(int frames, Action<object> done)
        {
            var mon = T("SevenDays.Core.PerfMonitor").GetMethod("Ensure").Invoke(null, null);
            var zm = T("SevenDays.Combat.ZombieManager"); var mgr = zm.GetProperty("Instance").GetValue(null);
            int fr = Application.targetFrameRate, vs = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;   // sin tope: el frame mide el coste real, no la espera a 60 FPS
            mon.GetType().GetMethod("BeginWindow").Invoke(mon, null);
            for (int i = 0; i < frames; i++) yield return null;
            done(mon.GetType().GetMethod("EndWindow").Invoke(mon, new object[] { (int)zm.GetProperty("ActiveCount").GetValue(mgr) }));
            Application.targetFrameRate = fr; QualitySettings.vSyncCount = vs;
        }

        public static string Row(object rep)
        {
            var rt = rep.GetType();
            var secs = (float[])rt.GetField("Sections").GetValue(rep);
            var sb = new StringBuilder((string)rt.GetMethod("Line").Invoke(rep, null));
            var name = rt.GetMethod("Name", BindingFlags.Public | BindingFlags.Static);
            var bytes = (float[])rt.GetField("SectionBytes").GetValue(rep);
            for (int i = 0; i < secs.Length; i++)
            {
                sb.Append("\n    ").Append((string)name.Invoke(null, new object[] { i })).Append(": ").Append(secs[i].ToString("0.000")).Append(" ms");
                if (bytes != null && i < bytes.Length) sb.Append(", ").Append(bytes[i].ToString("0")).Append(" B/frame");
            }
            return sb.ToString();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Benchmark_30_40_60_Zombies_BothMaps([Values("calle_en_ruinas", "bosque_oscuro")] string map)
        {
            yield return Arena(map, a => _arena = a);
            var sb = new StringBuilder("[Rendimiento] " + map + "\n");
            object rep = null;
            yield return Measure(120, r => rep = r);
            sb.Append("  base: ").Append(Row(rep)).Append('\n');
            foreach (int n in new[] { 30, 40, 60 })
            {
                int made = Spawn(n, n * 7);
                for (int i = 0; i < 150; i++) yield return null;   // terminan de emerger y persiguen
                yield return Measure(300, r => rep = r);
                sb.Append("  ").Append(Row(rep)).Append('\n');
                var zs = T("SevenDays.Combat.ZombieStats"); var snap = zs.GetMethod("Collect").Invoke(null, null);
                var desc = Array.Find(zs.GetMethods(), m => m.Name == "Describe");
                int chasing = 0, emerging = 0;
                foreach (Component z in UnityEngine.Object.FindObjectsByType(T("SevenDays.Combat.ZombieController"), FindObjectsSortMode.None))
                {
                    var st = z.GetType().GetProperty("State")?.GetValue(z)?.ToString();
                    if (st == "Chase" || st == "Attack") chasing++;
                    var rig = z.GetType().GetProperty("Rig").GetValue(z); if ((bool)(rig.GetType().GetProperty("Emerging")?.GetValue(rig) ?? false)) emerging++;
                }
                sb.Append("    estado: ").Append(desc.Invoke(null, new[] { snap })).Append(" · persiguen ").Append(chasing).Append(", emergiendo ").Append(emerging).Append(" · cámara ").Append(Camera.main != null).Append('\n');
                Despawn();
                for (int i = 0; i < 30; i++) yield return null;
            }
            Debug.Log(sb.ToString());
        }
    }
}

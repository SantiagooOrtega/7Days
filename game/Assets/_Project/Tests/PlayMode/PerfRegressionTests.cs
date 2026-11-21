using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Rendimiento con 40 zombis en horda, en los dos mapas: graba frames con el Profiler del editor y comprueba que NINGÚN método del juego reserva memoria
    /// cada frame (GC.Alloc), y que la IA y el NavMesh cuestan poco. Se permiten, porque no son por frame: la carga en segundo plano de texturas, mallas y
    /// sonidos (CharacterArtLoader, termina sola), las ventanas de los edificios (cada 1.3 s) y la estrella fugaz (cada ~25 s).
    /// </summary>
    public class PerfRegressionTests
    {
        private GameObject _arena;
        private string _prevMap;
        private static System.Reflection.FieldInfo MapField => System.Reflection.Assembly.Load("Assembly-CSharp").GetType("SevenDays.World.MapIO").GetField("SelectedId");
        [SetUp] public void Remember() { _prevMap = (string)MapField.GetValue(null); }
        [TearDown] public void Clean() { MapField.SetValue(null, _prevMap); if (_arena != null) UnityEngine.Object.Destroy(_arena); foreach (var p in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(p); }

        [UnityTest, Timeout(600000)]
        public IEnumerator Zombies40_NoPerFrameAllocations_LowAiCost([Values("calle_en_ruinas", "bosque_oscuro")] string map)
        {
            yield return PerfBenchmarkTests.Arena(map, a => _arena = a);
            PerfBenchmarkTests.Spawn(40, 3);
            for (int i = 0; i < 200; i++) yield return null;
            object rep = null;
            yield return PerfBenchmarkTests.Measure(120, r => rep = r);
            var secs = (float[])rep.GetType().GetField("Sections").GetValue(rep);
            int nav = (int)Assembly.Load("Assembly-CSharp").GetType("SevenDays.Core.PerfProbe").GetField("Count").GetValue(null) + 1;
            Debug.Log("[Rendimiento 40] " + map + ": " + PerfBenchmarkTests.Row(rep));
            Assert.Less(secs[0], 1.0f, "IA de 40 zombis (C#) por debajo de 1 ms");
            Assert.Less(secs[nav], 1.0f, "NavMesh del motor por debajo de 1 ms");
            var ed = Assembly.Load("UnityEditor.CoreModule");
            var pd = ed.GetType("UnityEditorInternal.ProfilerDriver");
            pd.GetProperty("profileEditor").SetValue(null, false);
            pd.GetProperty("enabled").SetValue(null, true);
            UnityEngine.Profiling.Profiler.enabled = true;
            for (int i = 0; i < 40; i++) yield return null;
            int last = (int)pd.GetProperty("lastFrameIndex").GetValue(null);
            var viewModes = ed.GetType("UnityEditor.Profiling.HierarchyFrameDataView+ViewModes");
            var hv = ed.GetType("UnityEditor.Profiling.HierarchyFrameDataView");
            int gcCol = (int)hv.GetField("columnGcMemory").GetValue(null);
            var get = Array.Find(pd.GetMethods(), m => m.Name == "GetHierarchyFrameDataView" && m.GetParameters().Length == 5);
            var agg = new Dictionary<string, float>();
            for (int f = last - 30; f <= last - 1; f++)
            {
                var view = get.Invoke(null, new object[] { f, 0, Enum.ToObject(viewModes, 0), gcCol, false });
                if (view == null || !(bool)hv.GetProperty("valid").GetValue(view)) continue;
                var children = hv.GetMethod("GetItemChildren", new[] { typeof(int), typeof(List<int>) });
                var name = hv.GetMethod("GetItemName", new[] { typeof(int) });
                var col = hv.GetMethod("GetItemColumnDataAsFloat", new[] { typeof(int), typeof(int) });
                void Walk(int id, string path, int depth)
                {
                    var l = new List<int>(); children.Invoke(view, new object[] { id, l });
                    foreach (int c in l)
                    {
                        string n = (string)name.Invoke(view, new object[] { c });
                        if (n == "GC.Alloc") { float b = (float)col.Invoke(view, new object[] { c, gcCol }); agg.TryGetValue(path, out float v); agg[path] = v + b; continue; }
                        if ((float)col.Invoke(view, new object[] { c, gcCol }) <= 0f) continue;
                        if (depth < 14) Walk(c, path.Length > 0 ? path + " > " + n : n, depth + 1);
                    }
                }
                Walk((int)hv.GetMethod("GetRootItemID").Invoke(view, null), "", 0);
                ((IDisposable)view).Dispose();
            }
            pd.GetProperty("enabled").SetValue(null, false);
            UnityEngine.Profiling.Profiler.enabled = false;   // no dejarlo activo para las pruebas siguientes
            var list = new List<KeyValuePair<string, float>>(agg); list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder("[Alloc] " + map + " (bytes en 30 frames)\n");
            for (int i = 0; i < Math.Min(25, list.Count); i++) sb.Append(list[i].Value.ToString("0")).Append("  ").Append(list[i].Key).Append('\n');
            Debug.Log(sb.ToString());
            var bad = new List<string>();
            foreach (var kv in list)
            {
                if (!kv.Key.Contains("Assembly-CSharp.dll!")) continue;   // motor y framework de pruebas
                if (kv.Value <= 256f) continue;   // menos de 8 B/frame en 30 frames: un evento suelto (VIGÍA encola una frase), no memoria por frame
                if (kv.Key.Contains("CharacterArtLoader") || kv.Key.Contains("SkylineLights") || kv.Key.Contains("NightSkyRenderer")) continue;   // carga en segundo plano y periódicos
                bad.Add(kv.Value.ToString("0") + " B en " + kv.Key.Substring(kv.Key.LastIndexOf('!') + 1));
            }
            Assert.IsEmpty(bad, "sin memoria por frame en el código del juego:\n" + string.Join("\n", bad));
        }
    }
}

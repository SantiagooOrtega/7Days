using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Textura de la casa estable entre días y al reintentar: se mide la escala de la textura del muro (tiling del material, tamaño e identidad de la textura y
    /// UV por metro de la malla) el día 1, en el día 3 y tras "Reintentar" (destruir la arena y descargar los assets sin usar, como hace cargar la escena).
    /// </summary>
    public class HouseTextureTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);


        /// <summary>Firma de todas las texturas de la casa: por pieza, la textura base y la de normales (instancia y tamaño), el tiling y la escala UV por metro.</summary>
        private static System.Collections.Generic.SortedDictionary<string, string> Measure()
        {
            var d = new System.Collections.Generic.SortedDictionary<string, string>();
            var house = GameObject.Find("House"); Assert.IsNotNull(house, "casa");
            int n = 0;
            foreach (var r in house.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial; if (m == null || !m.HasProperty("_BaseMap")) continue;
                var bt = m.GetTexture("_BaseMap"); var nt = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                var mf = r.GetComponent<MeshFilter>(); var mesh = mf != null ? mf.sharedMesh : null;
                float uvm = 0f;
                if (mesh != null) { var b = mesh.bounds.size; var uv = mesh.uv; float a = float.MaxValue, z = float.MinValue; foreach (var u in uv) { a = Mathf.Min(a, u.x); z = Mathf.Max(z, u.x); } uvm = (z - a) / Mathf.Max(0.01f, Mathf.Max(b.x, Mathf.Max(b.y, b.z))); }
                string key = r.name + "@" + r.transform.localPosition.ToString("0.00");
                d[key] = (bt != null ? bt.GetInstanceID() + "/" + bt.width : "sin") + " " + (nt != null ? nt.GetInstanceID() + "/" + nt.width : "sin") + " " + m.GetTextureScale("_BaseMap").ToString("0.00") + " " + uvm.ToString("0.000");
                if (r.name.StartsWith("Wall")) { Assert.IsNotNull(bt, key + ": muro sin textura (cayó al color plano)"); n++; }
            }
            Assert.Greater(n, 10, "muros medidos");
            return d;
        }

        private IEnumerator Build(GameObject[] holder)
        {
            holder[0] = new GameObject("Arena"); holder[0].AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 20; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator HouseWall_SameTextureScale_Day1_Day3_AndAfterRetry()
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, "calle_en_ruinas");
            var h = new GameObject[1];
            yield return Build(h);
            var d1 = Measure();
            var house = (Component)UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.HouseController"));
            house.GetType().GetMethod("SetDay").Invoke(house, new object[] { 3 });
            yield return null;
            var d3 = Measure();
            // reintentar: la escena se recarga y Unity descarga lo que no usa
            UnityEngine.Object.Destroy(h[0]); foreach (var p in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(p);
            yield return null; yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return Build(h);
            var retry = Measure();
            int diff3 = 0, diffR = 0; string ex = "";
            foreach (var kv in d1)
            {
                if (!d3.TryGetValue(kv.Key, out var v3) || v3 != kv.Value) diff3++;
                if (!retry.TryGetValue(kv.Key, out var vr) || vr != kv.Value) { diffR++; if (ex.Length < 300) ex += kv.Key + ": " + kv.Value + " -> " + vr + "; "; }
            }
            Debug.Log("[Textura casa] " + d1.Count + " piezas; distintas en el día 3: " + diff3 + ", tras reintentar: " + diffR + " " + ex);
            Assert.AreEqual(0, diff3, "día 3 igual que día 1");
            Assert.AreEqual(0, diffR, "tras reintentar, las mismas texturas (sin regenerar) y la misma escala: " + ex);
            UnityEngine.Object.Destroy(h[0]);
            foreach (var p in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(p);
            yield return null;
        }
    }
}

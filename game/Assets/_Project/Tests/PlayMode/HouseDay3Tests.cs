using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Casa opaca en el día 3 (bug: se veía transparente). Se juega de verdad hasta el día 3 con el salto de fase (tecla N) en los 2 mapas y se compara
    /// con el día 1 cada renderer de la casa: activo, material (instancia), shader, cola de render, Cull, tipo de superficie, keywords, malla y texturas.
    /// También se pasan los 4 niveles de calidad (el gobernador baja nivel con el jefe del día 3) y se comprueba que no tocan los materiales.
    /// </summary>
    public class HouseDay3Tests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private GameObject _arena;

        [TearDown]
        public void Clean()
        {
            if (_arena != null) UnityEngine.Object.Destroy(_arena);
            foreach (var p in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(p);
        }

        public static string[] Maps() => new[] { "calle_en_ruinas", "bosque_oscuro" };

        /// <summary>Firma de cada renderer de todas las casas del mapa.</summary>
        public static SortedDictionary<string, string> Snapshot(out int opaqueWalls)
        {
            var d = new SortedDictionary<string, string>();
            opaqueWalls = 0;
            var houses = UnityEngine.Object.FindObjectsByType(T("SevenDays.World.HouseController"), FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            Assert.Greater(houses.Length, 0, "casas");
            int h = 0;
            foreach (Component house in houses)
            {
                h++;
                foreach (var r in house.GetComponentsInChildren<Renderer>(true))
                {
                    var m = r.sharedMaterial;
                    string key = h + "/" + r.name + "@" + r.transform.localPosition.ToString("0.00");
                    while (d.ContainsKey(key)) key += "'";
                    var mf = r.GetComponent<MeshFilter>(); var mesh = mf != null ? mf.sharedMesh : null;
                    string sig = (r.gameObject.activeInHierarchy ? "on" : "off") + (r.enabled ? "" : " deshabilitado") + " " + r.shadowCastingMode
                        + " mesh " + (mesh != null ? mesh.GetInstanceID() + "/" + mesh.vertexCount + "/" + mesh.triangles.Length : "NULA");
                    if (m == null) sig += " SIN MATERIAL";
                    else
                    {
                        var kw = new List<string>(m.shaderKeywords); kw.Sort();
                        var bt = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        sig += " mat " + m.GetInstanceID() + " " + m.shader.name + " q" + m.renderQueue
                            + " cull " + (m.HasProperty("_Cull") ? m.GetFloat("_Cull") : -1f)
                            + " surf " + (m.HasProperty("_Surface") ? m.GetFloat("_Surface") : -1f)
                            + " zw " + (m.HasProperty("_ZWrite") ? m.GetFloat("_ZWrite") : -1f)
                            + " a " + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a.ToString("0.00") : "-")
                            + " tag " + m.GetTag("RenderType", false)
                            + " tex " + (bt != null ? bt.GetInstanceID() + "/" + bt.width : "sin")
                            + " kw " + string.Join(",", kw);
                        // regla dura: nada de la casa es transparente ni de una cara (las piezas son cajas cerradas)
                        bool transparent = m.renderQueue >= 2450 || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
                            || m.GetTag("RenderType", false) == "Transparent" || (m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").a < 0.99f);
                        bool isFire = r.GetComponent<ParticleSystem>() != null || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer;
                        if (!isFire)
                        {
                            Assert.IsFalse(transparent, key + ": material transparente en la casa (" + sig + ")");
                            if (r.name.StartsWith("Wall") || r.name.StartsWith("Roof")) { Assert.AreEqual(2f, m.GetFloat("_Cull"), key + ": Cull Back"); Assert.IsNotNull(mesh, key); opaqueWalls++; }
                        }
                    }
                    d[key] = sig;
                }
            }
            return d;
        }

        private static int Diff(SortedDictionary<string, string> a, SortedDictionary<string, string> b, out string ex)
        {
            int n = 0; ex = "";
            foreach (var kv in a)
            {
                if (kv.Key.Contains("Barricade") || kv.Key.Contains("/Furniture")) continue;   // tablones del director y muebles (rompibles a propósito)   // los tablones del director se activan a propósito
                if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) { n++; if (ex.Length < 600) ex += kv.Key + ": " + kv.Value + "  ->  " + (v ?? "FALTA") + "\n"; }
            }
            foreach (var kv in b) if (!kv.Key.Contains("Barricade") && !kv.Key.Contains("/Furniture") && !a.ContainsKey(kv.Key)) { n++; if (ex.Length < 600) ex += "NUEVO " + kv.Key + ": " + kv.Value + "\n"; }
            return n;
        }

        [UnityTest]
        public IEnumerator House_SameMaterialsCullAndRenderers_Day1VsDay3([ValueSource(nameof(Maps))] string map)
        {
            T("SevenDays.World.MapIO").GetField("SelectedId").SetValue(null, map);
            _arena = new GameObject("Arena"); _arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var d1 = Snapshot(out int walls);
            Assert.Greater(walls, 10, "muros y techos medidos");

            var loop = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.World.DayLoop"));
            Assert.IsNotNull(loop, "DayLoop");
            var cycle = loop.GetType().GetProperty("Cycle").GetValue(loop);
            var dayP = cycle.GetType().GetProperty("Day"); var phaseP = cycle.GetType().GetProperty("Phase");
            var skip = loop.GetType().GetMethod("DebugSkip");
            // N hasta el día 3, jugando un rato la oleada de cada día (sale el jefe del día 3)
            t0 = Time.realtimeSinceStartup;
            while ((int)dayP.GetValue(cycle) < 3 && Time.realtimeSinceStartup - t0 < 90f)
            {
                skip.Invoke(loop, null);
                for (int i = 0; i < 20; i++) yield return null;
            }
            Assert.AreEqual(3, (int)dayP.GetValue(cycle), "llegó al día 3");
            if (phaseP.GetValue(cycle).ToString() == "Preparation") skip.Invoke(loop, null);   // oleada del día 3, con el jefe
            for (int i = 0; i < 120; i++) yield return null;
            // lo que hace el jefe: carga y pisotón (fuerza 0), lanzamiento (1) y proyectil que rompe coberturas (Smash directo) sobre toda la casa
            int furniture = 0;
            foreach (Component house in UnityEngine.Object.FindObjectsByType(T("SevenDays.World.HouseController"), FindObjectsSortMode.None))
                foreach (var dst in house.GetComponentsInChildren(T("SevenDays.World.Destructible")))
                {
                    var dt = dst.GetType();
                    dt.GetMethod("TrySmash").Invoke(dst, new object[] { 0, dst.transform.position, Vector3.forward });
                    dt.GetMethod("TrySmash").Invoke(dst, new object[] { 1, dst.transform.position, Vector3.forward });
                    dt.GetMethod("Smash").Invoke(dst, new object[] { dst.transform.position, Vector3.forward });
                    if ((bool)dt.GetProperty("Broken").GetValue(dst)) { furniture++; Assert.IsTrue(dst.name.StartsWith("Furniture"), dst.name + ": solo los muebles se rompen"); }
                }
            yield return null;
            var d3 = Snapshot(out _);
            int n3 = Diff(d1, d3, out string ex3);
            T("SevenDays.World.Destructible").GetMethod("RestoreAll").Invoke(null, null);

            // los 4 niveles de calidad (el gobernador los cambia solo si el FPS cae con el jefe)
            int nq = 0; string exq = "";
            var qd = UnityEngine.Object.FindFirstObjectByType(T("SevenDays.Look.QualityDirector"));
            if (qd != null)
            {
                var setMode = qd.GetType().GetMethod("SetMode"); var modeT = T("SevenDays.Look.QualityMode");
                var prev = qd.GetType().GetProperty("Mode").GetValue(qd);
                foreach (var name in new[] { "Baja", "Media", "Alta", "Auto" })
                {
                    setMode.Invoke(qd, new[] { Enum.Parse(modeT, name) });
                    for (int i = 0; i < 5; i++) yield return null;
                    nq += Diff(d3, Snapshot(out _), out string e); if (exq.Length < 600) exq += e;
                }
                setMode.Invoke(qd, new[] { prev });
            }
            Debug.Log("[Casa día 3] " + map + ": " + d1.Count + " renderers; distintos en el día 3 tras los ataques del jefe: " + n3 + " (muebles rotos: " + furniture + ")" + ", al cambiar de calidad: " + nq + "\n" + ex3 + exq);
            Assert.AreEqual(0, n3, map + ": la casa del día 3 debe ser idéntica a la del día 1\n" + ex3);
            Assert.AreEqual(0, nq, map + ": los niveles de calidad no cambian los materiales de la casa\n" + exq);
        }
    }
}

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// "Foto" del mapa construido (se ejecuta a mano): cada objeto del nivel con su ruta, posición, giro, escala, material (nombre, color, textura, desplazamiento de UV),
    /// collider y luz; la niebla y el ambiente; el botín, la casa y el jugador. Sirve para comprobar que el mapa 1 leído del JSON sale IDÉNTICO al de antes del refactor:
    /// se guarda en la ruta de la variable de entorno MAP_SNAPSHOT y se comparan los dos archivos.
    /// </summary>
    [Explicit, Category("Mapas")]
    public class MapSnapshot
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        private static string C(Color c) => F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a);

        [UnityTest]
        public IEnumerator Snapshot()
        {
            var arena = new GameObject("Arena"); arena.AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var sb = new StringBuilder();
            var level = GameObject.Find("Level");
            Assert.IsNotNull(level);
            foreach (var t in level.GetComponentsInChildren<Transform>(true)) Dump(sb, t, level.transform);
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.Append("LUZ ").Append(Path(l.transform, null)).Append(' ').Append(l.type).Append(' ').Append(V(l.transform.position)).Append(' ').Append(C(l.color)).Append(' ').Append(F(l.intensity)).Append(' ').Append(F(l.range)).Append(' ').Append(l.shadows).Append('\n');
            sb.Append("NIEBLA ").Append(RenderSettings.fog).Append(' ').Append(RenderSettings.fogMode).Append(' ').Append(C(RenderSettings.fogColor)).Append(' ').Append(F(RenderSettings.fogDensity)).Append('\n');
            sb.Append("AMBIENTE ").Append(C(RenderSettings.ambientSkyColor)).Append(' ').Append(C(RenderSettings.ambientEquatorColor)).Append(' ').Append(C(RenderSettings.ambientGroundColor)).Append('\n');
            var arenaT = arena.GetComponent(T("SevenDays.World.TestArena"));
            foreach (var fld in new[] { "_layout" }) { var f = arenaT.GetType().GetField(fld, BindingFlags.NonPublic | BindingFlags.Instance); if (f != null) sb.Append("CAPA ").Append(fld).Append(' ').Append(JsonUtility.ToJson(f.GetValue(arenaT))).Append('\n'); }
            var house = GameObject.Find("House");
            if (house != null) foreach (var t in house.GetComponentsInChildren<Transform>(true)) Dump(sb, t, null);
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) if (r.transform.root.name != "Level" && r.transform.root.name != "Player" && r.transform.root != arena.transform) sb.Append("FUERA ").Append(Path(r.transform, null)).Append(' ').Append(V(r.transform.position)).Append('\n');
            var player = GameObject.FindWithTag("Player");
            sb.Append("JUGADOR ").Append(V(player.transform.position)).Append(' ').Append(F(player.transform.eulerAngles.y)).Append('\n');
            var lm = T("SevenDays.World.Loot.LootManager").GetProperty("Instance").GetValue(null);
            var layout = lm.GetType().GetProperty("Layout").GetValue(lm);
            sb.Append("BOTIN ").Append(JsonUtility.ToJson(layout)).Append('\n');
            sb.Append("BOTIN ACTIVO ").Append(lm.GetType().GetProperty("ActiveCount").GetValue(lm)).Append(" contenedores ").Append(lm.GetType().GetProperty("ContainerCount").GetValue(lm)).Append('\n');
            int atmosphere = 0; foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) atmosphere++;
            sb.Append("PARTICULAS ").Append(atmosphere).Append('\n');
            string path = Environment.GetEnvironmentVariable("MAP_SNAPSHOT") ?? System.IO.Path.Combine(Application.dataPath, "..", "map_snapshot.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[MapSnapshot] " + path + " (" + sb.Length + " caracteres)");
            UnityEngine.Object.Destroy(arena);
            foreach (var pl in GameObject.FindGameObjectsWithTag("Player")) UnityEngine.Object.Destroy(pl);
        }

        private static string Path(Transform t, Transform root)
        {
            var s = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        private static void Dump(StringBuilder sb, Transform t, Transform root)
        {
            sb.Append(Path(t, root)).Append(" | ").Append(V(t.position)).Append(" | ").Append(V(t.eulerAngles)).Append(" | ").Append(V(t.lossyScale)).Append(" | ").Append(t.gameObject.activeSelf ? "on" : "off");
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                sb.Append(" | R ").Append(r.shadowCastingMode);
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { sb.Append(" null"); continue; }
                    sb.Append(' ').Append(m.shader.name).Append(':').Append(m.HasProperty("_BaseColor") ? C(m.GetColor("_BaseColor")) : "").Append(':').Append(m.mainTexture != null ? m.mainTexture.name : "-");
                }
            }
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { var uv = mf.sharedMesh.uv; sb.Append(" | M ").Append(mf.sharedMesh.vertexCount).Append(uv.Length > 0 ? " uv0 " + F(uv[0].x) + "," + F(uv[0].y) : ""); }
            foreach (var c in t.GetComponents<Collider>()) sb.Append(" | C ").Append(c.GetType().Name).Append(' ').Append(c.isTrigger ? "trigger " : "").Append(V(c.bounds.size));
            foreach (var mb in t.GetComponents<MonoBehaviour>()) if (mb != null) sb.Append(" | ").Append(mb.GetType().Name);
            sb.Append('\n');
        }
    }
}

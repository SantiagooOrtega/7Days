using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using UnityEngine.TestTools;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Prueba de carga con 30 zombis en la arena real (no es de aprobado/suspenso: escribe una tabla en el log). Mide el frame completo (CPU + GPU, con la resolución dinámica
    /// y el gobernador de calidad actuales) sin zombis y con 30, el coste de los rigs, el reparto de LOD y los triángulos dibujados. Se ejecuta a mano (es [Explicit]).
    /// </summary>
    [Explicit, Category("Rendimiento")]
    public class ZombieLoadTest
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);

        /// <summary>ms de un render completo de la cámara a 1920x1080 esperando a la GPU (mediana de 30), como en RenderBenchmark.</summary>
        private static float RenderMs(Camera cam)
        {
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture; cam.targetTexture = rt;
            for (int i = 0; i < 12; i++) { cam.Render(); AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion(); }
            var samples = new List<float>(30); var w = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < 30; i++) { w.Restart(); cam.Render(); AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion(); w.Stop(); samples.Add((float)w.Elapsed.TotalMilliseconds); }
            cam.targetTexture = prev; rt.Release(); UnityEngine.Object.Destroy(rt);
            samples.Sort(); return samples[samples.Count / 2];
        }

        private static IEnumerator Measure(int frames, List<float> mainMs, List<float> cpu, List<float> gpu)
        {
            using (var rec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
            {
                var timings = new FrameTiming[1];
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    if (rec.Valid && rec.LastValue > 0) mainMs.Add(rec.LastValue / 1e6f);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0) { if (timings[0].cpuFrameTime > 0) cpu.Add((float)timings[0].cpuFrameTime); if (timings[0].gpuFrameTime > 0) gpu.Add((float)timings[0].gpuFrameTime); }
                }
            }
        }

        private static string Avg(List<float> l) { if (l.Count == 0) return "n/d"; float a = 0f; foreach (var v in l) a += v; return (a / l.Count).ToString("0.00") + " ms"; }

        private static float Median(List<float> l) { l.Sort(); return l[l.Count / 2]; }

        private static IEnumerator Frames(int n, List<float> ms)
        {
            for (int i = 0; i < n; i++) { yield return null; ms.Add(Time.unscaledDeltaTime * 1000f); }
        }

        private static string Stats(List<float> ms)
        {
            var s = new List<float>(ms); s.Sort();
            float avg = 0f; foreach (var v in s) avg += v; avg /= s.Count;
            return "media " + avg.ToString("0.00") + " ms (" + (1000f / avg).ToString("0") + " FPS)  mediana " + s[s.Count / 2].ToString("0.00") + "  p95 " + s[(int)(s.Count * 0.95f)].ToString("0.00") + "  peor " + s[s.Count - 1].ToString("0.00");
        }

        [UnityTest]
        public IEnumerator Thirty_Zombies()
        {
            new GameObject("Arena").AddComponent(T("SevenDays.World.TestArena"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 30f && GameObject.FindWithTag("Player") == null) yield return null;
            for (int i = 0; i < 300; i++) yield return null;
            var player = GameObject.FindWithTag("Player"); Assert.IsNotNull(player);
            var mgrType = T("SevenDays.Combat.ZombieManager");
            var mgr = mgrType.GetProperty("Instance").GetValue(null); Assert.IsNotNull(mgr);
            // sin daño al jugador: solo se mide el coste
            var ph = mgrType.GetProperty("PlayerHealth").GetValue(mgr) as Component;
            ph.GetType().GetMethod("SetMax").Invoke(ph, new object[] { 1000000f, true });
            var sb = new System.Text.StringBuilder("[Benchmark zombis] " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")\n");

            var cam = Camera.main;
            var baseline = new List<float>(); yield return Frames(120, baseline);
            var bMain = new List<float>(); var bCpu = new List<float>(); var bGpu = new List<float>(); yield return Measure(120, bMain, bCpu, bGpu);
            float bRender = RenderMs(cam);
            sb.Append("Sin zombis:   ").Append(Stats(baseline)).Append("  | hilo principal ").Append(Avg(bMain)).Append("  CPU frame ").Append(Avg(bCpu)).Append("  GPU frame ").Append(Avg(bGpu)).Append("  | render 1080p ").Append(bRender.ToString("0.00")).Append(" ms\n");

            // 30 zombis delante de la cámara: mezcla parecida a la del director
            mgrType.GetField("MaxZombies").SetValue(mgr, 40);
            var spawn = mgrType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var spawnM = Array.Find(spawn, m => m.Name == "Spawn" && m.GetParameters()[0].ParameterType == typeof(string));
            string[] mix = { "common", "common", "common", "common", "common", "common", "common", "common", "common", "common", "common", "common", "common", "common",
                             "runner", "runner", "runner", "runner", "runner", "runner", "tank", "tank", "armored", "armored", "armored", "spitter", "spitter", "explosive", "explosive", "common" };
            var rng = new System.Random(5);
            int made = 0;
            for (int i = 0; i < mix.Length; i++)
            {
                float ang = ((float)rng.NextDouble() - 0.5f) * 100f * Mathf.Deg2Rad, dist = 10f + (float)rng.NextDouble() * 22f;
                Vector3 dir = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, Vector3.up) * Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                var z = spawnM.Invoke(mgr, new object[] { mix[i], player.transform.position + dir * dist, 1f, 1f, 1f, true });
                if (z != null) made++;
            }
            sb.Append("Zombis creados: " + made + " de " + mix.Length + "\n");
            for (int i = 0; i < 150; i++) yield return null;   // salen del suelo
            var withZ = new List<float>(); yield return Frames(300, withZ);
            var zMain = new List<float>(); var zCpu = new List<float>(); var zGpu = new List<float>(); yield return Measure(180, zMain, zCpu, zGpu);
            float zRender = RenderMs(cam);
            sb.Append("Con 30 zombis:  ").Append(Stats(withZ)).Append("  | hilo principal ").Append(Avg(zMain)).Append("  CPU frame ").Append(Avg(zCpu)).Append("  GPU frame ").Append(Avg(zGpu)).Append("  | render 1080p ").Append(zRender.ToString("0.00")).Append(" ms\n");
            sb.Append("Coste de 30 zombis: render +" + (zRender - bRender).ToString("0.00") + " ms\n");

            // reparto: LOD, triángulos, coste de los rigs
            var all = (System.Collections.IEnumerable)mgrType.GetProperty("All").GetValue(mgr);
            int[] lods = new int[3]; long tris = 0; int bodies = 0; int active = 0;
            foreach (var z in all)
            {
                bool inUse = (bool)z.GetType().GetProperty("InUse").GetValue(z);
                if (!inUse) continue; active++;
                var rig = z.GetType().GetProperty("Rig").GetValue(z);
                if (rig.GetType().Name != "ZombieBody") continue;
                bodies++;
                int lod = (int)rig.GetType().GetProperty("Lod").GetValue(rig); lods[lod]++;
                var smr = (SkinnedMeshRenderer)rig.GetType().GetProperty("Renderer").GetValue(rig);
                if (smr.enabled && smr.sharedMesh != null) tris += smr.sharedMesh.GetIndexCount(0) / 3 + smr.sharedMesh.GetIndexCount(1) / 3;
            }
            float rigMs = 0f; int rigs = 0;
            var rigType = T("SevenDays.Animation.ProceduralRig");
            foreach (var r in (System.Collections.IEnumerable)rigType.GetField("Active").GetValue(null)) { rigMs += (float)rigType.GetProperty("AverageCostMs").GetValue(r) / Mathf.Max(1, (int)rigType.GetProperty("UpdateInterval").GetValue(r)); rigs++; }
            sb.Append("Activos " + active + " (procedurales " + bodies + "): LOD0 " + lods[0] + " / LOD1 " + lods[1] + " / LOD2 " + lods[2] + ", triángulos de zombis ~" + tris / 1000 + "k\n");
            sb.Append("Coste medio de los rigs (todos, ms por frame): " + rigMs.ToString("0.00") + " con " + rigs + " rigs\n");
            var art = T("SevenDays.Animation.ZombieArt");
            sb.Append("Texturas de zombi: " + ((long)art.GetProperty("TextureBytes").GetValue(null) / 1048576f).ToString("0.0") + " MB (" + art.GetProperty("TexturesReady").GetValue(null) + " mapas)  mallas: " + ((long)T("SevenDays.Animation.ZombieMeshes").GetProperty("MeshBytes").GetValue(null) / 1048576f).ToString("0.0") + " MB\n");
            // física: coste de sincronizar los colliders que se mueven con la animación (30 zombis con ~18 colliders cada uno)
            var sw = new System.Diagnostics.Stopwatch(); float syncMs = 0f; int syncN = 0;
            for (int i = 0; i < 60; i++) { yield return null; sw.Restart(); Physics.SyncTransforms(); sw.Stop(); syncMs += (float)sw.Elapsed.TotalMilliseconds; syncN++; }
            sb.Append("Physics.SyncTransforms con 30 zombis: " + (syncMs / syncN).ToString("0.00") + " ms por frame\n");
            // ¿dónde se va el coste? apagando cada cosa de los zombis por separado
            var smrs = new List<SkinnedMeshRenderer>(); var decals = new List<MeshRenderer>();
            foreach (var z in all)
            {
                if (!(bool)z.GetType().GetProperty("InUse").GetValue(z)) continue;
                var zc = (Component)z;
                smrs.AddRange(zc.GetComponentsInChildren<SkinnedMeshRenderer>());
                foreach (var m in zc.GetComponentsInChildren<MeshRenderer>()) if (m.name.StartsWith("Herida")) decals.Add(m);
            }
            sb.Append("Desglose del render de 30 zombis (1080p, mejor de 3):\n");
            float Best() { float b = float.MaxValue; for (int i = 0; i < 3; i++) b = Mathf.Min(b, RenderMs(cam)); return b; }
            float full = Best();
            sb.Append("  completo:                 " + full.ToString("0.00") + " ms\n");
            var shadowModes = new List<UnityEngine.Rendering.ShadowCastingMode>(); foreach (var r in smrs) shadowModes.Add(r.shadowCastingMode);
            foreach (var r in smrs) r.shadowCastingMode = ShadowCastingMode.Off;
            sb.Append("  sin sombras de zombis:    " + Best().ToString("0.00") + " ms\n");
            for (int i = 0; i < smrs.Count; i++) smrs[i].shadowCastingMode = shadowModes[i];
            var decalOn = new List<bool>(); foreach (var d in decals) decalOn.Add(d.enabled);
            foreach (var d in decals) d.enabled = false;
            sb.Append("  sin heridas (decals):     " + Best().ToString("0.00") + " ms\n");
            for (int i = 0; i < decals.Count; i++) decals[i].enabled = decalOn[i];
            foreach (var r in smrs) r.enabled = false;
            sb.Append("  sin dibujar zombis:       " + Best().ToString("0.00") + " ms  (activos con decals " + decalOn.FindAll(x => x).Count + ")\n");
            foreach (var r in smrs) r.enabled = true;
            Debug.Log(sb.ToString());
        }
    }
}

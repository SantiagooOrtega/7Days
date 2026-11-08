using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>Armas reales (GunParts con sus mallas): presupuesto, piezas animadas, y que el punto de mira (Sight -> SightPoint del ADS) cae en la línea de mira.</summary>
    public class WeaponArtTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown() { foreach (var g in _created) if (g != null) UnityEngine.Object.DestroyImmediate(g); _created.Clear(); }

        public static IEnumerable<string> Ids() { foreach (var w in WeaponCatalog.Defaults()) yield return w.id; }

        private Component Gun(WeaponData w)
        {
            var holder = new GameObject("Soporte");
            _created.Add(holder);
            return (Component)Game.GetType("SevenDays.Weapons.GunParts", true).GetMethod("Build").Invoke(null, new object[] { w, holder.transform });
        }

        [TestCaseSource(nameof(Ids))]
        public void Gun_WithinBudget_SightOnLineOfSight(string id)
        {
            var w = WeaponCatalog.Defaults().Find(x => x.id == id);
            var gun = Gun(w);
            int tris = 0;
            foreach (var mf in gun.GetComponentsInChildren<MeshFilter>()) tris += (int)mf.sharedMesh.GetIndexCount(0) / 3;
            Debug.Log("[Arma] " + id + ": " + tris + " triángulos");
            Assert.LessOrEqual(tris, 3000, id + ": presupuesto del arma");
            if (w.IsMelee) return;
            var sight = (Transform)gun.GetType().GetField("Sight").GetValue(gun);
            Assert.IsNotNull(sight, "punto de mira");
            bool optic = (bool)gun.GetType().GetField("HasOptic").GetValue(gun);
            Vector3 a, b;
            if (optic && gun.transform.Find("Ocular") != null) { a = gun.transform.Find("Ocular").position; b = gun.transform.Find("Lente").position; }
            else if (optic) { a = gun.transform.Find("Lente").position + gun.transform.forward * -0.047f; b = gun.transform.Find("Punto").position; }
            else
            {
                // alza y guion: la línea pasa por la parte alta de las dos
                var rear = gun.transform.Find("Alza").GetComponent<Renderer>().bounds; var front = gun.transform.Find("Guion").GetComponent<Renderer>().bounds;
                a = new Vector3(rear.center.x, rear.max.y, rear.center.z); b = new Vector3(front.center.x, front.max.y, front.center.z);
            }
            Vector3 dir = (b - a).normalized, off = Vector3.ProjectOnPlane(sight.position - a, dir);
            Assert.Less(off.magnitude, 0.002f, id + ": el SightPoint se sale de la línea de mira " + (off.magnitude * 1000f).ToString("0.0") + " mm");
            Assert.Less(Vector3.Dot(sight.position - a, dir), 0f, id + ": el punto de mira va detrás de la mira (lado del ojo)");
        }

        [TestCaseSource(nameof(Ids))]
        public void AnimatedParts_StillMove(string id)
        {
            var w = WeaponCatalog.Defaults().Find(x => x.id == id);
            var gun = Gun(w);
            var t = gun.GetType();
            foreach (var (field, method) in new[] { ("Slide", "SetSlide"), ("Pump", "SetPump"), ("Bolt", "SetBolt") })
            {
                var part = (Transform)t.GetField(field).GetValue(gun);
                if (part == null) continue;
                Vector3 p0 = part.localPosition; Quaternion r0 = part.localRotation;
                t.GetMethod(method).Invoke(gun, new object[] { 1f });
                Assert.IsTrue((part.localPosition - p0).magnitude > 0.005f || Quaternion.Angle(part.localRotation, r0) > 5f, id + ": " + field + " no se mueve");
                t.GetMethod(method).Invoke(gun, new object[] { 0f });
                Assert.Less((part.localPosition - p0).magnitude, 1e-4f, id + ": " + field + " no vuelve a su sitio");
            }
            var mag = (Transform)t.GetField("Mag").GetValue(gun);
            if (mag != null)
            {
                Vector3 p0 = mag.localPosition;
                t.GetMethod("SetMag").Invoke(gun, new object[] { 0.2f, false });
                Assert.Greater((mag.localPosition - p0).magnitude, 0.1f, "el cargador sale");
                foreach (var r in mag.GetComponentsInChildren<Renderer>()) Assert.IsFalse(r.enabled, "fuera, no se ve (con su acento)");
                t.GetMethod("SetMag").Invoke(gun, new object[] { 0f, true });
            }
        }
    }
}

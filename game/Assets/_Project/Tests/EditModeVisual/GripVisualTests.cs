using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>
    /// Agarre medido en las mallas reales (soldado + arma GunParts) con el mismo código que el juego (WeaponGripSolver), vía WeaponScenario y GripProbe
    /// (Assembly-CSharp, por reflexión). Recorre todas las armas: cadera, apuntar, disparar, recargar, bajar el arma corriendo, cobertura y sacarla.
    /// </summary>
    public class GripVisualTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private readonly List<object> _created = new List<object>();

        [TearDown]
        public void TearDown() { foreach (var s in _created) s.GetType().GetMethod("Destroy").Invoke(s, null); _created.Clear(); }

        private object Create(WeaponData w)
        {
            var sc = T("SevenDays.Animation.WeaponScenario").GetMethod("Create").Invoke(null, new object[] { w, Vector3.zero, null });
            _created.Add(sc);
            return sc;
        }

        private static void SetStep(object sc, string step) => sc.GetType().GetMethod("SetStep").Invoke(sc, new[] { Enum.Parse(T("SevenDays.Animation.GripStep"), step) });
        private static void Tick(object sc, float dt) => sc.GetType().GetMethod("Tick").Invoke(sc, new object[] { dt });
        private static void Run(object sc, float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds * 60f); i++) Tick(sc, 1f / 60f); }
        private static string Line(object sc) => (string)T("SevenDays.Animation.GripProbe").GetMethod("Line").Invoke(null, new[] { sc });

        public static IEnumerable<WeaponData> Weapons() => WeaponCatalog.Defaults();

        private static float F(object reading, string field) => Convert.ToSingle(reading.GetType().GetField(field).GetValue(reading));
        private static object Measure(object sc) => T("SevenDays.Animation.GripProbe").GetMethod("Measure", new[] { T("SevenDays.Animation.WeaponScenario") }).Invoke(null, new[] { sc });
        private static Component Rig(object sc) => (Component)sc.GetType().GetProperty("Rig").GetValue(sc);
        private static Transform Pose(object sc) => (Transform)sc.GetType().GetProperty("Pose").GetValue(sc);

        public static IEnumerable<string> Ids()
        {
            foreach (var w in WeaponCatalog.Defaults()) yield return w.id;
            yield return "bat"; yield return "axe"; yield return "shovel";
        }

        /// <summary>Arma por id: las del catálogo, o bate/hacha/pala (aún no están en el botín: se prueban sus agarres con datos de prueba).</summary>
        private static WeaponData Make(string id)
        {
            var w = WeaponCatalog.Defaults().Find(x => x.id == id);
            if (w != null) return w;
            float len = id == "shovel" ? 1f : (id == "axe" ? 0.8f : 0.85f), th = id == "bat" ? 0.06f : 0.045f;
            w = new WeaponData { id = id, displayName = id, kind = WeaponKind.Melee, ammoType = AmmoType.None, length = len, thickness = th, color = new[] { 0.4f, 0.32f, 0.22f }, roundsPerMinute = 90f, range = 2f };
            w.grip = WeaponGrip.For(w.kind, w.length, w.thickness);
            return w;
        }

        private static void AssertGrip(object sc, string where, bool checkError = true, float fingerLimit = 0.015f)
        {
            var r = Measure(sc);
            if (checkError && F(r, "RightWeight") > 0.95f) Assert.LessOrEqual(F(r, "RightError"), 0.02f, where + ": mano derecha lejos de su punto de agarre");
            if (checkError && F(r, "LeftWeight") > 0.95f) Assert.LessOrEqual(F(r, "LeftError"), 0.02f, where + ": mano de apoyo lejos de su punto de agarre");
            Assert.LessOrEqual(F(r, "FingerPenetration"), fingerLimit, where + ": los dedos atraviesan el arma (" + r.GetType().GetField("FingerCulprit").GetValue(r) + ")");
            Assert.LessOrEqual(F(r, "TorsoPenetration"), 0.01f, where + ": el arma atraviesa el torso");
            Assert.LessOrEqual(F(r, "StockPenetration"), 0.035f, where + ": la culata se hunde en el torso");
            if (F(r, "RightWeight") > 0.95f)
            {
                Assert.That(F(r, "ElbowFlexRight"), Is.InRange(4f, 150f), where + ": codo derecho hiperextendido o plegado de más");
                Assert.GreaterOrEqual(F(r, "ElbowDownRight"), -0.1f, where + ": codo derecho hacia arriba");
            }
            if (F(r, "LeftWeight") > 0.95f)
            {
                Assert.That(F(r, "ElbowFlexLeft"), Is.InRange(4f, 150f), where + ": codo izquierdo hiperextendido o plegado de más");
                Assert.GreaterOrEqual(F(r, "ElbowDownLeft"), -0.1f, where + ": codo izquierdo hacia arriba");
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void SteadyHolds_HandsOnGripPoints_FingersAndWeaponDontPenetrate_ElbowsDown(string id)
        {
            var w = Make(id);
            var sc = Create(w);
            foreach (var step in new[] { "Hip", "Aim", "Fire", "LowReady", "Cover" })
            {
                SetStep(sc, step);
                Run(sc, 1.2f);
                // disparando con bomba o cerrojo la mano viaja a la bomba/cerrojo: ahí se comprueba que no atraviesa, no la distancia
                bool cycling = step == "Fire" && (w.kind == WeaponKind.Shotgun || w.kind == WeaponKind.SniperRifle);
                for (int i = 0; i < 20; i++) { Tick(sc, 1f / 60f); AssertGrip(sc, id + " " + step + " frame " + i, !cycling); }
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void Reload_SupportHandNeverCrossesTheWeapon(string id)
        {
            var w = Make(id);
            if (w.IsMelee) Assert.Pass("sin recarga");
            var sc = Create(w);
            SetStep(sc, "Hip"); Run(sc, 0.8f);
            SetStep(sc, "Reload");
            int frames = Mathf.CeilToInt((w.reloadTime * 2.2f + 0.5f) * 60f);
            // en tránsito el dedo (1.9 cm de grueso) puede rozar 2 cm; atravesar el arma serían varios centímetros
            for (int i = 0; i < frames; i++) { Tick(sc, 1f / 60f); if (i % 2 == 0) AssertGrip(sc, id + " recarga frame " + i, false, 0.02f); }
            Run(sc, 0.6f);
            AssertGrip(sc, id + " tras la recarga");
        }

        /// <summary>Transiciones: sacar, cadera, apuntar, disparar, recargar, correr, cobertura y volver. Ninguna mano ni el arma saltan entre frames.</summary>
        [TestCaseSource(nameof(Ids))]
        public void Transitions_AreContinuous(string id)
        {
            var w = Make(id);
            var sc = Create(w);
            Transform root = Rig(sc).transform;
            var bones = new[] { Bone(sc, BoneId.RightHand), Bone(sc, BoneId.LeftHand) };
            Vector3[] last = null; Vector3 lastPose = Vector3.zero; Quaternion[] lastRot = null;
            float maxHand = 0f, maxPose = 0f, maxRot = 0f; string where = "";
            var steps = w.IsMelee ? new[] { "Draw", "Hip", "Melee", "LowReady", "Cover", "HeavyMelee", "Hip" } : new[] { "Draw", "Hip", "Aim", "Fire", "Reload", "LowReady", "Cover", "Hip" };
            foreach (var step in steps)
            {
                SetStep(sc, step);
                float secs = step == "Reload" ? w.reloadTime * 2.2f + 0.3f : 0.9f;
                for (int i = 0; i < Mathf.CeilToInt(secs * 60f); i++)
                {
                    Tick(sc, 1f / 60f);
                    // las manos se miden RESPECTO AL ARMA: un golpe mueve el arma muy rápido (y las manos con ella), pero ninguna mano debe saltar sobre ella
                    Transform wpT = Pose(sc);
                    var now = new[] { wpT.InverseTransformPoint(bones[0].position), wpT.InverseTransformPoint(bones[1].position) };
                    var rot = new[] { Quaternion.Inverse(wpT.rotation) * bones[0].rotation, Quaternion.Inverse(wpT.rotation) * bones[1].rotation };
                    Vector3 pose = root.InverseTransformPoint(Pose(sc).position);
                    if (last != null)
                        for (int h = 0; h < 2; h++)
                        {
                            float d = (now[h] - last[h]).magnitude, a = Quaternion.Angle(rot[h], lastRot[h]);
                            if (d > maxHand) { maxHand = d; where = step + " " + i + (h == 0 ? " der" : " izq"); }
                            maxRot = Mathf.Max(maxRot, a);
                        }
                    // el arma solo se comprueba fuera de los golpes: un golpe la mueve muy rápido a propósito (ahí cuenta que las manos no salten sobre ella)
                    if (last != null && step != "Melee" && step != "HeavyMelee" && i > 0) maxPose = Mathf.Max(maxPose, (pose - lastPose).magnitude);
                    last = now; lastRot = rot; lastPose = pose;
                }
            }
            Debug.Log("[Transiciones] " + id + ": mano " + (maxHand * 100f).ToString("0.0") + " cm/frame (" + where + "), giro " + maxRot.ToString("0") + "°/frame, arma " + (maxPose * 100f).ToString("0.0") + " cm/frame");
            Assert.LessOrEqual(maxHand, 0.06f, id + ": una mano salta sobre el arma entre frames en " + where);
            Assert.LessOrEqual(maxRot, 12f, id + ": una mano gira de golpe");
            Assert.LessOrEqual(maxPose, 0.08f, id + ": el arma salta entre frames");
        }

        private static Transform Bone(object sc, BoneId b) => (Transform)Rig(sc).GetType().GetMethod("Bone").Invoke(Rig(sc), new object[] { b });

        [TestCase("bar", true), TestCase("bat", true), TestCase("axe", true), TestCase("shovel", false)]
        public void Melee_TwoHandsAtRest_SupportReleasesInTheStrike(string id, bool releases)
        {
            var sc = Create(Make(id));
            SetStep(sc, "Hip"); Run(sc, 1f);
            AssertGrip(sc, id + " en reposo");
            Assert.Greater(F(Measure(sc), "LeftWeight"), 0.95f, id + ": a dos manos en reposo");
            SetStep(sc, "Melee");
            float minLeft = 1f;
            for (int i = 0; i < 30; i++) { Tick(sc, 1f / 60f); minLeft = Mathf.Min(minLeft, F(Measure(sc), "LeftWeight")); }
            if (releases) Assert.Less(minLeft, 0.05f, id + ": la mano de apoyo suelta en el golpe");
            else Assert.Greater(minLeft, 0.95f, id + ": la pala empuja con las dos manos");
            Run(sc, 1f);
            Assert.Greater(F(Measure(sc), "LeftWeight"), 0.95f, id + ": la mano vuelve tras el golpe");
            AssertGrip(sc, id + " tras el golpe");
        }

        [Test]
        public void Soldier_HasNoBackpack_AndIsSlim()
        {
            var sc = Create(WeaponCatalog.Defaults()[0]);
            var rig = Rig(sc);
            Assert.AreEqual(0, (int)T("SevenDays.Animation.GripProbe").GetMethod("BackpackParts").Invoke(null, new object[] { rig }), "no debe quedar ninguna pieza de mochila");
            // silueta del torso (pecho y chaleco) en el espacio del cuerpo, quieto
            Run(sc, 0.5f);
            float minX = 9f, maxX = -9f, minZ = 9f, maxZ = -9f;
            foreach (var b in new[] { BoneId.Chest, BoneId.UpperChest })
                foreach (var mf in Bone(sc, b).GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.transform.parent != Bone(sc, b)) continue;
                    foreach (var v in mf.sharedMesh.vertices) { Vector3 p = rig.transform.InverseTransformPoint(mf.transform.TransformPoint(v)); minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
                }
            Assert.LessOrEqual(maxX - minX, 0.42f, "torso ancho: debe ser atlético, no corpulento");
            Assert.LessOrEqual(maxZ - minZ, 0.3f, "chaleco grueso: debe ser ligero");
        }

        [Test]
        public void Report_GripPerWeaponAndStep()
        {
            var sb = new System.Text.StringBuilder("[Informe agarre]\n");
            foreach (var w in WeaponCatalog.Defaults())
            {
                var sc = Create(w);
                foreach (var step in new[] { "Hip", "Aim", "Fire", "Reload", "LowReady", "Cover" })
                {
                    SetStep(sc, step);
                    Run(sc, step == "Reload" ? w.reloadTime * 0.5f : 1.5f);
                    if (step == "Reload") { sb.Append(Line(sc)).Append('\n'); Run(sc, w.reloadTime * 3f); }
                    sb.Append(Line(sc)).Append('\n');
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}

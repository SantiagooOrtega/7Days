using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>
    /// Pruebas de lo que SE VE (no de la matemática): instancian el soldado con sus mallas reales, lo ponen en cada fase de la zancada, arrodillado y agachado,
    /// y miden las mallas (Renderer.bounds y vértices transformados). El rig vive en Assembly-CSharp, que un asmdef no puede referenciar, así que se llama por
    /// reflexión a RigScenario (crear y avanzar) y las medidas se hacen aquí con la API de Unity, sin usar el código del rig que se prueba.
    /// Suelo en y = 0 (sin collider: el rig usa la altura del cuerpo).
    /// </summary>
    public class LegVisualTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string name) => Game.GetType(name, true);
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown() { foreach (var g in _created) if (g != null) UnityEngine.Object.DestroyImmediate(g); _created.Clear(); }

        // ------------------------------------------------------------------ utilidades (reflexión mínima: crear, avanzar, leer)
        private Component Create()
        {
            var rig = (Component)T("SevenDays.Animation.RigScenario").GetMethod("CreateSoldier").Invoke(null, new object[] { Vector3.zero, null });
            _created.Add(rig.gameObject);
            return rig;
        }

        private static object Mode(string m) => Enum.Parse(T("SevenDays.Animation.RigScenarioMode"), m);
        private static void Simulate(Component rig, string mode, float s) => T("SevenDays.Animation.RigScenario").GetMethod("Simulate").Invoke(null, new[] { rig, Mode(mode), (object)s });
        private static void Step(Component rig, string mode) => T("SevenDays.Animation.RigScenario").GetMethod("Step").Invoke(null, new[] { rig, Mode(mode) });
        private static bool ToPhase(Component rig, string mode, int foot, float phase)
            => (bool)T("SevenDays.Animation.RigScenario").GetMethod("SimulateToFootPhase").Invoke(null, new[] { rig, Mode(mode), (object)foot, (object)phase });
        private static Transform Bone(Component rig, BoneId id) => (Transform)rig.GetType().GetMethod("Bone").Invoke(rig, new object[] { id });
        private static float Stance(Component rig) => (float)rig.GetType().GetProperty("StanceFraction").GetValue(rig);
        private static float IkKnee(Component rig, int f) => ((float[])rig.GetType().GetProperty("KneeFlexion").GetValue(rig))[f];

        private static BoneId UpperLeg(int f) => f == 0 ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg;
        private static BoneId LowerLeg(int f) => f == 0 ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg;
        private static BoneId FootBone(int f) => f == 0 ? BoneId.LeftFoot : BoneId.RightFoot;

        /// <summary>Pieza principal de un hueso ("&lt;hueso&gt; limb").</summary>
        private static Transform Limb(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++) if (bone.GetChild(i).name.EndsWith(" limb")) return bone.GetChild(i);
            Assert.Fail("sin malla principal en " + bone.name);
            return null;
        }

        private static Vector3 Centroid(Transform t)
        {
            var v = t.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 s = Vector3.zero; foreach (var p in v) s += p;
            return t.TransformPoint(s / v.Length);
        }

        /// <summary>Flexión de la rodilla medida en las MALLAS: ángulo entre el eje visible del muslo y el de la pantorrilla.</summary>
        private static float MeshKnee(Component rig, int f)
        {
            Transform hip = Bone(rig, UpperLeg(f)), knee = Bone(rig, LowerLeg(f));
            return Vector3.Angle(Centroid(Limb(hip)) - hip.position, Centroid(Limb(knee)) - knee.position);
        }

        /// <summary>Vértice más bajo (exacto) y Renderer.bounds.min.y más bajo de todas las piezas de una pierna.</summary>
        private static void Lowest(Component rig, int f, out float vertexY, out float boundsY)
        {
            vertexY = boundsY = float.MaxValue;
            foreach (var mf in Bone(rig, UpperLeg(f)).GetComponentsInChildren<MeshFilter>())
            {
                foreach (var v in mf.sharedMesh.vertices) vertexY = Mathf.Min(vertexY, mf.transform.TransformPoint(v).y);
                boundsY = Mathf.Min(boundsY, mf.GetComponent<Renderer>().bounds.min.y);
            }
        }

        /// <summary>+1 si la rodilla visible queda delante de la línea cadera-tobillo, -1 si queda detrás.</summary>
        private static float KneeForward(Component rig, int f)
        {
            Vector3 h = Bone(rig, UpperLeg(f)).position, k = Bone(rig, LowerLeg(f)).position, a = Bone(rig, FootBone(f)).position;
            Vector3 off = Vector3.ProjectOnPlane(k - h, (a - h).normalized);
            return off.magnitude < 0.01f ? 1f : Mathf.Sign(Vector3.Dot(off, rig.transform.forward));
        }

        /// <summary>Inclinación del torso hacia delante (grados): de la cadera a la base del cuello respecto a la vertical.</summary>
        private static float TorsoLean(Component rig)
        {
            Vector3 d = Bone(rig, BoneId.Neck).position - Bone(rig, BoneId.Hips).position;
            return Mathf.Atan2(Vector3.Dot(d, rig.transform.forward), d.y) * Mathf.Rad2Deg;
        }

        private static float SegmentFromVertical(Vector3 a, Vector3 b) => Vector3.Angle(a - b, Vector3.up);

        private void AssertLegVisible(Component rig, string where)
        {
            for (int f = 0; f < 2; f++)
            {
                Lowest(rig, f, out float vy, out float by);
                Assert.GreaterOrEqual(vy, -0.003f, where + ": un vértice de la pierna " + f + " queda " + (-vy * 100f).ToString("0.0") + " cm bajo el suelo");
                Assert.GreaterOrEqual(by, -0.03f, where + ": Renderer.bounds de la pierna " + f + " muy por debajo del suelo");
                float ik = IkKnee(rig, f), mesh = MeshKnee(rig, f);
                Assert.AreEqual(ik, mesh, 4f, where + ": la rodilla " + f + " de las MALLAS (" + mesh.ToString("0") + "°) no coincide con la del IK (" + ik.ToString("0") + "°)");
                if (mesh > 10f) Assert.AreEqual(1f, KneeForward(rig, f), where + ": la rodilla " + f + " apunta hacia atrás");
            }
        }

        // ------------------------------------------------------------------ pruebas
        [TestCase("Walk", 0.0f), TestCase("Walk", 0.3f), TestCase("Walk", 0.58f), TestCase("Walk", 0.8f)]
        [TestCase("Jog", 0.0f), TestCase("Jog", 0.25f), TestCase("Jog", 0.5f), TestCase("Jog", 0.75f)]
        [TestCase("Run", 0.0f), TestCase("Run", 0.12f), TestCase("Run", 0.26f), TestCase("Run", 0.6f)]
        [TestCase("RunWeapon", 0.0f), TestCase("RunWeapon", 0.6f)]
        public void Gait_EachPhase_MeshesAboveGround_KneeMatchesBone_PointsForward(string mode, float phase)
        {
            var rig = Create();
            Assert.IsTrue(ToPhase(rig, mode, 0, phase), "no llegó a la fase");
            AssertLegVisible(rig, mode + " fase " + phase);
        }

        [TestCase("Walk", 50f, 70f)]
        [TestCase("Run", 100f, 120f)]
        public void Gait_SwingKneePeak_IsInRange(string mode, float min, float max)
        {
            var rig = Create();
            Simulate(rig, mode, 2f);
            float peak = 0f;
            for (int i = 0; i < 120; i++) { Step(rig, mode); peak = Mathf.Max(peak, MeshKnee(rig, 0)); }
            Assert.That(peak, Is.InRange(min, max), mode + ": pico de la rodilla en el vuelo (mallas)");
        }

        [Test]
        public void Run_ContactKnee_IsAbout15()
        {
            var rig = Create();
            Assert.IsTrue(ToPhase(rig, "Run", 0, 0.01f));
            Assert.That(MeshKnee(rig, 0), Is.InRange(8f, 22f), "rodilla al contactar corriendo (~15°)");
        }

        [Test]
        public void Run_PelvisKeepsHeight_OnlyBounces4To6cm()
        {
            var rig = Create();
            Simulate(rig, "Idle", 2f);
            float stand = Bone(rig, BoneId.Hips).position.y;
            Simulate(rig, "Run", 2f);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 120; i++) { Step(rig, "Run"); float y = Bone(rig, BoneId.Hips).position.y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            Assert.LessOrEqual(stand - hi, 0.02f, "corriendo, la cadera en su punto alto debe estar casi a la altura de pie");
            Assert.That(hi - lo, Is.InRange(0.035f, 0.065f), "rebote de la pelvis corriendo");
        }

        [TestCase("Jog", 5f, 8f)]
        [TestCase("Run", 8f, 12f)]
        [TestCase("RunWeapon", 3f, 6f)]
        public void RunLean_IsSlight(string mode, float min, float max)
        {
            var rig = Create();
            Simulate(rig, mode, 2.5f);
            float sum = 0f; int n = 0;
            for (int i = 0; i < 120; i++) { Step(rig, mode); float l = TorsoLean(rig); Assert.LessOrEqual(l, 15f, "nunca más de 15°"); sum += l; n++; }
            Assert.That(sum / n, Is.InRange(min, max), mode + ": inclinación media del torso");
        }

        [Test]
        public void RunLean_HeadStaysLevel()
        {
            var rig = Create();
            Simulate(rig, "Run", 2.5f);
            Vector3 f = Bone(rig, BoneId.Head).forward;
            float pitch = Mathf.Asin(Mathf.Clamp(-f.y, -1f, 1f)) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.InRange(-6f, 6f), "la cabeza compensa: la mirada queda horizontal");
        }

        [Test]
        public void HalfKneel_FrontKnee90_ShinVertical_RearKneeNearGround_TorsoForward()
        {
            var rig = Create();
            Simulate(rig, "HalfKneel", 3f);
            AssertLegVisible(rig, "media rodilla");
            int rear = 1, front = 0;   // KneelSide = +1: la derecha atrás
            Assert.That(MeshKnee(rig, front), Is.InRange(80f, 100f), "rodilla delantera ~90°");
            Assert.LessOrEqual(SegmentFromVertical(Bone(rig, LowerLeg(front)).position, Bone(rig, FootBone(front)).position), 12f, "espinilla delantera vertical");
            float footPitch = Vector3.Angle(Vector3.ProjectOnPlane(Bone(rig, FootBone(front)).forward, rig.transform.right), rig.transform.forward);
            Assert.LessOrEqual(footPitch, 8f, "pie delantero plano");
            float rearKnee = Bone(rig, LowerLeg(rear)).position.y;
            Assert.That(rearKnee, Is.InRange(0.03f, 0.08f), "rodilla trasera a 3-8 cm del suelo");
            Assert.GreaterOrEqual(SegmentFromVertical(Bone(rig, LowerLeg(rear)).position, Bone(rig, FootBone(rear)).position), 65f, "espinilla trasera casi horizontal");
            Transform toes = Bone(rig, rear == 1 ? BoneId.RightToes : BoneId.LeftToes);
            float toeLow = float.MaxValue;
            foreach (var mf in toes.GetComponentsInChildren<MeshFilter>()) foreach (var v in mf.sharedMesh.vertices) toeLow = Mathf.Min(toeLow, mf.transform.TransformPoint(v).y);
            Assert.LessOrEqual(toeLow, 0.015f, "la punta del pie trasero apoyada");
            Assert.That(TorsoLean(rig), Is.InRange(25f, 35f), "torso adelante");
        }

        [Test]
        public void Squat_BothKnees90To120()
        {
            var rig = Create();
            Simulate(rig, "Squat", 2f);
            AssertLegVisible(rig, "sentadilla");
            for (int f = 0; f < 2; f++) Assert.That(MeshKnee(rig, f), Is.InRange(90f, 120f), "rodilla " + f + " en sentadilla");
        }

        [Test]
        public void F7Cycle_WithTransitions_EveryFrameVisible()
        {
            var rig = Create();
            foreach (var mode in new[] { "Walk", "Run", "RunWeapon", "HalfKneel", "Squat", "Walk" })
                for (int i = 0; i < 180; i++)
                {
                    Step(rig, mode);
                    if (i % 3 == 0) AssertLegVisible(rig, mode + " frame " + i);
                }
        }

        /// <summary>No comprueba nada nuevo: escribe en el log los ángulos finales medidos en las mallas (para el informe y para comparar con el clip F7).</summary>
        [Test]
        public void Report_FinalAngles()
        {
            var sb = new System.Text.StringBuilder("[Informe piernas]\n");
            foreach (var mode in new[] { "Walk", "Jog", "Run", "RunWeapon" })
            {
                var rig = Create();
                Simulate(rig, "Idle", 2f);
                float stand = Bone(rig, BoneId.Hips).position.y;
                Simulate(rig, mode, 2.5f);
                float peak = 0f, low = 999f, lean = 0f, lo = 99f, hi = -99f, clear = 99f; int n = 120;
                for (int i = 0; i < n; i++)
                {
                    Step(rig, mode);
                    float k = MeshKnee(rig, 0); peak = Mathf.Max(peak, k); low = Mathf.Min(low, k);
                    lean += TorsoLean(rig);
                    float y = Bone(rig, BoneId.Hips).position.y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                    for (int f = 0; f < 2; f++) { Lowest(rig, f, out float vy, out _); clear = Mathf.Min(clear, vy); }
                }
                var c = Create(); ToPhase(c, mode, 0, 0.01f);
                sb.AppendFormat("{0}: rodilla {1:0}-{2:0}° (contacto {3:0}°), torso {4:0.0}°, pelvis alta {5:+0.0;-0.0} cm vs de pie, rebote {6:0.0} cm, apoyo {7:0}%, bota-suelo mín {8:+0.0;-0.0} cm\n",
                    mode, low, peak, MeshKnee(c, 0), lean / n, (hi - stand) * 100f, (hi - lo) * 100f, Stance(rig) * 100f, clear * 100f);
            }
            var k1 = Create(); Simulate(k1, "HalfKneel", 3f);
            sb.AppendFormat("Media rodilla: delantera {0:0}°, espinilla delantera {1:0}° de la vertical, rodilla trasera a {2:0.0} cm, espinilla trasera {3:0}° de la horizontal, torso {4:0.0}°\n",
                MeshKnee(k1, 0), SegmentFromVertical(Bone(k1, LowerLeg(0)).position, Bone(k1, FootBone(0)).position), Bone(k1, LowerLeg(1)).position.y * 100f,
                90f - SegmentFromVertical(Bone(k1, LowerLeg(1)).position, Bone(k1, FootBone(1)).position), TorsoLean(k1));
            var sq = Create(); Simulate(sq, "Squat", 2f);
            sb.AppendFormat("Sentadilla: rodillas {0:0}° y {1:0}°", MeshKnee(sq, 0), MeshKnee(sq, 1));
            Debug.Log(sb.ToString());
        }

        [Test]
        public void Standing_MeshesAboveGround_KneeMatchesBone()
        {
            var rig = Create();
            Simulate(rig, "Idle", 2f);
            AssertLegVisible(rig, "de pie");
        }
    }
}

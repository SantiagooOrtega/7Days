using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>
    /// La unión brazo-torso con el soldado REAL en la pose que se ve en el juego (el IK del brazo libre en reposo, y apuntando con un arma): mallas de Unity cortadas con
    /// planos horizontales, huecos entre brazo y torso en centímetros, y los brazos contra las piezas del chaleco (orientadas, no solo cajas alineadas).
    /// </summary>
    public class BodyFitTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private readonly List<object> _created = new List<object>();

        [TearDown]
        public void TearDown() { foreach (var s in _created) s.GetType().GetMethod("Destroy").Invoke(s, null); _created.Clear(); }

        public static IEnumerable<string> VariantIds() { foreach (var v in CharacterVariants.Defaults()) yield return v.id; }

        private object Weapon(string weaponId, string variantId)
        {
            var lib = T("SevenDays.Animation.CharacterVariantLibrary");
            lib.GetMethod("SetCurrent").Invoke(null, new object[] { variantId });
            var w = WeaponCatalog.Defaults().Find(x => x.id == weaponId);
            var sc = T("SevenDays.Animation.WeaponScenario").GetMethod("Create").Invoke(null, new object[] { w, Vector3.zero, null });
            _created.Add(sc);
            return sc;
        }

        private static void Step(object sc, string step) => sc.GetType().GetMethod("SetStep").Invoke(sc, new[] { Enum.Parse(T("SevenDays.Animation.GripStep"), step) });
        private static void Run(object sc, float s) { for (int i = 0; i < Mathf.CeilToInt(s * 60f); i++) sc.GetType().GetMethod("Tick").Invoke(sc, new object[] { 1f / 60f }); }
        private static Component RigOf(object sc) => (Component)sc.GetType().GetProperty("Rig").GetValue(sc);

        private static bool IsArm(Transform t, bool right)
        {
            for (; t != null; t = t.parent) if (t.name == (right ? "RightUpperArm" : "LeftUpperArm") || t.name == (right ? "RightLowerArm" : "LeftLowerArm") || t.name == (right ? "RightHand" : "LeftHand")) return true;
            return false;
        }

        private static bool IsTorso(Transform start)
        {
            if (IsAnyArm(start)) return false;   // los brazos cuelgan de UpperChest: no cuentan como torso
            for (var t = start; t != null; t = t.parent) if (t.name == "Hips" || t.name == "Spine" || t.name == "Chest" || t.name == "UpperChest") return true;
            return false;
        }

        private static bool IsAnyArm(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name.EndsWith("UpperArm") || t.name.EndsWith("LowerArm") || t.name.EndsWith("Hand")) return true;
            return false;
        }

        /// <summary>Rango de |x| (en el espacio del cuerpo, un lado) de los triángulos cortados por el plano y.</summary>
        private static bool Slice(Transform root, Func<Transform, bool> pick, float y, bool right, out float minX, out float maxX)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!pick(mf.transform) || mf.sharedMesh == null) continue;
                var verts = mf.sharedMesh.vertices; var tris = mf.sharedMesh.triangles;
                var local = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++) local[i] = root.InverseTransformPoint(mf.transform.TransformPoint(verts[i]));
                for (int t = 0; t < tris.Length; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        Vector3 p = local[tris[t + e]], q = local[tris[t + (e + 1) % 3]];
                        if ((p.y - y) * (q.y - y) >= 0f) continue;
                        float x = p.x + (q.x - p.x) * ((y - p.y) / (q.y - p.y));
                        if (!right) x = -x;
                        if (x < 0f) continue;
                        lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x);
                    }
            }
            minX = lo; maxX = hi;
            return hi >= lo;
        }

        private static float Bone(Component rig, string name, bool y) { var t = FindDeep(rig.transform, name); return y ? rig.transform.InverseTransformPoint(t.position).y : rig.transform.InverseTransformPoint(t.position).x; }
        private static Transform FindDeep(Transform r, string n) { if (r.name == n) return r; for (int i = 0; i < r.childCount; i++) { var f = FindDeep(r.GetChild(i), n); if (f != null) return f; } return null; }

        [TestCaseSource(nameof(VariantIds))]
        public void IdleArms_HugTheTorso_InTheRealPose(string id)
        {
            var sc = Weapon("pistol", id);
            Step(sc, "Hip"); Run(sc, 2f);
            var rig = RigOf(sc);
            // sin arma en las manos: el brazo libre del IK (se pide el modo "Idle" sin manos soltando el arma)
            rig.GetType().GetField("ManualTick").SetValue(rig, true);
            var inputs = rig.GetType().GetField("Inputs").GetValue(rig);
            var tIn = inputs.GetType();
            tIn.GetField("RightWeight").SetValue(inputs, 0f); tIn.GetField("LeftWeight").SetValue(inputs, 0f); tIn.GetField("ArmSwing").SetValue(inputs, 1f);
            // el WeaponScenario recoloca las manos en cada Tick: se evita llamando al rig directamente con las manos libres
            var ev = rig.GetType().GetEvent("BeforeArms");
            sc.GetType().GetMethod("Destroy"); // (el escenario se destruye al terminar)
            rig.GetType().GetField("Inputs").SetValue(rig, inputs);
            var scT = sc.GetType();
            // desuscribe el escenario para que no ponga las manos en el arma
            var onBefore = scT.GetMethod("OnBeforeArms", BindingFlags.Instance | BindingFlags.NonPublic);
            ev.RemoveEventHandler(rig, Delegate.CreateDelegate(ev.EventHandlerType, sc, onBefore));
            for (int i = 0; i < 180; i++) { rig.GetType().GetField("Inputs").SetValue(rig, inputs); rig.GetType().GetMethod("Tick").Invoke(rig, new object[] { 1f / 60f }); }

            var root = rig.transform;
            float shoulderY = Bone(rig, "RightUpperArm", true), elbowY = Bone(rig, "RightLowerArm", true);
            float worstGap = float.MinValue, worstOverlap = float.MaxValue; string where = "";
            foreach (bool right in new[] { true, false })
                for (float y = shoulderY - 0.02f; y >= elbowY - 0.10f; y -= 0.02f)
                {
                    if (!Slice(root, IsTorso, y, right, out _, out float torsoOuter)) continue;
                    if (!Slice(root, t => IsArm(t, right), y, right, out float armInner, out _)) continue;
                    float gap = armInner - torsoOuter;
                    if (gap > worstGap) { worstGap = gap; where = (right ? "der" : "izq") + " y=" + y.ToString("0.00"); }
                    worstOverlap = Mathf.Min(worstOverlap, gap);
                }
            Debug.Log("[Brazo en reposo] " + id + ": hueco máx " + (worstGap * 100f).ToString("0.0") + " cm (" + where + "), solape máx " + (-worstOverlap * 100f).ToString("0.0") + " cm");
            Assert.LessOrEqual(worstGap, 0.015f, id + ": hueco de " + (worstGap * 100f).ToString("0.0") + " cm entre brazo y torso en reposo (" + where + ")");
            Assert.GreaterOrEqual(worstOverlap, -0.02f, id + ": el brazo se mete " + (-worstOverlap * 100f).ToString("0.0") + " cm en el torso");
        }

        private static Transform ShoulderOf(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name.EndsWith("UpperArm")) return t;
            return null;
        }

        private sealed class Shell { public string Name; public Vector3[] V; public int[] T; public Bounds B; }

        /// <summary>Mallas del tronco (torso, trapecio, chaleco, portacargadores, correas, radio...) en coordenadas del mundo.</summary>
        private static List<Shell> TorsoShells(Component rig)
        {
            var list = new List<Shell>();
            foreach (var mf in rig.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || IsAnyArm(mf.transform) || mf.name.StartsWith("Neck") || mf.name.StartsWith("Head")) continue;
                if (!(IsTorso(mf.transform))) continue;
                if (mf.name.EndsWith("cinturón") || mf.name.EndsWith("pelvis") || mf.name.EndsWith("bolsa")) continue;   // la cadera no se compara con el brazo apuntando
                var v = mf.sharedMesh.vertices; var w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) w[i] = mf.transform.TransformPoint(v[i]);
                var sh = new Shell { Name = mf.name, V = w, T = mf.sharedMesh.triangles };
                sh.B = new Bounds(w[0], Vector3.zero); foreach (var q in w) sh.B.Encapsulate(q);
                list.Add(sh);
            }
            return list;
        }

        private static float DistToTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return (p - a).magnitude;
            Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return (p - b).magnitude;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return (p - (a + ab * (d1 / (d1 - d3)))).magnitude;
            Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return (p - c).magnitude;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return (p - (a + ac * (d2 / (d2 - d6)))).magnitude;
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return (p - (b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))))).magnitude;
            float denom = 1f / (va + vb + vc);
            return (p - (a + ab * (vb * denom) + ac * (vc * denom))).magnitude;
        }

        /// <summary>true si el punto está dentro de la malla cerrada (paridad de cruces de un rayo) y, en "depth", la distancia a su superficie.</summary>
        private static bool Inside(Shell s, Vector3 p, out float depth)
        {
            depth = 0f;
            if (!s.B.Contains(p)) return false;
            Vector3 dir = new Vector3(0.3715f, 0.8113f, 0.4491f).normalized;
            int hits = 0; float best = float.MaxValue;
            for (int t = 0; t < s.T.Length; t += 3)
            {
                Vector3 a = s.V[s.T[t]], b = s.V[s.T[t + 1]], c = s.V[s.T[t + 2]];
                // Möller-Trumbore
                Vector3 e1 = b - a, e2 = c - a, h = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, h);
                if (Mathf.Abs(det) > 1e-9f)
                {
                    float f = 1f / det; Vector3 sv = p - a; float u = f * Vector3.Dot(sv, h);
                    if (u >= 0f && u <= 1f) { Vector3 q = Vector3.Cross(sv, e1); float v = f * Vector3.Dot(dir, q); if (v >= 0f && u + v <= 1f && f * Vector3.Dot(e2, q) > 1e-6f) hits++; }
                }
                float d = DistToTri(p, a, b, c); if (d < best) best = d;
            }
            depth = best;
            return (hits & 1) == 1;
        }

        private static bool IsVestPiece(string n) => n.EndsWith("chaleco") || n.EndsWith("portacargadores") || n.EndsWith("correas") || n.EndsWith("radio");

        /// <summary>Cuánto entran los vértices del brazo (sin deltoides, hombrera ni brazalete, ni el nacimiento del brazo) en las piezas del chaleco y en el torso, por la superficie real.</summary>
        private static void ArmInto(Component rig, out float vest, out string whoVest, out float torso, out string whoTorso)
        {
            vest = torso = 0f; whoVest = whoTorso = "";
            var shells = TorsoShells(rig);
            foreach (var mf in rig.GetComponentsInChildren<MeshFilter>())
            {
                if (!IsAnyArm(mf.transform) || mf.sharedMesh == null || mf.name.EndsWith("hombrera") || mf.name.EndsWith("deltoides") || mf.name.EndsWith("brazalete") || mf.name.StartsWith("finger") || mf.name.EndsWith("Hand")) continue;
                // el nacimiento del brazo (a menos de 9 cm de la articulación del hombro) es la propia unión con el torso: el brazo sale de dentro por diseño
                var shoulder = ShoulderOf(mf.transform);
                foreach (var v in mf.sharedMesh.vertices)
                {
                    Vector3 w = mf.transform.TransformPoint(v);
                    if (shoulder != null && (w - shoulder.position).sqrMagnitude < 0.09f * 0.09f) continue;
                    foreach (var sh in shells)
                    {
                        if (!Inside(sh, w, out float d)) continue;
                        if (IsVestPiece(sh.Name)) { if (d > vest) { vest = d; whoVest = mf.name + " en " + sh.Name; } }
                        else if (d > torso) { torso = d; whoTorso = mf.name + " en " + sh.Name; }
                    }
                }
            }
        }

        [TestCase("assault_rifle", "calavera"), TestCase("pistol", "calavera"), TestCase("shotgun", "medico"), TestCase("sniper", "francotirador"), TestCase("smg", "explorador"), TestCase("revolver", "explorador")]
        public void AimingAndCoverArms_DoNotPierceTheVest_NorSinkIntoTheTorso(string weapon, string variant)
        {
            var sc = Weapon(weapon, variant);
            Step(sc, "Aim"); Run(sc, 1.5f);
            ArmInto(RigOf(sc), out float vA, out string wvA, out float tA, out string wtA);
            Step(sc, "Cover"); Run(sc, 1.5f);
            ArmInto(RigOf(sc), out float vC, out string wvC, out float tC, out string wtC);
            Debug.Log("[Brazos y chaleco] " + weapon + " (" + variant + "): apuntando chaleco " + (vA * 100f).ToString("0.0") + " cm (" + wvA + "), torso " + (tA * 100f).ToString("0.0") + " cm (" + wtA + "); cobertura chaleco " + (vC * 100f).ToString("0.0") + " cm (" + wvC + "), torso " + (tC * 100f).ToString("0.0") + " cm (" + wtC + ")");
            Assert.LessOrEqual(vA, 0.02f, weapon + " apuntando: el brazo atraviesa el chaleco " + (vA * 100f).ToString("0.0") + " cm (" + wvA + ")");
            Assert.LessOrEqual(vC, 0.02f, weapon + " en cobertura: el brazo atraviesa el chaleco " + (vC * 100f).ToString("0.0") + " cm (" + wvC + ")");
            Assert.LessOrEqual(tA, 0.045f, weapon + " apuntando: el brazo se hunde " + (tA * 100f).ToString("0.0") + " cm en el torso (" + wtA + ")");
            Assert.LessOrEqual(tC, 0.045f, weapon + " en cobertura: el brazo se hunde " + (tC * 100f).ToString("0.0") + " cm en el torso (" + wtC + ")");
        }
    }
}

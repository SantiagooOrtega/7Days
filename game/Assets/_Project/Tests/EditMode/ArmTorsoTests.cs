using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>
    /// Unión brazo-torso con las mallas reales del modelo, en reposo (huesos en su pose de referencia: brazos colgando). Se corta cada malla con planos horizontales cada
    /// 2 cm: la distancia entre el borde exterior del torso (con el chaleco) y el borde interior del brazo es el hueco (+) o el solape (-).
    /// </summary>
    public class ArmTorsoTests
    {
        public static IEnumerable<CharacterVariantData> Variants() => CharacterVariants.Defaults();

        private static bool IsTorso(ModelPiece p) => p.Bone == BoneId.Hips || p.Bone == BoneId.Spine || p.Bone == BoneId.Chest || p.Bone == BoneId.UpperChest;
        private static bool IsArm(ModelPiece p, bool right) => p.Bone == (right ? BoneId.RightUpperArm : BoneId.LeftUpperArm) || p.Bone == (right ? BoneId.RightLowerArm : BoneId.LeftLowerArm);

        /// <summary>Rango de x (valor absoluto, lado derecho) de las piezas elegidas en el plano y.</summary>
        public static bool Slice(List<ModelPiece> pieces, BoneDef[] spec, Func<ModelPiece, bool> pick, float y, bool mirror, out float minX, out float maxX)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var pc in pieces)
            {
                if (!pick(pc)) continue;
                var o = HumanoidRigSpec.RestPosition(spec, pc.Bone) + pc.Pivot;
                var m = pc.Mesh;
                for (int t = 0; t < m.Triangles.Count; t += 3)
                {
                    V3 a = m.Positions[m.Triangles[t]] + o, b = m.Positions[m.Triangles[t + 1]] + o, c = m.Positions[m.Triangles[t + 2]] + o;
                    for (int e = 0; e < 3; e++)
                    {
                        V3 p = e == 0 ? a : (e == 1 ? b : c), q = e == 0 ? b : (e == 1 ? c : a);
                        if ((p.Y - y) * (q.Y - y) >= 0f) continue;
                        float x = p.X + (q.X - p.X) * ((y - p.Y) / (q.Y - p.Y));
                        if (mirror) x = -x;
                        if (x < 0f) continue;
                        if (x < lo) lo = x;
                        if (x > hi) hi = x;
                    }
                }
            }
            minX = lo; maxX = hi;
            return hi >= lo;
        }

        [TestCaseSource(nameof(Variants))]
        public void ArmHugsTheTorso_GapUnder15cm_AndNoDeepOverlap_BothSides(CharacterVariantData v)
        {
            var p = RigProportions.Soldier().WithBuild(v.buildScale);
            var spec = HumanoidRigSpec.Build(p);
            var pieces = SoldierModel.Build(spec, p, v);
            float shoulderY = HumanoidRigSpec.RestPosition(spec, BoneId.RightUpperArm).Y, elbowY = HumanoidRigSpec.RestPosition(spec, BoneId.RightLowerArm).Y;
            float worstGap = float.MinValue, worstOverlap = float.MaxValue; string where = "";
            foreach (bool right in new[] { true, false })
                for (float y = shoulderY - 0.02f; y >= elbowY - 0.10f; y -= 0.02f)
                {
                    if (!Slice(pieces, spec, IsTorso, y, !right, out _, out float torsoOuter)) continue;
                    if (!Slice(pieces, spec, pc => IsArm(pc, right), y, !right, out float armInner, out _)) continue;
                    float gap = armInner - torsoOuter;
                    if (gap > worstGap) { worstGap = gap; where = (right ? "der" : "izq") + " y=" + y.ToString("0.00"); }
                    worstOverlap = Math.Min(worstOverlap, gap);
                }
            TestContext.WriteLine(v.id + ": hueco máximo " + (worstGap * 100f).ToString("0.0") + " cm (" + where + "), solape máximo " + (-worstOverlap * 100f).ToString("0.0") + " cm");
            Assert.LessOrEqual(worstGap, 0.015f, v.id + ": hueco entre brazo y torso de " + (worstGap * 100f).ToString("0.0") + " cm en " + where);
            Assert.GreaterOrEqual(worstOverlap, -0.02f, v.id + ": el brazo se mete " + (-worstOverlap * 100f).ToString("0.0") + " cm en el torso");
        }

        [TestCaseSource(nameof(Variants))]
        public void ShoulderJoint_IsCoveredByDeltoidTrapeziusAndPad_NoBareGapAboveTheShoulder(CharacterVariantData v)
        {
            var p = RigProportions.Soldier().WithBuild(v.buildScale);
            var spec = HumanoidRigSpec.Build(p);
            var pieces = SoldierModel.Build(spec, p, v);
            float shoulderY = HumanoidRigSpec.RestPosition(spec, BoneId.RightUpperArm).Y;
            // a la altura del hombro y 2 cm más arriba el torso (trapecio) y el brazo (deltoides) tienen que tocarse; más arriba el hombro es una curva redonda y el cuello un valle natural
            foreach (float dy in new[] { 0f, 0.02f })
            {
                float y = shoulderY + dy;
                Assert.IsTrue(Slice(pieces, spec, pc => IsTorso(pc) || pc.Name.Contains("trapecio"), y, false, out _, out float torso), "sin torso/trapecio a +" + dy * 100f + " cm");
                Assert.IsTrue(Slice(pieces, spec, pc => IsArm(pc, true), y, false, out float arm, out _), "sin deltoides a +" + dy * 100f + " cm");
                TestContext.WriteLine(v.id + " +" + (dy * 100f).ToString("0") + " cm sobre el hombro: torso/trapecio " + (torso * 100f).ToString("0.0") + ", deltoides " + (arm * 100f).ToString("0.0"));
                Assert.LessOrEqual(arm - torso, 0.015f, v.id + ": hueco de " + ((arm - torso) * 100f).ToString("0.0") + " cm sobre el hombro (+" + dy * 100f + " cm)");
            }
        }

        [TestCaseSource(nameof(Variants))]
        public void Arms_DoNotPierceTheVest(CharacterVariantData v)
        {
            var p = RigProportions.Soldier().WithBuild(v.buildScale);
            var spec = HumanoidRigSpec.Build(p);
            var pieces = SoldierModel.Build(spec, p, v);
            // cajas del chaleco (placas, cummerbund, portacargadores, correas) en el espacio del esqueleto en reposo
            var boxes = new List<(V3 lo, V3 hi, string name)>();
            foreach (var pc in pieces)
            {
                if (pc.Slot != SoldierModel.SlotGear || !(pc.Name.Contains("chaleco") || pc.Name.Contains("portacargadores") || pc.Name.Contains("correas"))) continue;
                var o = HumanoidRigSpec.RestPosition(spec, pc.Bone) + pc.Pivot;
                float lx = 9f, ly = 9f, lz = 9f, hx = -9f, hy = -9f, hz = -9f;
                foreach (var q in pc.Mesh.Positions) { lx = Math.Min(lx, q.X); ly = Math.Min(ly, q.Y); lz = Math.Min(lz, q.Z); hx = Math.Max(hx, q.X); hy = Math.Max(hy, q.Y); hz = Math.Max(hz, q.Z); }
                boxes.Add((new V3(lx, ly, lz) + o, new V3(hx, hy, hz) + o, pc.Name));
            }
            float worst = 0f; string who = "";
            foreach (var pc in pieces)
            {
                if (!IsArm(pc, true) && !IsArm(pc, false)) continue;
                if (pc.Name.Contains("hombrera") || pc.Name.Contains("deltoides")) continue;   // el deltoides y la hombrera van sobre el hombro, por fuera del chaleco
                var o = HumanoidRigSpec.RestPosition(spec, pc.Bone) + pc.Pivot;
                foreach (var q0 in pc.Mesh.Positions)
                {
                    var q = q0 + o;
                    foreach (var (lo, hi, name) in boxes)
                    {
                        float d = Math.Min(Math.Min(q.X - lo.X, hi.X - q.X), Math.Min(Math.Min(q.Y - lo.Y, hi.Y - q.Y), Math.Min(q.Z - lo.Z, hi.Z - q.Z)));
                        if (d > worst) { worst = d; who = pc.Name + " en " + name; }
                    }
                }
            }
            TestContext.WriteLine(v.id + ": el brazo entra como máximo " + (worst * 100f).ToString("0.0") + " cm en una pieza del chaleco (" + who + ")");
            Assert.LessOrEqual(worst, 0.015f, v.id + ": el brazo atraviesa el chaleco " + (worst * 100f).ToString("0.0") + " cm (" + who + ")");
        }
    }
}

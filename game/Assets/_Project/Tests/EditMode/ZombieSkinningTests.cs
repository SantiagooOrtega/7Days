using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Pesos de hueso de los zombis: suman 1, las piezas rígidas usan un solo hueso, las articulaciones mezclan dos y las piezas de piel y ropa se doblan igual.</summary>
    public class ZombieSkinningTests
    {
        private static ZombieSkinned Skinned(string typeId, int lod = 0)
        {
            var a = ZombieAnatomies.For(typeId);
            var v = ZombieVariants.ForType(ZombieVariants.Defaults(), typeId)[0];
            return ZombieSkinning.Flatten(ZombieModel.Build(a, v, lod), a.Size);
        }

        public static IEnumerable<string> Types() => ZombieAnatomies.Types;

        [TestCaseSource(nameof(Types))]
        public void WeightsSumToOne_AndBonesAreValid(string type)
        {
            var z = Skinned(type);
            foreach (var part in new[] { z.Skin, z.Cloth })
            {
                Assert.AreEqual(part.Mesh.Positions.Count, part.Weights.Count);
                foreach (var w in part.Weights)
                {
                    Assert.AreEqual(1f, w.Sum, 1e-4f);
                    foreach (int b in new[] { w.B0, w.B1, w.B2, w.B3 }) Assert.That(b, Is.InRange(0, z.BoneCount - 1));
                    Assert.LessOrEqual(w.Count, 3, "como mucho hueso propio, padre e hijo");
                }
            }
        }

        [Test]
        public void ElbowBlend_IsHalfAndHalfAtTheJoint_AndRigidAwayFromIt()
        {
            var a = ZombieAnatomies.For(ZombieIds.Common);
            var spec = HumanoidRigSpec.Build(a.Proportions);
            V3 elbow = HumanoidRigSpec.RestPosition(spec, BoneId.LeftLowerArm);
            var at = ZombieSkinning.BlendWeights(spec, BoneId.LeftUpperArm, elbow, a.Size);
            Assert.AreEqual(0.5f, at.Of((int)BoneId.LeftUpperArm), 0.01f); Assert.AreEqual(0.5f, at.Of((int)BoneId.LeftLowerArm), 0.01f);
            var mid = ZombieSkinning.BlendWeights(spec, BoneId.LeftUpperArm, elbow + new V3(0f, 0.2f, 0f), a.Size);
            Assert.AreEqual(1f, mid.Of((int)BoneId.LeftUpperArm), 1e-4f);
            // las piezas del brazo y del antebrazo dan el MISMO peso al mismo punto: no se separan al doblar
            var fromLower = ZombieSkinning.BlendWeights(spec, BoneId.LeftLowerArm, elbow + new V3(0f, 0.02f, 0f), a.Size);
            var fromUpper = ZombieSkinning.BlendWeights(spec, BoneId.LeftUpperArm, elbow + new V3(0f, 0.02f, 0f), a.Size);
            Assert.AreEqual(fromUpper.Of((int)BoneId.LeftUpperArm), fromLower.Of((int)BoneId.LeftUpperArm), 1e-4f);
        }

        [TestCaseSource(nameof(Types))]
        public void RigidPieces_UseOneBone_AndBlendPiecesMixAtJoints(string type)
        {
            var a = ZombieAnatomies.For(type);
            var v = ZombieVariants.ForType(ZombieVariants.Defaults(), type)[0];
            var r = ZombieModel.Build(a, v, 0);
            var z = ZombieSkinning.Flatten(r, a.Size);
            int mixed = 0;
            for (int i = 0; i < z.Skin.Weights.Count; i++)
            {
                var p = r.Pieces[z.Skin.PieceOf[i]];
                int n = z.Skin.Weights[i].Count;
                if (!p.Blend || p.Extra >= 0) Assert.AreEqual(1, n, p.Name + " rígida");
                else if (n > 1) mixed++;
            }
            Assert.Greater(mixed, 150, "hay vértices mezclados en codos, rodillas y columna");
        }

        [Test]
        public void Head_IsRigid_SoItCanCollapseOnAHeadshot()
        {
            var a = ZombieAnatomies.For(ZombieIds.Common);
            var r = ZombieModel.Build(a, ZombieVariants.Defaults()[0], 0);
            var z = ZombieSkinning.Flatten(r, a.Size);
            for (int i = 0; i < z.Skin.Weights.Count; i++)
                if (r.Pieces[z.Skin.PieceOf[i]].Bone == BoneId.Head) { Assert.AreEqual(1f, z.Skin.Weights[i].Of((int)BoneId.Head), 1e-5f); }
        }

        [Test]
        public void BendingAnElbow_KeepsTheSkinConnected()
        {
            // doblar el codo 100 grados: ningún vértice del antebrazo se aleja más de lo razonable de su vecino del brazo en la articulación
            var a = ZombieAnatomies.For(ZombieIds.Common);
            var r = ZombieModel.Build(a, ZombieVariants.Defaults()[0], 0);
            var z = ZombieSkinning.Flatten(r, a.Size);
            V3 elbow = z.RestBonePositions[(int)BoneId.LeftLowerArm];
            var rot = Mat3.Euler(new V3(-100f, 0f, 0f));
            V3 Pose(V3 p, SkinWeight w)
            {
                V3 moved = elbow + rot.Mul(p - elbow);
                float wl = w.Of((int)BoneId.LeftLowerArm) + w.Of((int)BoneId.LeftHand);
                return p * (1f - wl) + moved * wl;
            }
            float maxJump = 0f;
            for (int i = 0; i < z.Skin.Weights.Count; i++)
            {
                var piece = r.Pieces[z.Skin.PieceOf[i]];
                if (piece.Bone != BoneId.LeftUpperArm && piece.Bone != BoneId.LeftLowerArm) continue;
                if (!piece.Blend) continue;
                V3 p = z.Skin.Mesh.Positions[i];
                if ((p - elbow).Length > 0.04f * a.Size) continue;
                V3 q = Pose(p, z.Skin.Weights[i]);
                maxJump = Math.Max(maxJump, (q - p).Length);
            }
            // el vértice se mueve (se dobla) pero con la mezcla nunca más que el radio del antebrazo por el seno del ángulo
            Assert.Less(maxJump, 0.2f * a.Size);
        }

        [TestCaseSource(nameof(Types))]
        public void TrianglesMatchModel(string type)
        {
            var a = ZombieAnatomies.For(type);
            var v = ZombieVariants.ForType(ZombieVariants.Defaults(), type)[0];
            var r = ZombieModel.Build(a, v, 0);
            Assert.AreEqual(r.Triangles(), ZombieSkinning.Flatten(r, a.Size).Triangles);
        }
    }
}

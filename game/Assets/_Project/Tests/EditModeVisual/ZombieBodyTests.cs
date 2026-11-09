using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>
    /// El zombi REAL de Unity (esqueleto, malla con pesos, hitboxes y decals) en las poses que se ven en el juego: nada bajo el suelo al andar, atacar o morir, los hitboxes
    /// cubren el cuerpo, 20 zombis no son clones y la capa de daño revela heridas por cuartos de vida.
    /// </summary>
    public class ZombieBodyTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private readonly List<GameObject> _roots = new List<GameObject>();

        [TearDown]
        public void TearDown() { foreach (var r in _roots) if (r != null) UnityEngine.Object.DestroyImmediate(r); _roots.Clear(); }

        public static IEnumerable<string> Types() { foreach (var t in ZombieAnatomies.Types) yield return t; foreach (var t in ZombieAnatomies.BossTypes) yield return t; }
        public static IEnumerable<string> Bosses() => ZombieAnatomies.BossTypes;

        private sealed class Body
        {
            public object Obj; public GameObject Root; public Type BodyType;
            public SkinnedMeshRenderer Smr => (SkinnedMeshRenderer)BodyType.GetProperty("Renderer").GetValue(Obj);
            public Component Rig => (Component)BodyType.GetProperty("Rig").GetValue(Obj);
            public object Prop(string n) => BodyType.GetProperty(n).GetValue(Obj);
        }

        private Body Make(string typeId, int seed, int bucket = 1)
        {
            ZombieArtWait();
            var root = new GameObject("Zombie " + typeId); _roots.Add(root);
            var health = root.AddComponent(T("SevenDays.Core.Health"));
            var type = ZombieCatalog.Get(typeId);
            var bt = T("SevenDays.Combat.ZombieBody");
            var obj = bt.GetMethod("Build").Invoke(null, new object[] { root.transform, health, type, bucket });
            bt.GetMethod("Setup").Invoke(obj, new object[] { type, seed, false });
            return new Body { Obj = obj, Root = root, BodyType = bt };
        }

        private static void ZombieArtWait() { T("SevenDays.Animation.ZombieArt").GetMethod("PrewarmAll").Invoke(null, null); T("SevenDays.Animation.ZombieArt").GetMethod("WaitAll").Invoke(null, null); }

        private object State(float dt, float moveAmount, bool chasing, bool stunned, float attackT, string kind, float dist = 5f)
        {
            var st = Activator.CreateInstance(T("SevenDays.Combat.ZombieAnimState"));
            void Set(string f, object v) => st.GetType().GetField(f).SetValue(st, v);
            Set("Dt", dt); Set("MoveAmount", moveAmount); Set("Chasing", chasing); Set("Stunned", stunned); Set("AttackT", attackT);
            Set("AttackKind", Enum.Parse(typeof(ZombieAttackKind), kind)); Set("DistanceToCamera", dist); Set("Time", 1f);
            return st;
        }

        // Animate recibe el struct por "in": se invoca con una copia en el array de argumentos
        private static void Animate(Body b, object state) { b.BodyType.GetMethod("Animate").Invoke(b.Obj, new[] { state }); }

        private static float MinY(Body b, out float maxY)
        {
            var m = new Mesh();
            b.Smr.BakeMesh(m);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in m.vertices) { var w = b.Smr.transform.TransformPoint(v); min = Mathf.Min(min, w.y); max = Mathf.Max(max, w.y); }
            UnityEngine.Object.DestroyImmediate(m);
            maxY = max;
            return min;
        }

        private void Walk(Body b, float seconds, float speed, bool chasing)
        {
            var root = b.Root.transform;
            for (int i = 0; i < Mathf.CeilToInt(seconds * 60f); i++)
            {
                root.position += root.forward * speed / 60f;
                Animate(b, State(1f / 60f, Mathf.Clamp01(speed / 5f), chasing, false, -1f, "Swipe"));
            }
        }

        [TestCaseSource(nameof(Types))]
        public void BuildsTheRigMeshHitboxesAndDecals(string type)
        {
            var b = Make(type, 3);
            Assert.IsNotNull(b.Smr.sharedMesh); Assert.AreEqual(2, b.Smr.sharedMaterials.Length);
            Assert.AreEqual(HumanoidRigSpec.BoneCount + (type == ZombieIds.Explosive ? 1 : (type == ZombieIds.BossFinal ? 5 : 0)), b.Smr.bones.Length);
            var hitboxes = (Array)b.Prop("Hitboxes");
            Assert.GreaterOrEqual(hitboxes.Length, 11);
            var zones = new HashSet<HitZone>();
            foreach (var h in hitboxes) zones.Add((HitZone)h.GetType().GetField("Zone").GetValue(h));
            Assert.IsTrue(zones.Contains(HitZone.Head) && zones.Contains(HitZone.Torso) && zones.Contains(HitZone.Arms) && zones.Contains(HitZone.Legs));
            foreach (var h in hitboxes) Assert.IsNotNull(((Component)h).GetComponent<Collider>());
        }

        [TestCaseSource(nameof(Types))]
        public void Hitboxes_FollowTheBones_AndCoverTheBody(string type)
        {
            var b = Make(type, 5);
            Walk(b, 1f, 1.5f, false);
            Physics.SyncTransforms();
            var m = new Mesh(); b.Smr.BakeMesh(m);
            var cols = new List<Collider>();
            foreach (var h in (Array)b.Prop("Hitboxes")) cols.Add(((Component)h).GetComponent<Collider>());
            int covered = 0, total = 0;
            float near = 0.05f * Mathf.Max(1f, ZombieAnatomies.For(type).Size * 0.6f);   // 5 cm en un cuerpo normal; algo más en los jefes de 4 m (la misma proporción)
            var verts = m.vertices; var bw = b.Smr.sharedMesh.boneWeights; var miss = new Dictionary<string, int>();
            for (int i = 0; i < verts.Length; i += 6)
            {
                var w = b.Smr.transform.TransformPoint(verts[i]);
                float best = float.MaxValue;
                foreach (var c in cols) best = Mathf.Min(best, Vector3.Distance(c.ClosestPoint(w), w));
                total++; if (best < near) covered++; else { string bn = b.Smr.bones[bw[i].boneIndex0].name; miss[bn] = (miss.TryGetValue(bn, out int c0) ? c0 : 0) + 1; }
            }
            UnityEngine.Object.DestroyImmediate(m);
            float frac = covered / (float)total;
            var sbm = new System.Text.StringBuilder(); foreach (var kv in miss) sbm.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            TestContext.WriteLine(type + " sin cubrir por hueso:" + sbm);
            TestContext.WriteLine(type + ": " + (frac * 100f).ToString("0") + " % de la malla a menos de " + (near * 100f).ToString("0") + " cm de un hitbox");
            Assert.Greater(frac, 0.8f, type + ": hitboxes demasiado lejos de la malla");
            // cada collider cuelga de un hueso del rig (no de la raíz) y sigue a la malla: su centro queda dentro de los límites del cuerpo
            var bounds = b.Smr.bounds;
            bounds.Expand(0.3f);
            foreach (var c in cols) Assert.IsTrue(bounds.Contains(c.bounds.center), type + " " + c.transform.parent.name + " fuera del cuerpo");
        }

        [TestCaseSource(nameof(Types))]
        public void Walking_Idle_AndAttacks_NeverGoBelowTheGround(string type)
        {
            var b = Make(type, 7);
            float max;
            Walk(b, 2.0f, 1.2f, false);
            Assert.GreaterOrEqual(MinY(b, out max), -0.03f, type + " andando");
            Walk(b, 1.5f, 3.2f, true);
            Assert.GreaterOrEqual(MinY(b, out max), -0.04f, type + " corriendo");
            foreach (ZombieAttackKind kind in Enum.GetValues(typeof(ZombieAttackKind)))
                for (int i = 0; i <= 42; i++)
                {
                    Animate(b, State(1f / 60f, 0f, true, false, i / 42f, kind.ToString()));
                    if (i % 7 == 0) Assert.GreaterOrEqual(MinY(b, out max), -0.05f, type + " " + kind + " t=" + (i / 42f).ToString("0.00"));
                }
            for (int i = 0; i < 90; i++) Animate(b, State(1f / 60f, 0f, false, true, -1f, "Swipe"));
            Assert.GreaterOrEqual(MinY(b, out max), -0.05f, type + " aturdido");
        }

        [TestCaseSource(nameof(Types))]
        public void Body_HasTheProportionsOfItsType(string type)
        {
            var b = Make(type, 9);
            for (int i = 0; i < 30; i++) Animate(b, State(1f / 60f, 0f, false, false, -1f, "Swipe"));
            MinY(b, out float maxY);
            var a = ZombieAnatomies.For(type, 1);
            Assert.That(maxY, Is.InRange(a.Proportions.height * 0.82f, a.Proportions.height * 1.1f), type + ": altura de pie " + maxY.ToString("0.00"));
        }

        [TestCase(ZombieIds.Common), TestCase(ZombieIds.Tank), TestCase(ZombieIds.Explosive), TestCase(ZombieIds.BossBrute), TestCase(ZombieIds.BossFinal)]
        public void Death_LiesOnTheGround_InEveryForm(string type)
        {
            foreach (var zone in new[] { HitZone.Head, HitZone.Torso, HitZone.Legs })
                foreach (var fromFront in new[] { true, false })
                {
                    var b = Make(type, 11);
                    for (int i = 0; i < 20; i++) Animate(b, State(1f / 60f, 0f, true, false, -1f, "Swipe"));
                    var dir = fromFront ? -b.Root.transform.forward : b.Root.transform.forward;   // dirección de la bala
                    b.BodyType.GetMethod("OnDeath").Invoke(b.Obj, new object[] { zone, dir, zone == HitZone.Head });
                    for (int i = 0; i < 120; i++) Animate(b, new ZombieAnimStateProxy(this).Dead());
                    float min = MinY(b, out float max);
                    Assert.AreEqual(0.012f, min, 0.04f * Mathf.Max(1f, ZombieAnatomies.For(type).Size * 0.6f), type + " " + zone + (fromFront ? " de frente" : " por detrás") + ": toca el suelo");
                    Assert.Less(max, ZombieAnatomies.For(type).Proportions.height * 0.9f, type + " " + zone + " queda caído");
                }
        }

        private object BossState(BossPose pose, float t)
        {
            var st = State(1f / 60f, 0f, true, false, -1f, "Swipe");
            st.GetType().GetField("BossPose").SetValue(st, pose); st.GetType().GetField("BossT").SetValue(st, t);
            return st;
        }

        [TestCaseSource(nameof(Bosses))]
        public void BossAbilities_StayAboveGround_AndStrikeWhenTheTelegraphEnds(string type)
        {
            var b = Make(type, 13);
            float size = ZombieAnatomies.For(type).Size;
            for (int i = 0; i < 30; i++) Animate(b, State(1f / 60f, 0f, true, false, -1f, "Swipe"));
            foreach (BossPose pose in Enum.GetValues(typeof(BossPose)))
            {
                if (pose == BossPose.None) continue;
                float handAtWind = 0f, handAtImpact = 0f;
                for (int i = 0; i <= 60; i++)
                {
                    float t = i / 60f;
                    for (int k = 0; k < 3; k++) Animate(b, BossState(pose, t));   // unos frames por paso: las manos llegan a su objetivo
                    var hand = ((Transform)b.Rig.GetType().GetMethod("Bone").Invoke(b.Rig, new object[] { BoneId.RightHand })).position.y;
                    if (Mathf.Abs(t - 0.4f) < 0.009f) handAtWind = hand;
                    if (Mathf.Abs(t - 0.5f) < 0.009f) handAtImpact = hand;
                    if (i % 6 == 0) Assert.GreaterOrEqual(MinY(b, out _), -0.05f * size, type + " " + pose + " t=" + t.ToString("0.00"));
                }
                // golpe de suelo: el puño está arriba al final del aviso y abajo justo cuando se dispara la habilidad
                if (pose == BossPose.Slam) Assert.Less(handAtImpact, handAtWind - 0.25f * size, type + ": el golpe cae en t = 0.5");
                if (pose == BossPose.Throw) Assert.Greater(handAtImpact, 0.9f * size, type + ": suelta con la mano alta y delante");
            }
        }

        [Test]
        public void Devourer_WeakPoint_OnlyWhileExposed_RibsOpen_AndItIsHitFromTheFront()
        {
            var b = Make(ZombieIds.BossFinal, 17);
            for (int i = 0; i < 30; i++) Animate(b, State(1f / 60f, 0f, false, false, -1f, "Swipe"));
            Transform weak = null;
            foreach (var t in b.Root.GetComponentsInChildren<Transform>(true)) if (t.name == "Hitbox_WeakPoint") weak = t;
            Assert.IsNotNull(weak, "hitbox del punto débil");
            Assert.IsFalse(weak.gameObject.activeInHierarchy, "oculto: no se le puede dar");
            b.BodyType.GetMethod("SetWeakPoint").Invoke(b.Obj, new object[] { true });
            for (int i = 0; i < 60; i++) Animate(b, State(1f / 60f, 0f, false, false, -1f, "Swipe"));
            Assert.IsTrue(weak.gameObject.activeInHierarchy);
            Transform ribL = null; foreach (var t in b.Root.GetComponentsInChildren<Transform>(true)) if (t.name == "RibL") ribL = t;
            Assert.Greater(Quaternion.Angle(Quaternion.identity, ribL.localRotation), 60f, "las costillas se abren");
            float r = ((SphereCollider)weak.GetComponent<Collider>()).radius;
            Assert.AreEqual(0.16f * 2.4f, r, 0.03f, "mismo tamaño que el punto débil de antes");
            // un disparo de frente a la altura del núcleo da en el punto débil antes que en el torso
            Physics.SyncTransforms();
            Vector3 c = weak.position, from = c + b.Root.transform.forward * 6f;
            Assert.IsTrue(Physics.Raycast(from, (c - from).normalized, out RaycastHit hit, 10f, 1 << weak.gameObject.layer));
            Assert.AreEqual(HitZone.WeakPoint, (HitZone)hit.collider.GetComponent(T("SevenDays.Combat.Hitbox")).GetType().GetField("Zone").GetValue(hit.collider.GetComponent(T("SevenDays.Combat.Hitbox"))));
            b.BodyType.GetMethod("SetWeakPoint").Invoke(b.Obj, new object[] { false });
            Assert.IsFalse(weak.gameObject.activeInHierarchy);
        }

        [Test]
        public void Fury_BrightensEyesAndVeins_AndTheDevourerJawOpensWhenRoaring()
        {
            var b = Make(ZombieIds.BossFinal, 19);
            for (int i = 0; i < 20; i++) Animate(b, State(1f / 60f, 0f, false, false, -1f, "Swipe"));
            Color E() { var mpb = new MaterialPropertyBlock(); b.Smr.GetPropertyBlock(mpb, 0); return mpb.GetColor("_EmissionColor"); }
            float calm = E().maxColorComponent;
            b.BodyType.GetMethod("SetFury").Invoke(b.Obj, new object[] { 2 });
            Assert.Greater(E().maxColorComponent, calm * 3f, "furia total: ojos y venas brillan");
            Transform jaw = null; foreach (var t in b.Root.GetComponentsInChildren<Transform>(true)) if (t.name == "JawL") jaw = t;
            for (int i = 0; i <= 40; i++) Animate(b, BossState(BossPose.Roar, 0.4f * i / 40f));
            Assert.Greater(Quaternion.Angle(Quaternion.identity, jaw.localRotation), 25f, "la mandíbula doble se abre al rugir");
        }

        private readonly struct ZombieAnimStateProxy
        {
            private readonly ZombieBodyTests _t;
            public ZombieAnimStateProxy(ZombieBodyTests t) { _t = t; }
            public object Dead()
            {
                var st = _t.State(1f / 60f, 0f, false, false, -1f, "Swipe");
                st.GetType().GetField("Dead").SetValue(st, true);
                return st;
            }
        }

        [Test]
        public void HeadshotDeath_CollapsesTheHead()
        {
            var b = Make(ZombieIds.Common, 13);
            var head = b.Smr.bones[(int)BoneId.Head];
            b.BodyType.GetMethod("OnDeath").Invoke(b.Obj, new object[] { HitZone.Head, b.Root.transform.forward, true });
            Assert.AreEqual(Vector3.zero, head.localScale);
        }

        [Test]
        public void TwentyZombies_AreNotClones()
        {
            var seen = new HashSet<string>();
            var sizes = new HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                var b = Make(ZombieIds.Common, 100 + i, i % 3);
                var variant = (ZombieVariantData)b.Prop("Variant"); var ind = (ZombieIndividual)b.Prop("Individual");
                var mpb = new MaterialPropertyBlock(); b.Smr.GetPropertyBlock(mpb, 0);
                var c = mpb.GetColor("_BaseColor");
                seen.Add(variant.id + "|" + ind.WoundMask + "|" + c.r.ToString("0.000") + "|" + ind.Limp.ToString("0.00"));
                sizes.Add(((ZombieAnatomy)b.Prop("Anatomy")).Proportions.height.ToString("0.000"));
            }
            Assert.GreaterOrEqual(seen.Count, 18, "apariencias distintas");
            Assert.AreEqual(3, sizes.Count, "tres alturas (-6 %, 0, +6 %)");
        }

        [Test]
        public void DamageLayer_RevealsWoundsByQuartersOfLife()
        {
            var b = Make(ZombieIds.Common, 21);
            int Count() => (int)b.Prop("ActiveDecals");
            int baseline = Count();
            void Hit(float hp, HitZone z) => b.BodyType.GetMethod("OnHit").Invoke(b.Obj, new object[] { z, b.Root.transform.forward, 1f, hp });
            Hit(0.9f, HitZone.Torso); Assert.AreEqual(baseline, Count(), "100-76 %: sin heridas nuevas");
            Hit(0.74f, HitZone.Torso); Assert.AreEqual(baseline + 1, Count(), "75 %");
            Hit(0.49f, HitZone.Head); Assert.AreEqual(baseline + 2, Count(), "50 %");
            Hit(0.24f, HitZone.Legs); Assert.AreEqual(baseline + 3, Count(), "25 %");
        }

        [Test]
        public void MeshBudget_PerType_IsReported()
        {
            var meshes = T("SevenDays.Animation.ZombieMeshes");
            var sb = new System.Text.StringBuilder("Triángulos por tipo (LOD0 / LOD1 / LOD2):\n");
            foreach (var type in ZombieAnatomies.Types)
            {
                foreach (var v in ZombieVariants.ForType(ZombieVariants.Defaults(), type))
                {
                    var bundle = meshes.GetMethod("Get").Invoke(null, new object[] { type, v.id, 1 });
                    var tris = (int[])bundle.GetType().GetField("Triangles").GetValue(bundle);
                    sb.Append("  ").Append(v.id).Append(": ").Append(tris[0]).Append(" / ").Append(tris[1]).Append(" / ").Append(tris[2]).Append('\n');
                    Assert.LessOrEqual(tris[0], ZombieModel.Budget(type), v.id);
                }
            }
            TestContext.WriteLine(sb.ToString());
        }
    }
}

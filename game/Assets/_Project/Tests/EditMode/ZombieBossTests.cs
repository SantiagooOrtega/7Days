using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Jefes con cuerpo propio (bloque D): anatomía de su tamaño de juego, mallas dentro del presupuesto con sus piezas, animaciones de habilidad sincronizadas con el golpe, texturas y voz.</summary>
    public class ZombieBossTests
    {
        public static IEnumerable<string> Bosses() => ZombieAnatomies.BossTypes;

        private static ZombieModelResult Model(string type, int lod) => ZombieModel.Build(ZombieAnatomies.For(type), ZombieVariants.ForType(ZombieVariants.Defaults(), type)[0], lod);

        [TestCaseSource(nameof(Bosses))]
        public void Anatomy_MatchesTheGameScale_AndHasOneVariant(string type)
        {
            var a = ZombieAnatomies.For(type);
            var data = ZombieCatalog.Get(type);
            Assert.IsTrue(a.IsBoss && ZombieAnatomies.Has(type));
            Assert.AreEqual(1.82f * data.scale, a.Proportions.height, 0.12f, "mide 1.82 m x su escala (la cápsula y los alcances no cambian)");
            Assert.AreEqual(1, ZombieVariants.ForType(ZombieVariants.Defaults(), type).Count);
            CollectionAssert.AreEqual(new[] { 1 }, ZombieAnatomies.Buckets(type));
            Assert.IsEmpty(ZombieLookValidator.Validate(ZombieVariants.Defaults()));
        }

        [TestCaseSource(nameof(Bosses))]
        public void Mesh_FitsTheBudget_WithThreeLods(string type)
        {
            int[] tris = new int[3];
            for (int l = 0; l < 3; l++) tris[l] = Model(type, l).Triangles();
            TestContext.WriteLine(type + ": " + tris[0] + " / " + tris[1] + " / " + tris[2] + " triángulos");
            Assert.AreEqual(ZombieModel.HeavyBudget, ZombieModel.Budget(type));
            Assert.LessOrEqual(tris[0], ZombieModel.HeavyBudget);
            Assert.Greater(tris[0], 7000, "más detalle que un zombi normal");
            Assert.Less(tris[1], tris[0] * 0.65f); Assert.Less(tris[2], tris[1] * 0.65f);
        }

        [Test]
        public void EachBoss_HasItsOwnPieces()
        {
            bool Has(string type, string piece) { foreach (var p in Model(type, 0).Pieces) if (p.Name.Contains(piece)) return true; return false; }
            foreach (var p in new[] { "armadura soldada", "hombrera", "cadena", "casco de obra", "puño maza" }) Assert.IsTrue(Has(ZombieIds.BossBrute, p), "Bruto: " + p);
            foreach (var p in new[] { "vendas", "neumatico", "bolsas", "deposito", "pustulas" }) Assert.IsTrue(Has(ZombieIds.BossLauncher, p), "Lanzador Colosal: " + p);
            foreach (var p in new[] { "mandibula izquierda", "mandibula derecha", "colmillos", "columna", "nucleo", "costillas", "garras" }) Assert.IsTrue(Has(ZombieIds.BossFinal, p), "Devorador: " + p);
            foreach (var p in Model(ZombieIds.BossFinal, 0).Pieces) Assert.AreNotEqual("Head mandibula", p.Name, "el Devorador no lleva la mandíbula normal");
            var extras = new List<string>(); foreach (var e in Model(ZombieIds.BossFinal, 0).Extras) extras.Add(e.Name);
            CollectionAssert.AreEquivalent(new[] { "JawL", "JawR", "Core", "RibL", "RibR" }, extras);
        }

        [Test]
        public void BossMotion_StrikesExactlyWhenTheTelegraphEnds_AndIsContinuous()
        {
            V3 gl = ZombieMotion.Guard(ZombieIds.BossBrute, -1, 0.5f), gr = ZombieMotion.Guard(ZombieIds.BossBrute, 1, 0.5f);
            foreach (BossPose pose in Enum.GetValues(typeof(BossPose)))
            {
                if (pose == BossPose.None) continue;
                var prev = ZombieMotion.Boss(pose, 0f, 0f, 0, gl, gr);
                for (int i = 1; i <= 200; i++)
                {
                    float t = i / 200f;
                    var f = ZombieMotion.Boss(pose, t, 0f, 0, gl, gr);
                    float jump = (f.RightHand - prev.RightHand).Length;
                    Assert.Less(jump, 0.2f, pose + " salto de la mano en t=" + t);
                    prev = f;
                }
            }
            Assert.AreEqual(1f, ZombieMotion.Boss(BossPose.Slam, ZombieMotion.BossImpactT, 0f, 0, gl, gr).Impact, 1e-3f);
            Assert.Greater(ZombieMotion.Boss(BossPose.Slam, 0.4f, 0f, 0, gl, gr).RightHand.Y, 0.9f, "puño arriba al final del aviso");
            Assert.Less(ZombieMotion.Boss(BossPose.Slam, 0.5f, 0f, 0, gl, gr).RightHand.Y, -0.6f, "puño en el suelo en el golpe");
            Assert.Less(ZombieMotion.Boss(BossPose.Throw, 0.4f, 0f, 0, gl, gr).RightHand.Z, -0.3f, "brazo atrás");
            Assert.Greater(ZombieMotion.Boss(BossPose.Throw, 0.5f, 0f, 0, gl, gr).RightHand.Z, 0.9f, "suelta delante");
            Assert.Greater(ZombieMotion.Boss(BossPose.Roar, 0.45f, 0f, 0, gl, gr).JawOpen, 0.8f);
            Assert.Greater(ZombieMotion.Boss(BossPose.ChargeRun, 0.5f, 0f, 0, gl, gr).Torso.X, 25f, "embiste inclinado");
        }

        [Test]
        public void BossTextures_HaveTheirMarks()
        {
            // celdas propias en un atlas pequeño: casco amarillo del Bruto y núcleo que brilla del Devorador
            var brute = ZombieTextures.Generate(ZombieAtlasKind.Skin, ZombieIds.BossBrute, 128);
            var cell = ZombieAtlas.Skin(ZombieAtlas.SkFeatureB);
            int x = (int)((cell.U0 + cell.Size * 0.5f) * 128), y = (int)((cell.V0 + cell.Size * 0.5f) * 128), i = (y * 128 + x) * 4;
            Assert.Greater(brute.Albedo[i], brute.Albedo[i + 2] + 60, "casco amarillo (mucho más rojo/verde que azul)");
            var dev = ZombieTextures.Generate(ZombieAtlasKind.Skin, ZombieIds.BossFinal, 128);
            int en = dev.EmissionSize, ex = (int)((cell.U0 + cell.Size * 0.5f) * en), ey = (int)((cell.V0 + cell.Size * 0.5f) * en);
            Assert.Greater(dev.Emission[(ey * en + ex) * 4], 120, "el núcleo brilla");
        }

        [TestCaseSource(nameof(Bosses))]
        public void BossVoice_Roars_AndStepsHeavy(string type)
        {
            var events = ZombieSoundMap.EventsFor(type);
            CollectionAssert.Contains(events, ZombieSound.Roar); CollectionAssert.Contains(events, ZombieSound.Step);
            Assert.AreEqual(ZombieSound.Roar, ZombieSoundMap.Alert(type));
            var roar = ZombieSynth.Render(type, ZombieSound.Roar, 0, 0);
            float peak = 0f; foreach (float s in roar) peak = Math.Max(peak, Math.Abs(s));
            Assert.That(peak, Is.InRange(0.5f, 0.95f));
            Assert.Less(ZombieVoiceProfile.For(type).F0, ZombieVoiceProfile.For(ZombieIds.Tank).F0, "más grave que el tanque");
        }
    }
}

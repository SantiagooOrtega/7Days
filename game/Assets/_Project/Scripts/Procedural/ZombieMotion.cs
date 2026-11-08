using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum ZombieAttackKind { Swipe, DoubleClaw, Bite, Smash, Throw }
    public enum ZombieDeathForm { FallBack, FallForward, Kneel }

    /// <summary>
    /// Objetivos y extras de una fase de la animación de un zombi (todo puro). Las manos van en espacio del cuerpo relativas a su hombro y en fracciones de la longitud del brazo
    /// (x derecha, y arriba, z delante); el peso 1 las lleva al objetivo con IK y 0 las deja colgar. Los grados son extras aditivos del ProceduralRig.
    /// </summary>
    public struct ZombieMotionFrame
    {
        public V3 LeftHand, RightHand;
        public float LeftWeight, RightWeight;
        public V3 Torso, Head;          // grados (pitch, yaw, roll)
        public V3 PelvisOffset;         // en fracciones de la altura
        public float PelvisRoll;        // grados
        public float Impact;            // 1 durante el instante del golpe (para efectos y sonido)
    }

    public static class ZombieMotion
    {
        public const float AttackSeconds = 0.7f;      // duración visual; el daño cae a los 0.35 s (t = 0.5)
        public const float ImpactT = 0.5f;

        private static float S(float e0, float e1, float x) { float k = Math.Max(0f, Math.Min(1f, (x - e0) / (e1 - e0))); return k * k * (3f - 2f * k); }
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static V3 Lerp(V3 a, V3 b, float t) => new V3(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Z, b.Z, t));

        /// <summary>Postura de guardia (brazos) de cada tipo; side -1 izquierda, +1 derecha; aggression 0..1 del individuo.</summary>
        public static V3 Guard(string typeId, int side, float aggression)
        {
            switch (typeId)
            {
                case ZombieIds.Runner: return new V3(side * (0.3f + 0.15f * aggression), 0.15f + 0.2f * aggression, 0.55f);
                case ZombieIds.Tank: return new V3(side * 0.3f, -0.45f, 0.5f);
                case ZombieIds.Armored: return new V3(side * 0.2f, -0.1f, 0.75f);
                case ZombieIds.Explosive: return new V3(side * 0.5f, -0.05f, 0.55f);
                case ZombieIds.Spitter: return side > 0 ? new V3(0.22f, 0.05f, 0.35f) : new V3(-0.2f, -0.1f, 0.7f);
                default: return new V3(side * 0.12f, -0.15f + 0.1f * aggression, 0.82f);
            }
        }

        /// <summary>Tipo de ataque de cada zombi (los comunes alternan zarpazo, doble zarpazo y mordida).</summary>
        public static ZombieAttackKind KindFor(string typeId, int attackIndex)
        {
            switch (typeId)
            {
                case ZombieIds.Tank: return ZombieAttackKind.Smash;
                case ZombieIds.Runner: return attackIndex % 2 == 0 ? ZombieAttackKind.DoubleClaw : ZombieAttackKind.Bite;
                case ZombieIds.Spitter: return ZombieAttackKind.Swipe;
                case ZombieIds.Explosive: return ZombieAttackKind.DoubleClaw;
                case ZombieIds.Armored: return attackIndex % 2 == 0 ? ZombieAttackKind.Swipe : ZombieAttackKind.Smash;
                default: return (ZombieAttackKind)(attackIndex % 3);
            }
        }

        /// <summary>
        /// Un ataque en t = 0..1: anticipación (0 a 0.45), golpe (0.45 a 0.55, con el impacto en 0.5, la ventana de daño del juego) y recuperación con seguimiento.
        /// "guardL/guardR" es la guardia de cada mano.
        /// </summary>
        public static ZombieMotionFrame Attack(ZombieAttackKind kind, float t, V3 guardL, V3 guardR)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float wind = S(0f, 0.45f, t), strike = S(0.43f, 0.53f, t), recover = S(0.6f, 1f, t);
            float k = wind * (1f - strike) + strike * (1f - recover);   // 0 en guardia, 1 en el golpe: la "intensidad" de cada tramo
            var f = new ZombieMotionFrame { LeftHand = guardL, RightHand = guardR, LeftWeight = 1f, RightWeight = 1f };
            f.Impact = Math.Max(0f, 1f - Math.Abs(t - ImpactT) / 0.05f);
            switch (kind)
            {
                case ZombieAttackKind.Swipe:
                {
                    V3 up = new V3(0.45f, 0.75f, 0.3f), down = new V3(-0.3f, -0.35f, 0.95f);
                    V3 target = Lerp(up, down, strike);
                    float amount = wind * (1f - recover);
                    f.RightHand = Lerp(guardR, target, amount);
                    f.LeftHand = Lerp(guardL, new V3(guardL.X, guardL.Y + 0.1f, guardL.Z * 0.8f), wind * (1f - recover));
                    f.Torso = new V3(Lerp(0f, 8f, strike) * (1f - recover) - 6f * wind * (1f - strike), Lerp(25f * wind, -30f * strike, 1f) * (1f - recover), 0f);
                    break;
                }
                case ZombieAttackKind.DoubleClaw:
                {
                    // izquierda golpea en 0.45, derecha en 0.55 (el daño cae entre las dos)
                    float sl = S(0.38f, 0.47f, t), sr = S(0.48f, 0.57f, t);
                    V3 raised = new V3(0.15f, 0.7f, 0.35f), hit = new V3(0f, -0.3f, 0.95f);
                    float am = wind * (1f - recover);
                    f.LeftHand = Lerp(guardL, Lerp(new V3(-raised.X, raised.Y, raised.Z), new V3(-0.1f, hit.Y, hit.Z), sl), am);
                    f.RightHand = Lerp(guardR, Lerp(raised, hit, sr), am);
                    f.Torso = new V3(Lerp(-8f * wind, 22f, strike) * (1f - recover), 0f, 0f);
                    f.PelvisOffset = new V3(0f, 0f, 0.03f * strike * (1f - recover));
                    break;
                }
                case ZombieAttackKind.Bite:
                {
                    // se echa atrás y se lanza hacia delante con las manos agarrando; la cabeza baja al morder
                    f.Torso = new V3(Lerp(-14f * wind, 34f, strike) * (1f - recover), 0f, 0f);
                    f.Head = new V3(Lerp(-12f * wind, 30f, strike) * (1f - recover), 0f, 0f);
                    float am = (0.3f * wind + 0.7f * strike) * (1f - recover);
                    f.LeftHand = Lerp(guardL, new V3(-0.25f, 0.1f, 0.95f), am);
                    f.RightHand = Lerp(guardR, new V3(0.25f, 0.1f, 0.95f), am);
                    f.PelvisOffset = new V3(0f, -0.01f * strike, 0.07f * strike * (1f - recover));
                    break;
                }
                case ZombieAttackKind.Smash:
                {
                    // dos puños arriba y golpe de suelo; el cuerpo se echa atrás y cae hacia delante
                    V3 high = new V3(0.15f, 1.05f, 0.15f), low = new V3(0.1f, -0.85f, 0.7f);
                    float am = wind * (1f - recover);
                    f.LeftHand = Lerp(guardL, Lerp(new V3(-high.X, high.Y, high.Z), new V3(-low.X, low.Y, low.Z), strike), am);
                    f.RightHand = Lerp(guardR, Lerp(high, low, strike), am);
                    f.Torso = new V3(Lerp(-18f * wind, 36f, strike) * (1f - recover), 0f, 0f);
                    f.Head = new V3(Lerp(-10f * wind, 20f, strike) * (1f - recover), 0f, 0f);
                    f.PelvisOffset = new V3(0f, -0.03f * strike * (1f - recover), 0.06f * strike * (1f - recover));
                    break;
                }
                default:   // Throw: mano derecha atrás y arriba, suelta delante en 0.5
                {
                    V3 back = new V3(0.35f, 0.55f, -0.35f), release = new V3(0.05f, 0.35f, 1.05f);
                    float am = wind * (1f - recover);
                    f.RightHand = Lerp(guardR, Lerp(back, release, strike), am);
                    f.LeftHand = Lerp(guardL, new V3(-0.25f, 0.25f, 0.6f), am);
                    f.Torso = new V3(Lerp(-6f * wind, 12f, strike) * (1f - recover), Lerp(-28f * wind, 24f, strike) * (1f - recover), 0f);
                    break;
                }
            }
            return f;
        }

        /// <summary>Reacción al impacto (aturdimiento ligero): impulsos en grados/s para los muelles del cuerpo y desplazamiento de la cadera.</summary>
        public struct Flinch
        {
            public V3 Torso, Head;       // velocidad angular inicial
            public float PelvisRoll, PelvisDrop;   // grados/s y metros/s (la pierna que cede baja la cadera)
            public float LeftArm, RightArm;        // empujón del objetivo de la mano (fracción de brazo/s)
        }

        /// <summary>dirLocal = dirección del golpe en espacio del cuerpo (z positiva: el disparo viene de frente y empuja hacia atrás).</summary>
        public static Flinch FlinchFor(HitZone zone, V3 dirLocal, float strength)
        {
            float k = Math.Max(0.3f, Math.Min(3f, strength));
            float fz = Math.Max(-1f, Math.Min(1f, dirLocal.Z)), fx = Math.Max(-1f, Math.Min(1f, dirLocal.X));
            var f = new Flinch();
            switch (zone)
            {
                case HitZone.Head:
                    f.Head = new V3(-fz * 220f * k, fx * 120f * k, fx * 90f * k); f.Torso = new V3(-fz * 70f * k, 0f, 0f); break;                      // la cabeza hacia atrás
                case HitZone.Torso:
                case HitZone.WeakPoint:
                    f.Torso = new V3(fz * 130f * k, fx * 60f * k, -fx * 80f * k); f.Head = new V3(fz * 90f * k, 0f, 0f); break;                          // el torso se dobla
                case HitZone.Arms:
                    f.Torso = new V3(0f, fx * 70f * k, -fx * 40f * k); f.LeftArm = (fx > 0 ? 1.8f : 0.6f) * k; f.RightArm = (fx < 0 ? 1.8f : 0.6f) * k; break;
                default:   // piernas: cede la rodilla, la cadera baja y se ladea
                    f.PelvisDrop = -0.9f * k; f.PelvisRoll = fx * 70f * k; f.Torso = new V3(fz * 50f * k, 0f, 0f); break;
            }
            return f;
        }

        /// <summary>Aturdido: cabeza colgando, torso doblado, brazos caídos y un vaivén lento.</summary>
        public static ZombieMotionFrame Stunned(float time, float phase)
        {
            float sway = (float)Math.Sin(time * 2.1f + phase * 6.28f);
            return new ZombieMotionFrame { Torso = new V3(24f + 3f * sway, 8f * sway, 5f * sway), Head = new V3(26f, 6f * sway, 14f), PelvisOffset = new V3(0f, -0.02f, 0f), PelvisRoll = 3f * sway };
        }

        /// <summary>Emerger del suelo en 0..1: la cadera sube desde bajo tierra, el torso se endereza y la cabeza cuelga hasta el final.</summary>
        public static ZombieMotionFrame Emerge(float e)
        {
            e = Math.Max(0f, Math.Min(1f, e));
            float up = S(0f, 0.85f, e), straight = S(0.25f, 1f, e);
            return new ZombieMotionFrame { PelvisOffset = new V3(0f, -0.62f * (1f - up), -0.05f * (1f - up)), Torso = new V3(70f * (1f - straight), 0f, 8f * (1f - straight)), Head = new V3(-25f * (1f - straight), 0f, 0f), LeftWeight = 0f, RightWeight = 0f };
        }

        /// <summary>Cojera (0..1) en el lado dado: la cadera cae hacia ese lado y el torso se contrapesa.</summary>
        public static void Limp(float limp, int side, float gaitPhase, out float pelvisRoll, out float torsoRoll)
        {
            float beat = 0.5f + 0.5f * (float)Math.Sin(gaitPhase * 2f * Math.PI + (side > 0 ? 0f : Math.PI));
            pelvisRoll = side * limp * (3f + 7f * beat);
            torsoRoll = -side * limp * (2f + 5f * beat);
        }

        /// <summary>Forma de morir: de espaldas si el golpe viene de frente, hacia delante si viene de atrás, de rodillas si fue en las piernas.</summary>
        public static ZombieDeathForm DeathFor(HitZone zone, V3 dirLocal, bool headshot, int seed)
        {
            if (zone == HitZone.Legs) return ZombieDeathForm.Kneel;
            if (headshot) return dirLocal.Z >= 0f ? ZombieDeathForm.FallBack : ZombieDeathForm.FallForward;
            if (Math.Abs(dirLocal.Z) < 0.25f) return (seed & 1) == 0 ? ZombieDeathForm.Kneel : ZombieDeathForm.FallForward;
            return dirLocal.Z > 0f ? ZombieDeathForm.FallBack : ZombieDeathForm.FallForward;
        }
    }

    /// <summary>Una pose completa del esqueleto: posición de la cadera (espacio del cuerpo) y rotación local de cada hueso en grados (Euler de Unity).</summary>
    public sealed class ZombiePose
    {
        public V3 HipsPos;
        public readonly V3[] Euler = new V3[HumanoidRigSpec.BoneCount];
        public ZombiePose Clone() { var c = new ZombiePose { HipsPos = HipsPos }; Array.Copy(Euler, c.Euler, Euler.Length); return c; }
    }

    /// <summary>
    /// Poses finales de la muerte (de espaldas, hacia delante y de rodillas) y su ajuste al suelo: se calcula con la malla real de menor detalle (piel y ropa con sus pesos) la
    /// altura de la cadera a la que el punto más bajo del cuerpo toca el suelo. Así el cuerpo nunca queda bajo el asfalto ni flotando, sea cual sea el tipo o la altura.
    /// </summary>
    public static class ZombiePoses
    {
        private static V3 E(float x, float y, float z) => new V3(x, y, z);

        /// <summary>Pose en el suelo SIN la altura de la cadera (HipsPos.Y = 0); la altura la pone <see cref="FitToGround"/>.</summary>
        public static ZombiePose Lying(ZombieDeathForm form, BoneDef[] spec)
        {
            var p = new ZombiePose();
            for (int i = 0; i < p.Euler.Length; i++) p.Euler[i] = E(0, 0, 0);
            void Set(BoneId id, float x, float y, float z) => p.Euler[(int)id] = E(x, y, z);
            switch (form)
            {
                case ZombieDeathForm.FallBack:
                    Set(BoneId.Hips, -90f, 0f, 8f); Set(BoneId.Spine, -4f, 0f, 0f); Set(BoneId.Chest, -3f, 0f, 0f); Set(BoneId.Neck, 8f, 0f, 0f); Set(BoneId.Head, 6f, 0f, 14f);
                    Set(BoneId.LeftUpperArm, 0f, 0f, -30f); Set(BoneId.RightUpperArm, 6f, 0f, 40f); Set(BoneId.LeftLowerArm, -10f, 0f, 0f); Set(BoneId.RightLowerArm, -6f, 0f, 0f);
                    Set(BoneId.LeftUpperLeg, 4f, 0f, 14f); Set(BoneId.RightUpperLeg, -6f, 0f, -10f); Set(BoneId.LeftLowerLeg, 14f, 0f, 0f); Set(BoneId.RightLowerLeg, 28f, 0f, 0f);
                    break;
                case ZombieDeathForm.FallForward:
                    Set(BoneId.Hips, 90f, 0f, -6f); Set(BoneId.Spine, 5f, 0f, 0f); Set(BoneId.Chest, 4f, 0f, 0f); Set(BoneId.Neck, -14f, 0f, 0f); Set(BoneId.Head, -6f, 0f, -18f);
                    Set(BoneId.LeftUpperArm, 6f, 0f, -34f); Set(BoneId.RightUpperArm, 10f, 0f, 28f); Set(BoneId.LeftLowerArm, 0f, 0f, 0f); Set(BoneId.RightLowerArm, 0f, 0f, 0f);
                    Set(BoneId.LeftUpperLeg, -4f, 0f, 10f); Set(BoneId.RightUpperLeg, 10f, 0f, -14f); Set(BoneId.LeftLowerLeg, 22f, 0f, 0f); Set(BoneId.RightLowerLeg, 6f, 0f, 0f);
                    break;
                default:   // de rodillas, torso caído hacia delante (se apoya en las rodillas y la cara)
                    Set(BoneId.Hips, 0f, 0f, 0f); Set(BoneId.Spine, 28f, 0f, 0f); Set(BoneId.Chest, 24f, 0f, 3f); Set(BoneId.UpperChest, 16f, 0f, 0f); Set(BoneId.Neck, 18f, 0f, 0f); Set(BoneId.Head, 14f, 0f, 12f);
                    Set(BoneId.LeftUpperArm, -6f, 0f, -8f); Set(BoneId.RightUpperArm, -14f, 0f, 8f); Set(BoneId.LeftLowerArm, -20f, 0f, 0f); Set(BoneId.RightLowerArm, -12f, 0f, 0f);
                    Set(BoneId.LeftUpperLeg, 0f, 0f, 6f); Set(BoneId.RightUpperLeg, 4f, 0f, -6f); Set(BoneId.LeftLowerLeg, 100f, 0f, 0f); Set(BoneId.RightLowerLeg, 96f, 0f, 0f);
                    Set(BoneId.LeftFoot, 20f, 0f, 0f); Set(BoneId.RightFoot, 18f, 0f, 0f);
                    break;
            }
            p.HipsPos = E(0f, 0f, 0f);
            return p;
        }

        /// <summary>Transformaciones de mundo (rotación y posición) de cada hueso para una pose.</summary>
        public static void Forward(BoneDef[] spec, ZombiePose pose, Mat3[] rot, V3[] pos)
        {
            for (int i = 0; i < spec.Length; i++)
            {
                Mat3 local = Mat3.Euler(pose.Euler[i]);
                if (spec[i].Parent < 0) { rot[i] = local; pos[i] = pose.HipsPos; }
                else { int pi = spec[i].Parent; rot[i] = rot[pi] * local; pos[i] = pos[pi] + rot[pi].Mul(spec[i].LocalPosition); }
            }
        }

        /// <summary>Posición de un vértice del modelo (espacio de reposo) con la pose y sus pesos de hueso; los huesos extra siguen a su hueso del esqueleto.</summary>
        public static V3 SkinPoint(V3 restPoint, SkinWeight w, V3[] restBones, int[] boneToSkeleton, Mat3[] rot, V3[] pos)
        {
            V3 acc = new V3(0, 0, 0);
            void Add(int b, float k)
            {
                if (k <= 0f) return;
                int sb = boneToSkeleton[b];
                acc = acc + (pos[sb] + rot[sb].Mul(restPoint - restBones[sb])) * k;
            }
            Add(w.B0, w.W0); Add(w.B1, w.W1); Add(w.B2, w.W2); Add(w.B3, w.W3);
            return acc;
        }

        /// <summary>
        /// Altura de la cadera para la que el punto más bajo de la malla toca el suelo con la pose dada (devuelve HipsPos.Y). Los huesos extra (vientre) siguen a su padre.
        /// </summary>
        public static float FitToGround(ZombieSkinned mesh, BoneDef[] spec, ZombiePose pose)
        {
            var rot = new Mat3[spec.Length]; var pos = new V3[spec.Length];
            var p = pose.Clone(); p.HipsPos = E(pose.HipsPos.X, 0f, pose.HipsPos.Z);
            Forward(spec, p, rot, pos);
            float min = float.MaxValue;
            foreach (var part in new[] { mesh.Skin, mesh.Cloth })
                for (int i = 0; i < part.Mesh.Positions.Count; i++)
                {
                    var w = part.Weights[i];
                    V3 q = SkinPoint(part.Mesh.Positions[i], w, mesh.RestBonePositions, mesh.BoneToSkeleton, rot, pos);
                    min = Math.Min(min, q.Y);
                }
            return -min + 0.012f;
        }
    }
}

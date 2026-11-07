using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Animation
{
    /// <summary>Estado del manejo del arma en un instante (pesos ya suavizados por quien llama). Lo rellenan WeaponHandling/PlayerRigDriver y el escenario de pruebas.</summary>
    public struct GripState
    {
        public float Aim, Sprint, Cover, Draw, Reload;   // pesos 0..1
        public float AimPitch;                           // grados (+ abajo)
        public bool Aiming, InCover, TriggerOn, Sprinting;
        public ReloadState ReloadInfo;                   // puede ser null
        public float ActionCycle;                        // 0..1 bomba / cerrojo ciclando
        public float SinceShot;                          // segundos desde el último disparo
        public float MeleeT;                             // segundos desde que empezó el golpe (-1 sin golpe)
        public bool MeleeHeavy, MeleeCharging;
        public float FirstPerson;                        // 0..1 (mejilla solo en tercera persona)
    }

    /// <summary>
    /// Agarre del arma compartido por el juego y por las pruebas visuales (así se prueba exactamente lo que se ve): la pose base del arma según el estado
    /// (cadera, corriendo, cobertura, apuntando, sacándola) y dónde van las manos (puntos de agarre del arma, orientación y pose de dedos).
    /// Se llama DESPUÉS de colocar el torso del rig y ANTES del IK de los brazos (ProceduralRig.BeforeArms): el arma sigue al pecho de este frame.
    /// </summary>
    public static class WeaponGripSolver
    {
        private static readonly Vector3 AimPose = new Vector3(-0.2f, 0.17f, 0.12f);
        private static readonly Vector3 Holstered = new Vector3(0.1f, -0.5f, -0.12f);

        private static Vector3 V(V3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>Pose base del arma en el espacio del padre de "pose" (el soporte del arma): posición y ángulos (grados).</summary>
        public static void BasePose(WeaponData w, ProceduralRig rig, Transform pose, in GripState s, out Vector3 pos, out Vector3 rot)
        {
            var hold = WeaponHolds.For(w.kind);
            pos = V(hold.HipPos); rot = V(hold.HipRot);
            pos = Vector3.Lerp(pos, V(hold.SprintPos), s.Sprint); rot = Vector3.Lerp(rot, V(hold.SprintRot), s.Sprint);
            pos = Vector3.Lerp(pos, V(hold.CoverPos), s.Cover); rot = Vector3.Lerp(rot, V(hold.CoverRot), s.Cover);
            pos = Vector3.Lerp(pos, AimLocalPosition(w, rig, pose, s.AimPitch), s.Aim); rot = Vector3.Lerp(rot, Vector3.zero, s.Aim);
            pos = Vector3.Lerp(Holstered, pos, s.Draw); rot = Vector3.Lerp(new Vector3(80f, 10f, 0f), rot, s.Draw);
        }

        private static Vector3 AimLocalPosition(WeaponData w, ProceduralRig rig, Transform pose, float pitch)
        {
            if (rig == null || pose.parent == null || w.IsMelee) return AimPose;
            var g = w.grip ?? WeaponGrip.For(w.kind, w.length, w.thickness);
            var chest = rig.Bone(BoneId.UpperChest);
            if (g.shouldered)
            {
                Vector3 shoulder = rig.Bone(BoneId.RightUpperArm).position;
                Vector3 pocket = shoulder - chest.right * 0.1f - chest.up * 0.03f + chest.forward * 0.08f;
                Vector3 local = pose.parent.InverseTransformPoint(pocket);
                return local - Quaternion.Euler(pitch, 0f, 0f) * V(g.StockPos);
            }
            Vector3 front = chest.position + rig.transform.forward * 0.42f + Vector3.up * 0.08f + rig.transform.right * 0.05f;
            return pose.parent.InverseTransformPoint(front);
        }

        /// <summary>Manos al arma: rellena las manos de "i" (posición, giro y peso) y devuelve la pose de dedos de cada una.</summary>
        public static void Hands(WeaponData data, Transform weaponPose, GunParts parts, ProceduralRig rig, in GripState s, ref RigInputs i, out HandPose rightPose, out HandPose leftPose)
        {
            HandGrip right = HandGrip.Relaxed, left = HandGrip.Relaxed;
            var hold = WeaponHolds.For(data.kind);
            var g = data.grip ?? WeaponGrip.For(data.kind, data.length, data.thickness);
            Vector3 wf = weaponPose.forward, wu = weaponPose.up, wr = weaponPose.right;
            i.RightHand = weaponPose.TransformPoint(V(g.GripPos));
            i.RightHandRot = HandRot(wr, (wf * 0.35f - wu).normalized) * Quaternion.Euler(g.grip[3] * 0.3f, g.grip[4], g.grip[5]);
            i.RightWeight = 1f;
            right = data.IsMelee ? HandGrip.Fist : HandGrip.Grip;

            bool twoHands = !hold.OneHandedMelee && (s.Aiming || s.InCover ? hold.TwoHandsAim : hold.TwoHandsHip);
            i.LeftHand = weaponPose.TransformPoint(V(g.ForegripPos));
            i.LeftHandRot = g.support == "pistol" ? HandRot(-wr, (wf * 0.35f - wu).normalized) : HandRot(-wu, (wr + wf * 0.5f).normalized);
            i.LeftWeight = twoHands ? 1f : 0f;
            left = twoHands ? (g.support == "pistol" ? HandGrip.Support : g.SupportGrip) : HandGrip.Relaxed;

            var rl = s.ReloadInfo;
            if (rl != null && rl.Active)
            {
                i.LeftWeight = 1f;
                i.LeftHand = ReloadHand(rl, i.LeftHand, parts, rig);
                left = rl.LastEvent == ReloadEvent.MagOut ? HandGrip.Open : HandGrip.MagGrip;
            }
            if (s.Draw < 0.7f) i.LeftWeight *= s.Draw;

            bool shouldered = g.shouldered && s.Aiming && !data.IsMelee;
            i.AimTwist = shouldered ? GripLayout.ShoulderedTwist : 0f;
            i.CheekWeld = shouldered && s.FirstPerson < 0.5f ? 1f : 0f;
            rightPose = HandPoses.For(right, s.TriggerOn);
            leftPose = HandPoses.For(left);
        }

        /// <summary>Dónde está la mano izquierda en cada momento de la recarga.</summary>
        public static Vector3 ReloadHand(ReloadState rl, Vector3 support, GunParts parts, ProceduralRig rig)
        {
            Vector3 magWell = parts != null ? parts.MagWorldRest : support;
            Vector3 belt = rig.Bone(BoneId.Hips).TransformPoint(-0.2f, 0.05f, 0.12f);
            switch (rl.Style)
            {
                case ReloadStyle.Shells:
                {
                    if (rl.Elapsed < ReloadState.ShellLead) return support;
                    float local = ((rl.Elapsed - ReloadState.ShellLead) % rl.ShellTime) / rl.ShellTime;
                    Vector3 port = parts != null && parts.Pump != null ? parts.Pump.position + parts.transform.up * -0.04f : support;
                    return local < 0.45f ? belt : port;
                }
                case ReloadStyle.Cylinder:
                    switch (rl.LastEvent)
                    {
                        case ReloadEvent.CylinderOpen: return parts != null && parts.Cylinder != null ? parts.Cylinder.position : support;
                        case ReloadEvent.HandToBelt: return belt;
                        case ReloadEvent.MagIn: return parts != null && parts.Cylinder != null ? parts.Cylinder.position : support;
                        default: return support;
                    }
                default:
                    switch (rl.LastEvent)
                    {
                        case ReloadEvent.BoltOpen: return parts != null && parts.Bolt != null ? parts.Bolt.position : support;
                        case ReloadEvent.MagOut: return belt;
                        case ReloadEvent.HandToBelt: return Vector3.Lerp(belt, magWell + Vector3.down * 0.1f, Mathf.Clamp01(rl.SinceEvent / 0.3f));
                        case ReloadEvent.MagIn: return magWell;
                        case ReloadEvent.Slap: return support;
                        default: return rl.Progress < 0.05f ? support : magWell;
                    }
            }
        }

        /// <summary>Rotación de una mano: "back" = hacia dónde mira el dorso (+X local; la palma mira al contrario), "fingers" = hacia dónde apuntan los dedos (-Y local).</summary>
        public static Quaternion HandRot(Vector3 back, Vector3 fingers)
        {
            Vector3 y = -fingers.normalized;
            Vector3 x = Vector3.ProjectOnPlane(back, y);
            if (x.sqrMagnitude < 1e-5f) x = Vector3.Cross(y, Vector3.forward);
            x.Normalize();
            Vector3 z = Vector3.Cross(x, y);
            return Quaternion.LookRotation(z, y);
        }
    }
}

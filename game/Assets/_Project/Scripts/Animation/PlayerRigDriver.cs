using UnityEngine;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Conecta el jugador con su rig procedural. Lee la interfaz pública de PlayerAnimatorHooks (Speed, MoveX/Y, IsAiming, IsInCover, IsVaulting, Fatigue,
    /// IsBlindFiring, Exposure...) y lo poco que no está ahí (agachado continuo, lean, mira, objetivos de las manos). Si mañana se usan clips reales, el resto del juego
    /// no cambia: solo este driver y el rig.
    /// </summary>
    [DefaultExecutionOrder(90)]   // después de WeaponHandling (pose del arma) y antes de ProceduralRig (100)
    public class PlayerRigDriver : MonoBehaviour
    {
        public ProceduralRig Rig { get; private set; }
        public Transform WeaponPose => _weaponPose;
        private PlayerAnimatorHooks _hooks;
        private PlayerMotor _motor;
        private PlayerCamera _camera;
        private PlayerCover _cover;
        private PlayerShooting _shooting;
        private PlayerVault _vault;
        private Health _health;
        private Transform _weaponPose;

        public void Initialize(ProceduralRig rig, Transform weaponPose)
        {
            Rig = rig; _weaponPose = weaponPose;
            Rig.Pose = LocomotionPoseSettings.Load();   // pose de locomoción del ScriptableObject (editable en F6)
            _hooks = GetComponent<PlayerAnimatorHooks>();
            _motor = GetComponent<PlayerMotor>();
            _camera = GetComponent<PlayerCamera>();
            _cover = GetComponent<PlayerCover>();
            _shooting = GetComponent<PlayerShooting>();
            _vault = GetComponent<PlayerVault>();
            _health = GetComponent<Health>();
            _health.Damaged += OnDamaged;
            _motor.Landed += OnLanded;
            if (_shooting != null) _shooting.ShotFired += OnShot;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            if (_motor != null) _motor.Landed -= OnLanded;
            if (_shooting != null) _shooting.ShotFired -= OnShot;
        }

        /// <summary>Dónde está la mano izquierda en cada momento de la recarga (el rig la lleva con suavidad de un punto a otro).</summary>
        private Vector3 ReloadHand(ReloadState rl, Vector3 support, SevenDays.Weapons.GunParts parts)
        {
            Vector3 magWell = parts != null ? parts.MagWorldRest : support;
            Vector3 belt = Rig.Bone(BoneId.Hips).TransformPoint(-0.2f, 0.05f, 0.12f);   // bolsillo de cargadores en la cintura izquierda
            switch (rl.Style)
            {
                case ReloadStyle.Shells:
                {
                    // cada cartucho: a la cintura y a la ventana de carga
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
                default:   // cargador (y cerrojo + cargador)
                    switch (rl.LastEvent)
                    {
                        case ReloadEvent.BoltOpen: return parts != null && parts.Bolt != null ? parts.Bolt.position : support;
                        case ReloadEvent.MagOut: return belt;
                        case ReloadEvent.HandToBelt: return Vector3.Lerp(belt, magWell + Vector3.down * 0.1f, Mathf.Clamp01(rl.SinceEvent / 0.3f));
                        case ReloadEvent.MagIn: return magWell;
                        case ReloadEvent.Slap: return support;
                        default: return rl.Progress < 0.05f ? support : magWell;   // empieza agarrando el cargador
                    }
            }
        }

        private void OnShot()
        {
            var d = _shooting.Current.Data;
            if (!d.IsMelee) Rig.Recoil(d.recoil * 0.35f);   // el torso y los brazos acusan el retroceso
        }

        private void OnDamaged(DamageInfo info) { if (info.Amount > 0f) Rig.Hit(info.Direction, info.Amount / 20f); }
        private void OnLanded(float fall) => Rig.Land(fall);

        private void LateUpdate()
        {
            if (Rig == null || _hooks == null) return;
            var i = new RigInputs
            {
                MoveX = _hooks.Move.x, MoveY = _hooks.Move.y,
                Grounded = _hooks.IsGrounded, Aiming = _hooks.IsAiming, Sprinting = _motor.IsSprinting,
                InCover = _hooks.IsInCover, Vaulting = _hooks.IsVaulting, BlindFiring = _hooks.IsBlindFiring, Dead = _health.IsDead,
                Crouch = _motor.CrouchAmount, Fatigue = _hooks.Fatigue, Exposure = _hooks.Exposure,
                Lean = _cover != null ? _cover.Lean.Value : 0f,
                LookDirection = _camera.AimForward, DesiredForward = _motor.DesiredFacing,
                ArmSwing = 1f,
                // cobertura baja, cubierto y quieto: una rodilla al suelo (la del lado del hombro activo; Q la cambia). Al asomarse se estira desde las rodillas.
                Kneel = _cover != null && _cover.Active && !_cover.WantsPop && !_cover.IsBlindFiring && _cover.CoverTop <= 1.25f ? 1f : 0f,   // deslizándose, el rig lo deja a media altura
                KneelSide = _camera.Shoulder.Side,
                CoverCollider = _cover != null ? _cover.CoverCollider : null,
            };

            // manos a los puntos de agarre del arma (WeaponData.grip: empuñadura, guardamano/bomba/cargador, culata), orientadas según el tipo de agarre
            var data = _shooting != null ? _shooting.Current.Data : null;
            HandGrip rightPose = HandGrip.Relaxed, leftPose = HandGrip.Relaxed;
            if (data != null && _weaponPose != null)
            {
                var hold = WeaponHolds.For(data.kind);
                var g = data.grip ?? WeaponGrip.For(data.kind, data.length, data.thickness);
                Vector3 wf = _weaponPose.forward, wu = _weaponPose.up, wr = _weaponPose.right;
                i.RightHand = _weaponPose.TransformPoint(V(g.GripPos));
                // empuñadura: dorso hacia fuera, dedos hacia delante-abajo rodeando la empuñadura
                i.RightHandRot = HandRot(wr, (wf * 0.35f - wu).normalized) * Quaternion.Euler(g.grip[3] * 0.3f, g.grip[4], g.grip[5]);
                i.RightWeight = 1f;
                rightPose = data.IsMelee ? HandGrip.Fist : HandGrip.Grip;

                bool inCover = _hooks.IsInCover;
                bool twoHands = !hold.OneHandedMelee && (_hooks.IsAiming || inCover ? hold.TwoHandsAim : hold.TwoHandsHip);   // en cobertura, también la pistola a dos manos
                i.LeftHand = _weaponPose.TransformPoint(V(g.ForegripPos));
                // apoyo: en "C" bajo el cañón (palma arriba), sobre la bomba o el cargador; con pistola, envolviendo la otra mano (palma hacia el arma)
                i.LeftHandRot = g.support == "pistol" ? HandRot(-wr, (wf * 0.35f - wu).normalized) : HandRot(-wu, (wr + wf * 0.5f).normalized);
                i.LeftWeight = twoHands ? 1f : 0f;
                leftPose = twoHands ? (g.support == "pistol" ? HandGrip.Support : g.SupportGrip) : HandGrip.Relaxed;

                // recarga: la mano izquierda suelta el arma, va a la cintura y vuelve (abierta al coger el cargador)
                var rl = _shooting.Reload;
                if (rl.Active)
                {
                    i.LeftWeight = 1f;
                    i.LeftHand = ReloadHand(rl, i.LeftHand, _shooting.Parts);
                    leftPose = rl.LastEvent == ReloadEvent.MagOut ? HandGrip.Open : HandGrip.MagGrip;
                }
                // sacando el arma, la mano de apoyo se separa hasta que el arma está lista
                if (!_shooting.Draw.Ready) { i.LeftWeight *= _shooting.Draw.Value; }

                // postura de perfil y mejilla en la culata al encarar un arma con culata
                bool shouldered = g.shouldered && _hooks.IsAiming && !data.IsMelee;
                i.AimTwist = shouldered ? GripLayout.ShoulderedTwist : 0f;
                i.CheekWeld = shouldered && _camera.FirstPersonBlend < 0.5f ? 1f : 0f;
            }

            // vault: las dos manos se apoyan en el obstáculo durante la primera mitad
            if (_hooks.IsVaulting && _vault != null)
            {
                float t = _vault.Progress;
                i.VaultProgress = t; i.VaultTop = _vault.ObstacleTop; i.VaultDir = _vault.VaultDirection;
                float hands = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(t / 0.65f) * Mathf.PI) * 1.6f);
                Vector3 right = Vector3.Cross(Vector3.up, _vault.VaultDirection).normalized;
                Vector3 top = _vault.ObstacleTop + Vector3.up * 0.03f;
                i.LeftHand = Vector3.Lerp(i.LeftHand, top - right * 0.25f, hands); i.LeftWeight = Mathf.Max(i.LeftWeight, hands);
                i.RightHand = Vector3.Lerp(i.RightHand, top + right * 0.25f, hands);
                i.LeftHandRot = i.RightHandRot = Quaternion.LookRotation(_vault.VaultDirection, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            }
            Rig.Inputs = i;

            // dedos: índice estirado fuera del guardamonte hasta que dispara (en el gatillo al disparar y un momento después); brazo libre medio cerrado al correr
            float dt = Time.deltaTime;
            bool triggerOn = _shooting != null && (Time.time - _shooting.LastShotTime < 0.25f || (_hooks.IsAiming && SevenDays.Core.GameInput.FireHeld));
            if (Rig.RightHandMesh != null) Rig.RightHandMesh.Blend(_health.IsDead ? HandPoses.For(HandGrip.Open) : HandPoses.For(rightPose, triggerOn), dt);
            if (Rig.LeftHandMesh != null)
            {
                var lp = HandPoses.For(leftPose);
                if (i.LeftWeight < 0.5f && _motor.IsSprinting) lp = HandPose.Lerp(lp, HandPoses.For(HandGrip.Fist), 0.6f);
                Rig.LeftHandMesh.Blend(_health.IsDead ? HandPoses.For(HandGrip.Open) : lp, dt);
            }
        }

        private static Vector3 V(V3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>Rotación de una mano: "back" = hacia dónde mira el dorso (+X local; la palma mira al contrario), "fingers" = hacia dónde apuntan los dedos (-Y local).</summary>
        private static Quaternion HandRot(Vector3 back, Vector3 fingers)
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

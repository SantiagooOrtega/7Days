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
        private WeaponHandling _handling;

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
            _handling = GetComponent<WeaponHandling>();
            Rig.BeforeArms += OnBeforeArms;
            _health.Damaged += OnDamaged;
            _motor.Landed += OnLanded;
            if (_shooting != null) _shooting.ShotFired += OnShot;
        }

        private void OnDestroy()
        {
            if (Rig != null) Rig.BeforeArms -= OnBeforeArms;
            if (_health != null) _health.Damaged -= OnDamaged;
            if (_motor != null) _motor.Landed -= OnLanded;
            if (_shooting != null) _shooting.ShotFired -= OnShot;
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

            Rig.Inputs = i;   // las manos se colocan en OnBeforeArms, con el pecho y el arma de este frame
        }

        /// <summary>
        /// Tras colocar el torso del rig y antes del IK de los brazos: pose final del arma (WeaponHandling), manos a sus puntos de agarre (WeaponGripSolver), manos al
        /// obstáculo en el vault y dedos. Así las manos y el arma usan el mismo pecho del mismo frame.
        /// </summary>
        private void OnBeforeArms(ProceduralRig rig, float dt)
        {
            if (_handling != null) _handling.ApplyPose();
            var i = Rig.Inputs;
            var data = _shooting != null ? _shooting.Current.Data : null;
            HandPose rightPose = HandPoses.For(HandGrip.Relaxed), leftPose = rightPose;
            if (data != null && _weaponPose != null && _handling != null)
                WeaponGripSolver.Hands(data, _weaponPose, _shooting.Parts, Rig, _handling.State, ref i, out rightPose, out leftPose);

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

            // dedos: índice estirado fuera del guardamonte hasta que dispara; brazo libre medio cerrado al correr
            if (Rig.RightHandMesh != null) Rig.RightHandMesh.Blend(_health.IsDead ? HandPoses.For(HandGrip.Open) : rightPose, dt);
            if (Rig.LeftHandMesh != null)
            {
                if (i.LeftWeight < 0.5f && _motor.IsSprinting) leftPose = HandPose.Lerp(leftPose, HandPoses.For(HandGrip.Fist), 0.6f);
                Rig.LeftHandMesh.Blend(_health.IsDead ? HandPoses.For(HandGrip.Open) : leftPose, dt);
            }
        }
    }
}

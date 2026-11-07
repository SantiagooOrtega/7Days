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
            _hooks = GetComponent<PlayerAnimatorHooks>();
            _motor = GetComponent<PlayerMotor>();
            _camera = GetComponent<PlayerCamera>();
            _cover = GetComponent<PlayerCover>();
            _shooting = GetComponent<PlayerShooting>();
            _vault = GetComponent<PlayerVault>();
            _health = GetComponent<Health>();
            _health.Damaged += OnDamaged;
            _motor.Landed += OnLanded;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            if (_motor != null) _motor.Landed -= OnLanded;
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
            };

            // manos al arma: derecha en la empuñadura; izquierda en el guardamano (dos manos), salvo arma cuerpo a cuerpo o corriendo sin apuntar (brazo libre)
            var data = _shooting != null ? _shooting.Current.Data : null;
            if (data != null && _weaponPose != null)
            {
                Vector3 fingers = (_weaponPose.forward * 0.35f - _weaponPose.up).normalized;
                i.RightHand = _weaponPose.TransformPoint(0f, -0.07f, 0.02f);
                i.RightHandRot = Quaternion.LookRotation(_weaponPose.forward, -fingers);
                i.RightWeight = 1f;
                bool twoHands = !data.IsMelee && (!_motor.IsSprinting || _hooks.IsAiming);
                i.LeftHand = _weaponPose.TransformPoint(0f, -0.05f, Mathf.Clamp(data.length * 0.6f, 0.16f, 0.5f));
                i.LeftHandRot = Quaternion.LookRotation(_weaponPose.forward, -(_weaponPose.right * -0.6f - _weaponPose.up).normalized);
                i.LeftWeight = twoHands ? 1f : 0f;
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
        }
    }
}

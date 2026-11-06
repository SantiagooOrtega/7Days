using UnityEngine;

namespace SevenDays.Player
{
    /// <summary>
    /// Parámetros listos para el Animator (Fase 3, cuando haya modelos de Mixamo/Quaternius):
    ///   bool IsGrounded, bool IsJumping, bool IsVaulting, float VerticalSpeed.
    /// Si asignas un Animator cuyo controlador tenga esos parámetros, se actualizan solos; sin modelo no hace nada.
    /// </summary>
    public class PlayerAnimatorHooks : MonoBehaviour
    {
        public static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        public static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
        public static readonly int IsVaultingHash = Animator.StringToHash("IsVaulting");
        public static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");

        public Animator Animator;

        public bool IsGrounded { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsVaulting { get; private set; }
        public float VerticalSpeed { get; private set; }

        private PlayerMotor _motor;
        private bool _hasGrounded, _hasJumping, _hasVaulting, _hasVertical;
        private RuntimeAnimatorController _checkedController;

        private void Awake() => _motor = GetComponent<PlayerMotor>();

        private void Update()
        {
            if (_motor == null) return;
            IsGrounded = _motor.IsGrounded;
            IsJumping = _motor.IsJumping;
            IsVaulting = _motor.IsVaulting;
            VerticalSpeed = _motor.VerticalSpeed;

            if (Animator == null || Animator.runtimeAnimatorController == null) return;
            if (_checkedController != Animator.runtimeAnimatorController) CacheParameters();
            if (_hasGrounded) Animator.SetBool(IsGroundedHash, IsGrounded);
            if (_hasJumping) Animator.SetBool(IsJumpingHash, IsJumping);
            if (_hasVaulting) Animator.SetBool(IsVaultingHash, IsVaulting);
            if (_hasVertical) Animator.SetFloat(VerticalSpeedHash, VerticalSpeed);
        }

        private void CacheParameters()
        {
            _checkedController = Animator.runtimeAnimatorController;
            _hasGrounded = _hasJumping = _hasVaulting = _hasVertical = false;
            foreach (var p in Animator.parameters)
            {
                if (p.nameHash == IsGroundedHash) _hasGrounded = true;
                else if (p.nameHash == IsJumpingHash) _hasJumping = true;
                else if (p.nameHash == IsVaultingHash) _hasVaulting = true;
                else if (p.nameHash == VerticalSpeedHash) _hasVertical = true;
            }
        }
    }
}

using UnityEngine;

namespace SevenDays.Player
{
    /// <summary>
    /// Parámetros del Animator listos para cuando haya modelos (Quaternius/Mixamo, Fase 3). Si asignas un Animator cuyo controlador tenga estos parámetros
    /// (menú 7Days > Crear Animator del jugador genera uno con todos), se actualizan solos; sin modelo no hace nada. Detalle en docs/ANIMATION_HOOKS.md.
    ///   float: Speed, MoveX, MoveY, VerticalSpeed, Fatigue, MeleeIndex
    ///   bool:  IsAiming, IsCrouching, IsInCover, IsGrounded, IsJumping, IsVaulting
    ///   trigger: Fire, Reload, MeleeSwing, MeleeHeavy
    /// </summary>
    public class PlayerAnimatorHooks : MonoBehaviour
    {
        public static readonly int SpeedHash = Animator.StringToHash("Speed");
        public static readonly int MoveXHash = Animator.StringToHash("MoveX");
        public static readonly int MoveYHash = Animator.StringToHash("MoveY");
        public static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        public static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        public static readonly int IsInCoverHash = Animator.StringToHash("IsInCover");
        public static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        public static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
        public static readonly int IsVaultingHash = Animator.StringToHash("IsVaulting");
        public static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        public static readonly int FatigueHash = Animator.StringToHash("Fatigue");
        public static readonly int FireHash = Animator.StringToHash("Fire");
        public static readonly int ReloadHash = Animator.StringToHash("Reload");
        public static readonly int MeleeSwingHash = Animator.StringToHash("MeleeSwing");
        public static readonly int MeleeHeavyHash = Animator.StringToHash("MeleeHeavy");
        public static readonly int MeleeIndexHash = Animator.StringToHash("MeleeIndex");

        /// <summary>Nombres y tipos de todos los parámetros (los usa el generador del Animator y las pruebas de la documentación).</summary>
        public static readonly (string name, AnimatorControllerParameterType type)[] Parameters =
        {
            ("Speed", AnimatorControllerParameterType.Float), ("MoveX", AnimatorControllerParameterType.Float), ("MoveY", AnimatorControllerParameterType.Float),
            ("IsAiming", AnimatorControllerParameterType.Bool), ("IsCrouching", AnimatorControllerParameterType.Bool), ("IsInCover", AnimatorControllerParameterType.Bool),
            ("IsGrounded", AnimatorControllerParameterType.Bool), ("IsJumping", AnimatorControllerParameterType.Bool), ("IsVaulting", AnimatorControllerParameterType.Bool),
            ("VerticalSpeed", AnimatorControllerParameterType.Float), ("Fatigue", AnimatorControllerParameterType.Float),
            ("Fire", AnimatorControllerParameterType.Trigger), ("Reload", AnimatorControllerParameterType.Trigger),
            ("MeleeSwing", AnimatorControllerParameterType.Trigger), ("MeleeHeavy", AnimatorControllerParameterType.Trigger), ("MeleeIndex", AnimatorControllerParameterType.Int),
        };

        public Animator Animator;

        public float Speed { get; private set; }
        public Vector2 Move { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsInCover { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsVaulting { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float Fatigue { get; private set; }

        private PlayerMotor _motor;
        private PlayerCamera _camera;
        private PlayerCover _cover;
        private PlayerShooting _shooting;
        private RuntimeAnimatorController _checked;
        private readonly System.Collections.Generic.HashSet<int> _present = new System.Collections.Generic.HashSet<int>();

        private void Start()
        {
            _motor = GetComponent<PlayerMotor>();
            _camera = GetComponent<PlayerCamera>();
            _cover = GetComponent<PlayerCover>();
            _shooting = GetComponent<PlayerShooting>();
            if (_shooting != null)
            {
                _shooting.ShotFired += () => { if (!_shooting.Current.Data.IsMelee) Trigger(FireHash); };
                _shooting.ReloadStarted += () => Trigger(ReloadHash);
                _shooting.MeleeSwung += (heavy, index) => { SetInt(MeleeIndexHash, index); Trigger(heavy ? MeleeHeavyHash : MeleeSwingHash); };
            }
        }

        private void Update()
        {
            if (_motor == null) return;
            Speed = _motor.HorizontalSpeed;
            Move = _motor.LocalMove;
            IsAiming = _camera != null && _camera.Aiming;
            IsCrouching = _motor.IsCrouched;
            IsInCover = _cover != null && _cover.Active;
            IsGrounded = _motor.IsGrounded;
            IsJumping = _motor.IsJumping;
            IsVaulting = _motor.IsVaulting;
            VerticalSpeed = _motor.VerticalSpeed;
            Fatigue = _camera != null && _camera.RunCam != null ? _camera.RunCam.Fatigue : 0f;

            if (!Ready()) return;
            SetFloat(SpeedHash, Speed); SetFloat(MoveXHash, Move.x); SetFloat(MoveYHash, Move.y);
            SetBool(IsAimingHash, IsAiming); SetBool(IsCrouchingHash, IsCrouching); SetBool(IsInCoverHash, IsInCover);
            SetBool(IsGroundedHash, IsGrounded); SetBool(IsJumpingHash, IsJumping); SetBool(IsVaultingHash, IsVaulting);
            SetFloat(VerticalSpeedHash, VerticalSpeed); SetFloat(FatigueHash, Fatigue);
        }

        private bool Ready()
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return false;
            if (_checked != Animator.runtimeAnimatorController)
            {
                _checked = Animator.runtimeAnimatorController;
                _present.Clear();
                foreach (var p in Animator.parameters) _present.Add(p.nameHash);   // solo se escriben los que existen: un controlador parcial no da avisos
            }
            return true;
        }

        private void SetFloat(int h, float v) { if (_present.Contains(h)) Animator.SetFloat(h, v); }
        private void SetBool(int h, bool v) { if (_present.Contains(h)) Animator.SetBool(h, v); }
        private void SetInt(int h, int v) { if (Ready() && _present.Contains(h)) Animator.SetInteger(h, v); }
        private void Trigger(int h) { if (Ready() && _present.Contains(h)) Animator.SetTrigger(h); }
    }
}

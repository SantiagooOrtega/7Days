using UnityEngine;
using SevenDays.Core;

namespace SevenDays.Player
{
    /// <summary>Movimiento con CharacterController: caminar, correr, agacharse y girar el cuerpo.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 6.8f;
        [SerializeField] private float crouchSpeed = 2.2f;
        [SerializeField] private float coverSpeed = 2.4f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float turnSpeed = 14f;

        private const float StandHeight = 1.8f;
        private const float CrouchHeight = 1.1f;

        /// <summary>El cuerpo mira hacia donde mira la cámara hasta este instante (se activa al disparar).</summary>
        public float FaceCameraUntil { get; set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouched => _crouch > 0.5f;

        private CharacterController _cc;
        private PlayerCamera _camera;
        private PlayerCover _cover;
        private Health _health;
        private Transform _visual;
        private Transform _weaponHolder;
        private float _crouch;
        private float _verticalVelocity;

        public void Initialize(PlayerCamera cam, PlayerCover cover, Transform visual, Transform weaponHolder)
        {
            _camera = cam;
            _cover = cover;
            _visual = visual;
            _weaponHolder = weaponHolder;
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
        }

        private void Update()
        {
            if (_camera == null || _health.IsDead) return;
            float dt = Time.deltaTime;

            bool wantCrouch = (_cover.Active && !_camera.Aiming) || GameInput.CrouchHeld;
            _crouch = Mathf.MoveTowards(_crouch, wantCrouch ? 1f : 0f, dt * 6f);
            ApplyCrouchPose();

            Vector2 input = GameInput.Move;
            Vector3 velocity;
            Vector3 moveDir = Vector3.zero;

            if (_cover.Active)
            {
                IsSprinting = false;
                velocity = _cover.CoverVelocity(input, coverSpeed);
            }
            else
            {
                moveDir = Quaternion.Euler(0f, _camera.Yaw, 0f) * new Vector3(input.x, 0f, input.y);
                IsSprinting = GameInput.SprintHeld && input.y > 0.1f && !_camera.Aiming && !wantCrouch;
                float speed = IsSprinting ? sprintSpeed : (IsCrouched ? crouchSpeed : walkSpeed);
                if (_camera.Aiming) speed *= 0.7f;
                velocity = moveDir * speed;
            }

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * dt;
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * dt);

            Vector3 face = Vector3.zero;
            if (_camera.Aiming || Time.time < FaceCameraUntil) face = Quaternion.Euler(0f, _camera.Yaw, 0f) * Vector3.forward;
            else if (_cover.Active) face = _cover.FacingDirection;
            else if (moveDir.sqrMagnitude > 0.01f) face = moveDir;

            if (face.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(face, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
            }
        }

        private void ApplyCrouchPose()
        {
            float h = Mathf.Lerp(StandHeight, CrouchHeight, _crouch);
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
            _visual.localScale = new Vector3(1f, h / StandHeight, 1f);
            var wp = _weaponHolder.localPosition;
            wp.y = Mathf.Lerp(1.2f, 0.72f, _crouch);
            _weaponHolder.localPosition = wp;
            _camera.PivotHeight = Mathf.Lerp(1.55f, 1.0f, _crouch);
        }
    }
}

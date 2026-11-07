using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// Movimiento con CharacterController: caminar, correr, agacharse, salto ágil (más gravedad al caer, coyote time, jump buffer,
    /// control aéreo limitado), daño por caídas altas y paso de obstáculos con PlayerVault.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 6.8f;
        [SerializeField] private float crouchSpeed = 2.2f;
        [SerializeField] private float coverSpeed = 2.4f;
        [SerializeField] private float turnSpeed = 14f;

        private const float StandHeight = 1.8f;
        private const float CrouchHeight = 1.1f;

        /// <summary>El cuerpo mira hacia donde mira la cámara hasta este instante (se activa al disparar).</summary>
        public float FaceCameraUntil { get; set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouched => _crouch > 0.5f;
        public MovementSettings Settings { get; private set; } = new MovementSettings();
        public bool IsGrounded { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsVaulting => _vault != null && _vault.IsVaulting;
        public float VerticalSpeed => _verticalVelocity;
        /// <summary>Velocidad horizontal real (m/s) y relativa a correr (0..1).</summary>
        public float HorizontalSpeed => new Vector2(_horizontalVelocity.x, _horizontalVelocity.z).magnitude;
        public float Speed01 => Mathf.Clamp01(HorizontalSpeed / sprintSpeed);
        /// <summary>Movimiento local respecto al cuerpo (x lateral, y adelante), suavizado: entrada del blend tree de 8 direcciones (strafe). 1 = correr.</summary>
        public Vector2 LocalMove { get; private set; }

        private CharacterController _cc;
        private PlayerCamera _camera;
        private PlayerCover _cover;
        private PlayerVault _vault;
        private PlayerImpulse _impulse;
        private JumpController _jump;
        private Health _health;
        private Transform _visual;
        private Transform _weaponHolder;
        private float _crouch;
        private float _verticalVelocity;
        private Vector3 _horizontalVelocity;
        private bool _wasGrounded = true;
        private float _peakY;

        public void Initialize(PlayerCamera cam, PlayerCover cover, Transform visual, Transform weaponHolder,
            MovementSettings settings, PlayerVault vault, PlayerImpulse impulse)
        {
            _camera = cam;
            _cover = cover;
            _visual = visual;
            _weaponHolder = weaponHolder;
            _vault = vault;
            _impulse = impulse;
            Settings = settings ?? new MovementSettings();
            _jump = new JumpController(Settings);
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _peakY = transform.position.y;
        }

        /// <summary>PlayerVault lo llama al terminar un vault: el jugador queda en el suelo y sin velocidad.</summary>
        public void OnVaultFinished()
        {
            _verticalVelocity = -2f;
            _horizontalVelocity = Vector3.zero;
            _wasGrounded = true;
            IsJumping = false;
            IsGrounded = true;
            _peakY = transform.position.y;
        }

        private Vector3 _push;

        /// <summary>Empuja al jugador (velocidad inicial en m/s; se disipa en ~0.4 s).</summary>
        public void Push(Vector3 velocity) => _push += velocity;

        private void Update()
        {
            if (_camera == null || _health.IsDead) return;
            if (IsVaulting) return; // durante el vault la posición la gobierna PlayerVault
            float dt = Mathf.Min(Time.deltaTime, 0.05f); // un tirón de FPS no debe lanzar al jugador

            bool wantCrouch = (_cover.Active && !_cover.WantsPop) || GameInput.CrouchHeld;   // asomarse por encima = levantarse (la cámara sube con el pivote)
            _crouch = Mathf.MoveTowards(_crouch, wantCrouch ? 1f : 0f, dt * 6f);
            ApplyCrouchPose();

            bool grounded = _cc.isGrounded;
            IsGrounded = grounded;
            TrackLanding(grounded);

            Vector2 input = GameInput.Move;
            Vector3 moveDir = Quaternion.Euler(0f, _camera.Yaw, 0f) * new Vector3(input.x, 0f, input.y);
            Vector3 jumpDir = moveDir.sqrMagnitude > 0.01f ? moveDir : Quaternion.Euler(0f, _camera.Yaw, 0f) * Vector3.forward;
            bool jumpPressed = GameInput.JumpPressed;

            // --- cobertura: Espacio salta el obstáculo hacia el lado contrario
            if (_cover.Active && jumpPressed)
            {
                if (_vault.TryStartFromCover()) return;
                jumpPressed = false; // no se salta "en el sitio" dentro de una cobertura
            }

            // --- vault: Espacio junto a un obstáculo vaultable, o correr hacia uno bajo
            if (!_cover.Active && grounded)
            {
                IsSprinting = GameInput.SprintHeld && input.y > 0.1f && !_camera.Aiming && !wantCrouch;
                if (jumpPressed && _vault.TryStartFromJump(jumpDir)) { _jump.Consume(); return; }
                if (Settings.AutoVaultWhenSprinting && IsSprinting && _vault.TryAutoVault(moveDir)) return;
            }

            // --- salto con coyote time y jump buffer
            bool doJump = _jump.Tick(dt, grounded, jumpPressed) && !_cover.Active;
            if (doJump)
            {
                _verticalVelocity = Settings.JumpVelocity;
                IsJumping = true;
                _peakY = transform.position.y;
                _impulse?.Jump();
            }

            // --- velocidad horizontal
            Vector3 desired;
            if (_cover.Active)
            {
                IsSprinting = false;
                desired = _cover.CoverVelocity(input, coverSpeed);
                moveDir = Vector3.zero;
            }
            else
            {
                IsSprinting = GameInput.SprintHeld && input.y > 0.1f && !_camera.Aiming && !wantCrouch;
                float speed = IsSprinting ? sprintSpeed : (IsCrouched ? crouchSpeed : walkSpeed);
                if (_camera.Aiming) speed *= 0.7f;
                desired = moveDir * speed;
            }

            if (grounded && !doJump) _horizontalVelocity = desired;                       // en el suelo: respuesta inmediata
            else _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desired, 30f * Settings.AirControl * dt); // en el aire: control limitado

            // --- gravedad: sube con la normal y cae con más fuerza (salto ágil)
            if (grounded && _verticalVelocity <= 0f) _verticalVelocity = -2f;
            float g = _verticalVelocity > 0f ? Settings.Gravity : Settings.Gravity * Settings.FallGravityMultiplier;
            _verticalVelocity -= g * dt;

            Vector3 velocity = _horizontalVelocity;
            velocity.y = _verticalVelocity;
            if (_push.sqrMagnitude > 0.01f)
            {
                velocity += _push;                                            // empujón externo (explosiones): se suma y se disipa
                _push = Vector3.Lerp(_push, Vector3.zero, 1f - Mathf.Exp(-7f * dt));
            }
            _cc.Move(velocity * dt);
            if ((_cc.collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f; // golpe con la cabeza

            // --- girar el cuerpo
            Vector3 face = Vector3.zero;
            if (_camera.Aiming || Time.time < FaceCameraUntil) face = Quaternion.Euler(0f, _camera.Yaw, 0f) * Vector3.forward;
            else if (_cover.Active) face = _cover.FacingDirection;
            else if (moveDir.sqrMagnitude > 0.01f) face = moveDir;

            if (face.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(face, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
            }
            Vector3 local = transform.InverseTransformDirection(_horizontalVelocity) / sprintSpeed;
            LocalMove = Vector2.Lerp(LocalMove, new Vector2(local.x, local.z), 1f - Mathf.Exp(-10f * dt));
        }

        /// <summary>Mide la caída y, al aterrizar, lanza el impulso de cámara y aplica el daño si la altura supera el umbral.</summary>
        private void TrackLanding(bool grounded)
        {
            if (!grounded)
            {
                _peakY = Mathf.Max(_peakY, transform.position.y);
            }
            else if (!_wasGrounded)
            {
                float fall = _peakY - transform.position.y;
                _impulse?.Land(fall);
                float damage = FallDamage.Compute(fall, Settings);
                if (damage > 0f)
                    _health.TakeDamage(new DamageInfo { Amount = damage, Point = transform.position, Direction = Vector3.up, Source = null });
                IsJumping = false;
            }
            if (grounded) { _peakY = transform.position.y; IsJumping = false; }
            _wasGrounded = grounded;
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

using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Player
{
    /// <summary>
    /// Pose procedural del arma sobre la "WeaponPose" (hija del soporte que sube y baja al agacharse):
    /// - apuntar: el arma se centra y se acerca a la línea de visión;
    /// - aim offset: el arma (y con ella el torso de las primitivas) sigue el pitch de la mira al apuntar o disparar;
    /// - sway: se retrasa un poco al girar la cámara; bob: se mece con los pasos (más al correr); ambos menores al apuntar;
    /// - kickback: retrocede y se levanta al disparar, y vuelve con un muelle; balanceo del golpe cuerpo a cuerpo.
    /// Todo con valores del ScriptableObject del arma (swayAmount, bobAmount, kickback, kickbackPitch).
    /// </summary>
    public class WeaponHandling : MonoBehaviour
    {
        private Transform _pose, _torso;
        private PlayerCamera _camera;
        private PlayerMotor _motor;
        private PlayerShooting _shooting;
        private PlayerCover _cover;
        private Vector2 _sway, _swayVel;
        private float _kick, _aimOffsetWeight;
        private Procedural.CriticalSpring _blind, _down, _sprintW, _coverW, _reloadW;
        private Procedural.CriticalSpring3 _melee;
        private Procedural.CriticalSpring _sideSwing;
        private SevenDays.Animation.ProceduralRig _rig;
        private Health _health;
        // estado calculado en LateUpdate; la pose se compone en ApplyPose (con rig, tras colocar el torso de este frame)
        private SevenDays.Animation.GripState _state;
        private Vector3 _kickPos, _reloadRot;
        private float _kickPitch, _side, _blindW, _downW;
        private V3 _meleeRot;
        private bool _ready;
        private readonly SevenDays.Animation.GripInertia _inertia = new SevenDays.Animation.GripInertia();

        /// <summary>Lo que el arma retrocede ahora mismo (0..1), para el panel de depuración.</summary>
        public float Kick => _kick;
        public Vector2 Sway => _sway;
        /// <summary>Estado del agarre de este frame (pesos de cadera/carrera/cobertura/apuntado, recarga...): lo usa el driver para colocar las manos.</summary>
        public SevenDays.Animation.GripState State => _state;

        public void Initialize(Transform pose, Transform torso, PlayerCamera cam, PlayerMotor motor, PlayerShooting shooting)
        {
            _pose = pose; _torso = torso; _camera = cam; _motor = motor; _shooting = shooting;
            _cover = GetComponent<PlayerCover>();
            _health = GetComponent<Health>();
            _rig = GetComponentInChildren<SevenDays.Animation.ProceduralRig>();
        }

        public void OnShot() => _kick = Mathf.Min(1.5f, _kick + 1f);

        private static Vector3 V(V3 v) => new Vector3(v.X, v.Y, v.Z);

        private void LateUpdate()
        {
            if (_pose == null || _shooting == null) return;
            float dt = Time.deltaTime;
            var w = _shooting.Current.Data;
            float aim = _camera.Aim.Eased;
            float calm = Mathf.Lerp(1f, 0.3f, aim);   // sway menor al apuntar

            // --- sway: muelle amortiguado que persigue al contrario del giro del ratón (limitado)
            Vector2 look = Cursor.lockState == CursorLockMode.Locked ? GameInput.Look : Vector2.zero;
            Vector2 target = Vector2.ClampMagnitude(-look * 0.05f * w.swayAmount * calm, 4f);
            _sway = Vector2.SmoothDamp(_sway, target, ref _swayVel, 0.08f, Mathf.Infinity, dt);

            // --- kickback con muelle
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 9f);
            float k = CameraMath.EaseInOut(Mathf.Clamp01(_kick));
            float kickMul = Mathf.Lerp(1f, w.recoilAimMultiplier, aim);
            _kickPos = new Vector3(0f, 0f, -w.kickback * k * kickMul);
            _kickPitch = -w.kickbackPitch * k * kickMul;

            // --- aim offset: al apuntar o al disparar, el arma sigue el pitch de la mira
            bool following = _camera.Aiming || Time.time < _motor.FaceCameraUntil;
            _aimOffsetWeight = Mathf.MoveTowards(_aimOffsetWeight, following ? 1f : 0.25f, dt * 6f);
            float aimPitch = CameraMath.DeltaAngle(0f, _camera.AimRotation.eulerAngles.x) * _aimOffsetWeight;

            // --- pesos de la pose base: cadera, corriendo (abajo e inclinada), cobertura (pegada al pecho) y apuntando (a la mira)
            float sprint = _sprintW.Step(_motor.IsSprinting && !_camera.Aiming ? 1f : 0f, 0.12f, dt);
            float cover = _coverW.Step(_cover != null && _cover.Active && !_cover.WantsPop && !_cover.IsBlindFiring ? 1f : 0f, 0.15f, dt);
            var rl = _shooting.Reload;
            float reload = _reloadW.Step(rl.Active ? 1f : 0f, 0.12f, dt);
            _reloadRot = SevenDays.Animation.WeaponGripSolver.ReloadTilt * reload;

            // --- cuerpo a cuerpo: anticipación, golpe curvo y seguimiento; cargando, se echa atrás poco a poco
            V3 m = new V3(0, 0, 0);
            float meleeT = -1f;
            if (w.IsMelee)
            {
                float t = Time.time - _shooting.SwingStart;
                if (_shooting.MeleeCharging) m = MeleeSwing.Pose(MeleeSwing.Windup(true) * _shooting.MeleeCharge, true, _shooting.SwingMirror);
                else if (t <= MeleeSwing.Total(_shooting.SwingHeavy)) { m = MeleeSwing.Pose(t, _shooting.SwingHeavy, _shooting.SwingMirror); meleeT = t; }
                float strikeStart = MeleeSwing.Windup(_shooting.SwingHeavy);
                _shooting.Parts?.SetTrail(!_shooting.MeleeCharging && t >= strikeStart * 0.8f && t <= strikeStart + MeleeSwing.Strike(_shooting.SwingHeavy) + 0.05f);
            }
            _meleeRot = _melee.Step(m, 0.025f, dt);

            // corriendo con un arma a una mano (pistola, barra): el brazo la lleva al costado con un pequeño balanceo amortiguado
            float oneHanded = (w.IsMelee || w.kind == WeaponKind.Pistol || w.kind == WeaponKind.Revolver) ? 1f : 0f;
            _side = _sideSwing.Step(_rig != null ? ArmSwingMath.Offset(_rig.GaitPhase, false) * sprint * oneHanded : 0f, 0.06f, dt);

            _blindW = _blind.Step(_cover != null && _cover.IsBlindFiring ? 1f : 0f, 0.08f, dt);
            _downW = _down.Step(_health != null && _health.IsDead ? 1f : 0f, 0.25f, dt);

            _state = new SevenDays.Animation.GripState
            {
                Aim = aim, Sprint = sprint, Cover = cover, Draw = _shooting.Draw.Eased, Reload = reload, AimPitch = aimPitch,
                Aiming = _camera.Aiming, InCover = _cover != null && _cover.Active, Sprinting = _motor.IsSprinting,
                ReloadInfo = rl, ActionCycle = ActionCycle(w), SinceShot = Time.time - _shooting.LastShotTime,
                MeleeT = meleeT, MeleeHeavy = _shooting.SwingHeavy, MeleeCharging = _shooting.MeleeCharging,
                TriggerOn = Time.time - _shooting.LastShotTime < 0.25f || (_camera.Aiming && GameInput.FireHeld),
                FirstPerson = _camera.FirstPersonBlend,
            };
            _ready = true;
            SevenDays.Animation.WeaponGripSolver.AnimateParts(_shooting.Parts, w, rl, ActionCycle(w), Time.time - _shooting.LastShotTime);

            // el torso de las primitivas se inclina un poco con la mira (aim offset del cuerpo; con modelos lo hará el Multi-Aim)
            float lean = _cover != null ? _cover.Lean.BodyRoll() : 0f;
            if (_torso != null) _torso.localRotation = Quaternion.Euler(aimPitch * 0.25f, 0f, lean);

            if (_rig == null) ApplyPose();   // con rig la pose se compone en ProceduralRig.BeforeArms (PlayerRigDriver), con el pecho de este frame
        }

        private float ActionCycle(WeaponData w)
        {
            float action = (Time.time - _shooting.ActionStart) / (w.kind == WeaponKind.SniperRifle ? 0.45f : 0.3f);
            return action >= 0f && action <= 1f ? Mathf.Sin(action * Mathf.PI) : 0f;
        }

        /// <summary>Compone la pose final del arma: base (WeaponGripSolver) + retroceso, sway, recarga, golpe, balanceo, lean, disparo a ciegas y caída al morir.</summary>
        public void ApplyPose()
        {
            if (!_ready || _pose == null) return;
            var w = _shooting.Current.Data;
            SevenDays.Animation.WeaponGripSolver.ApplyPose(_pose, w, _rig, _state, new SevenDays.Animation.PoseExtras
            {
                KickPos = _kickPos, KickPitch = _kickPitch, Sway = _sway, ReloadRot = _reloadRot, MeleeRot = _meleeRot, Side = _side,
            }, _inertia, Time.deltaTime);   // anclada al cuerpo del rig: ya sigue al pecho (con inercia según el peso)

            // el arma acompaña el lean (se desplaza hacia el lado al que se asoma)
            if (_cover != null) _pose.localPosition += new Vector3(_cover.Lean.Value * 0.12f, 0f, 0f);

            // disparo a ciegas: el brazo se extiende y el arma sube sobre el borde de la cobertura (las manos del rig la siguen por IK)
            if (_blindW > 0.001f && _pose.parent != null)
            {
                Vector3 local = _pose.parent.InverseTransformPoint(_cover.PopBase(true));
                _pose.localPosition = Vector3.Lerp(_pose.localPosition, local, _blindW);
            }
            // al morir el arma cae al suelo en vez de quedarse flotando
            if (_downW > 0.001f)
            {
                _pose.localPosition = Vector3.Lerp(_pose.localPosition, new Vector3(0.1f, 0.12f - (_pose.parent != null ? _pose.parent.localPosition.y : 1.2f), 0.35f), _downW);
                _pose.localRotation = Quaternion.Slerp(_pose.localRotation, Quaternion.Euler(0f, 70f, 90f), _downW);
            }
        }
    }
}

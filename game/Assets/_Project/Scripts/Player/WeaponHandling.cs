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
        private static readonly Vector3 AimPose = new Vector3(-0.2f, 0.17f, 0.12f);   // relativo a la cadera: más centrado y alto (a la línea de la mira)

        private Transform _pose, _torso;
        private PlayerCamera _camera;
        private PlayerMotor _motor;
        private PlayerShooting _shooting;
        private PlayerCover _cover;
        private Vector2 _sway, _swayVel;
        private float _kick, _aimOffsetWeight;
        private Procedural.CriticalSpring _blind, _down, _sprintW, _coverW, _reloadW;
        private Procedural.CriticalSpring3 _melee, _chest;
        private Procedural.CriticalSpring _sideSwing;
        private SevenDays.Animation.ProceduralRig _rig;
        private static readonly Vector3 Holstered = new Vector3(0.1f, -0.5f, -0.12f);
        private Health _health;

        /// <summary>Lo que el arma retrocede ahora mismo (0..1), para el panel de depuración.</summary>
        public float Kick => _kick;
        public Vector2 Sway => _sway;

        public void Initialize(Transform pose, Transform torso, PlayerCamera cam, PlayerMotor motor, PlayerShooting shooting)
        {
            _pose = pose; _torso = torso; _camera = cam; _motor = motor; _shooting = shooting;
            _cover = GetComponent<PlayerCover>();
            _health = GetComponent<Health>();
            _rig = GetComponentInChildren<SevenDays.Animation.ProceduralRig>();
        }

        public void OnShot() => _kick = Mathf.Min(1.5f, _kick + 1f);

        private static Vector3 V(V3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>Piezas móviles: corredera al disparar, bomba y cerrojo al ciclar, cargador y tambor durante la recarga.</summary>
        private void AnimateParts(WeaponData w, ReloadState rl)
        {
            var parts = _shooting.Parts;
            if (parts == null) return;
            float now = Time.time;
            parts.SetSlide(1f - Mathf.Clamp01((now - _shooting.LastShotTime) / 0.07f));
            float action = (now - _shooting.ActionStart) / (w.kind == WeaponKind.SniperRifle ? 0.45f : 0.3f);
            float cycle = action >= 0f && action <= 1f ? Mathf.Sin(action * Mathf.PI) : 0f;
            parts.SetPump(cycle);

            float p = rl.Active ? rl.Progress : -1f;
            float bolt = cycle;
            switch (rl.Active ? rl.Style : ReloadStyle.None)
            {
                case ReloadStyle.Magazine: Mag(parts, p, 0.15f, 0.65f); break;
                case ReloadStyle.BoltMagazine: Mag(parts, p, 0.28f, 0.62f); bolt = Window(p, 0.12f, 0.85f); break;
                case ReloadStyle.Cylinder: parts.SetCylinder(Window(p, 0.12f, 0.85f)); break;
                default: parts.SetMag(0f, true); parts.SetCylinder(0f); break;
            }
            parts.SetBolt(bolt);
        }

        /// <summary>Cargador: baja y desaparece al sacarlo; el nuevo aparece por debajo y sube hasta encajar.</summary>
        private static void Mag(GunParts parts, float p, float outAt, float inAt)
        {
            if (p < outAt) parts.SetMag(0f, true);
            else if (p < outAt + 0.12f) parts.SetMag(Mathf.Lerp(0f, 0.25f, (p - outAt) / 0.12f), true);
            else if (p < inAt - 0.15f) parts.SetMag(0.25f, false);
            else if (p < inAt) parts.SetMag(Mathf.Lerp(0.2f, 0f, CameraMath.EaseInOut((p - (inAt - 0.15f)) / 0.15f)), true);
            else parts.SetMag(0f, true);
        }

        /// <summary>0 cerrado .. 1 abierto entre "open" y "close", con transiciones cortas.</summary>
        private static float Window(float p, float open, float close)
            => Mathf.Clamp01((p - open) / 0.06f) * Mathf.Clamp01((close - p) / 0.06f);

        private void LateUpdate()
        {
            if (_pose == null || _shooting == null) return;
            float dt = Time.deltaTime;
            var w = _shooting.Current.Data;
            float aim = _camera.Aim.Eased;
            float calm = Mathf.Lerp(1f, 0.3f, aim);   // sway y bob menores al apuntar

            // --- sway: muelle amortiguado que persigue al contrario del giro del ratón (limitado)
            Vector2 look = Cursor.lockState == CursorLockMode.Locked ? GameInput.Look : Vector2.zero;
            Vector2 target = Vector2.ClampMagnitude(-look * 0.05f * w.swayAmount * calm, 4f);
            _sway = Vector2.SmoothDamp(_sway, target, ref _swayVel, 0.08f, Mathf.Infinity, dt);

            // --- bob: sigue a los pasos de la cámara de carrera (misma cadencia que los pies)
            float phase = _camera.RunCam != null ? _camera.RunCam.Steps.Phase : 0f;
            float speed01 = _motor.IsGrounded ? _motor.Speed01 : 0f;
            float bobAmp = w.bobAmount * Mathf.Lerp(0.012f, 0.03f, _motor.IsSprinting ? 1f : 0f) * speed01 * calm;
            Vector3 bob = new Vector3(Mathf.Sin(phase) * bobAmp, (Mathf.Abs(Mathf.Cos(phase)) - 0.5f) * bobAmp, 0f);
            if (_rig != null) bob = Vector3.zero;   // con rig el arma sigue el rebote REAL del pecho (abajo): este balanceo sería un duplicado

            // --- kickback con muelle
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 9f);
            float k = CameraMath.EaseInOut(Mathf.Clamp01(_kick));
            float kickMul = Mathf.Lerp(1f, w.recoilAimMultiplier, aim);
            Vector3 kickPos = new Vector3(0f, 0f, -w.kickback * k * kickMul);
            float kickPitch = -w.kickbackPitch * k * kickMul;

            // --- aim offset: al apuntar o al disparar, el arma sigue el pitch de la mira
            bool following = _camera.Aiming || Time.time < _motor.FaceCameraUntil;
            _aimOffsetWeight = Mathf.MoveTowards(_aimOffsetWeight, following ? 1f : 0.25f, dt * 6f);
            float aimPitch = CameraMath.DeltaAngle(0f, _camera.AimRotation.eulerAngles.x) * _aimOffsetWeight;

            // --- pose base según el tipo de arma: cadera, corriendo (abajo e inclinada), cobertura (pegada al pecho) y apuntando (a la mira)
            var hold = WeaponHolds.For(w.kind);
            float sprint = _sprintW.Step(_motor.IsSprinting && !_camera.Aiming ? 1f : 0f, 0.12f, dt);
            float cover = _coverW.Step(_cover != null && _cover.Active && !_cover.WantsPop && !_cover.IsBlindFiring ? 1f : 0f, 0.15f, dt);
            Vector3 pos = V(hold.HipPos), rot = V(hold.HipRot);
            pos = Vector3.Lerp(pos, V(hold.SprintPos), sprint); rot = Vector3.Lerp(rot, V(hold.SprintRot), sprint);
            pos = Vector3.Lerp(pos, V(hold.CoverPos), cover); rot = Vector3.Lerp(rot, V(hold.CoverRot), cover);
            pos = Vector3.Lerp(pos, AimPose, aim); rot = Vector3.Lerp(rot, Vector3.zero, aim);
            // sacar el arma (al cambiar): sube desde la cadera
            float draw = _shooting.Draw.Eased;
            pos = Vector3.Lerp(Holstered, pos, draw); rot = Vector3.Lerp(new Vector3(80f, 10f, 0f), rot, draw);

            // --- recarga: el arma se inclina hacia la mano izquierda mientras dura
            var rl = _shooting.Reload;
            float reload = _reloadW.Step(rl.Active ? 1f : 0f, 0.12f, dt);
            Vector3 reloadRot = new Vector3(-12f, 12f, 32f) * reload;
            pos += new Vector3(-0.05f, 0.03f, -0.04f) * reload;

            // --- cuerpo a cuerpo: anticipación, golpe curvo y seguimiento; cargando, se echa atrás poco a poco
            V3 m = new V3(0, 0, 0);
            if (w.IsMelee)
            {
                float t = Time.time - _shooting.SwingStart;
                if (_shooting.MeleeCharging) m = MeleeSwing.Pose(MeleeSwing.Windup(true) * _shooting.MeleeCharge, true, _shooting.SwingMirror);
                else if (t <= MeleeSwing.Total(_shooting.SwingHeavy)) m = MeleeSwing.Pose(t, _shooting.SwingHeavy, _shooting.SwingMirror);
                float strikeStart = MeleeSwing.Windup(_shooting.SwingHeavy);
                _shooting.Parts?.SetTrail(!_shooting.MeleeCharging && t >= strikeStart * 0.8f && t <= strikeStart + MeleeSwing.Strike(_shooting.SwingHeavy) + 0.05f);
            }
            V3 melee = _melee.Step(m, 0.025f, dt);

            // el arma sigue al pecho (rebote, giro, inclinación) con un retraso elástico: acompaña la inercia del torso sin vibrar
            Vector3 chest = Vector3.zero;
            if (_rig != null)
            {
                V3 c = _chest.Step(new V3(_rig.ChestOffsetLocal.x, _rig.ChestOffsetLocal.y, _rig.ChestOffsetLocal.z), 0.07f, dt);
                chest = new Vector3(c.X, c.Y, c.Z) * 0.85f * (1f - aim * 0.7f);
            }
            // corriendo con un arma a una mano (pistola, barra): el brazo la lleva al costado con un pequeño balanceo amortiguado
            float oneHanded = (w.IsMelee || w.kind == WeaponKind.Pistol || w.kind == WeaponKind.Revolver) ? 1f : 0f;
            float side = _sideSwing.Step(_rig != null ? ArmSwingMath.Offset(_rig.GaitPhase, false) * sprint * oneHanded : 0f, 0.06f, dt);
            pos += new Vector3(0f, 0f, side * 0.07f);
            rot.x += side * 12f;

            _pose.localPosition = pos + bob + kickPos + chest;
            _pose.localRotation = Quaternion.Euler(rot.x + aimPitch + kickPitch + _sway.y + reloadRot.x + melee.X, rot.y + _sway.x + reloadRot.y + melee.Y, rot.z - _sway.x * 0.5f + reloadRot.z + melee.Z);

            AnimateParts(w, rl);

            // el torso de las primitivas se inclina un poco con la mira (aim offset del cuerpo; con modelos lo hará el Multi-Aim)
            float lean = _cover != null ? _cover.Lean.BodyRoll() : 0f;
            if (_torso != null) _torso.localRotation = Quaternion.Euler(aimPitch * 0.25f, 0f, lean);
            // el arma acompaña el lean (se desplaza hacia el lado al que se asoma)
            if (_cover != null) _pose.localPosition += new Vector3(_cover.Lean.Value * 0.12f, 0f, 0f);

            // disparo a ciegas: el brazo se extiende y el arma sube sobre el borde de la cobertura (las manos del rig la siguen por IK)
            float blind = _blind.Step(_cover != null && _cover.IsBlindFiring ? 1f : 0f, 0.08f, dt);
            if (blind > 0.001f && _pose.parent != null)
            {
                Vector3 local = _pose.parent.InverseTransformPoint(_cover.PopBase(true));
                _pose.localPosition = Vector3.Lerp(_pose.localPosition, local, blind);
            }
            // al morir el arma cae al suelo en vez de quedarse flotando
            float down = _down.Step(_health != null && _health.IsDead ? 1f : 0f, 0.25f, dt);
            if (down > 0.001f)
            {
                _pose.localPosition = Vector3.Lerp(_pose.localPosition, new Vector3(0.1f, 0.12f - (_pose.parent != null ? _pose.parent.localPosition.y : 1.2f), 0.35f), down);
                _pose.localRotation = Quaternion.Slerp(_pose.localRotation, Quaternion.Euler(0f, 70f, 90f), down);
            }
        }
    }
}

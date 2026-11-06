using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

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
        private Vector2 _sway, _swayVel;
        private float _kick, _aimOffsetWeight;

        /// <summary>Lo que el arma retrocede ahora mismo (0..1), para el panel de depuración.</summary>
        public float Kick => _kick;
        public Vector2 Sway => _sway;

        public void Initialize(Transform pose, Transform torso, PlayerCamera cam, PlayerMotor motor, PlayerShooting shooting)
        {
            _pose = pose; _torso = torso; _camera = cam; _motor = motor; _shooting = shooting;
        }

        public void OnShot() => _kick = Mathf.Min(1.5f, _kick + 1f);

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

            // --- golpe cuerpo a cuerpo
            float swingT = (Time.time - _shooting.SwingStart) / 0.3f;
            float swing = swingT >= 0f && swingT <= 1f ? Mathf.Sin(swingT * Mathf.PI) * 80f : 0f;

            _pose.localPosition = Vector3.Lerp(Vector3.zero, AimPose, aim) + bob + kickPos;
            _pose.localRotation = Quaternion.Euler(aimPitch + kickPitch + swing + _sway.y, _sway.x, -_sway.x * 0.5f);

            // el torso de las primitivas se inclina un poco con la mira (aim offset del cuerpo; con modelos lo hará el Multi-Aim)
            if (_torso != null) _torso.localRotation = Quaternion.Euler(aimPitch * 0.25f, 0f, 0f);
        }
    }
}

using Unity.Cinemachine;
using UnityEngine;
using SevenDays.Core;

namespace SevenDays.Player
{
    /// <summary>
    /// Cámara en tercera persona sobre el hombro con Cinemachine 3.
    /// El ratón gira un objeto invisible (CameraTarget) y Cinemachine coloca la cámara detrás de él.
    /// Al apuntar (clic derecho) la cámara se acerca y reduce el campo de visión.
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Hombro (normal / apuntando)")]
        [SerializeField] private float hipDistance = 3.0f;
        [SerializeField] private float aimDistance = 1.7f;
        [SerializeField] private float hipFov = 60f;
        [SerializeField] private float aimFov = 42f;
        [SerializeField] private float hipShoulderX = 0.5f;
        [SerializeField] private float aimShoulderX = 0.65f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Aiming { get; private set; }
        public Camera Cam { get; private set; }
        /// <summary>Altura (sobre el suelo) del punto que mira la cámara; baja al agacharse.</summary>
        public float PivotHeight { get; set; } = 1.55f;

        private Transform _player;
        private Transform _target;
        private CinemachineCamera _vcam;
        private CinemachineThirdPersonFollow _follow;
        private float _aimBlend;
        private float _pivotSmoothed = 1.55f;

        public void Initialize(Transform player, Camera cam, float startYaw)
        {
            _player = player;
            Cam = cam;
            Yaw = startYaw;
            Pitch = 10f;

            if (!cam.TryGetComponent<CinemachineBrain>(out _)) cam.gameObject.AddComponent<CinemachineBrain>();

            _target = new GameObject("CameraTarget").transform;
            _target.position = player.position + Vector3.up * PivotHeight;

            var go = new GameObject("PlayerVirtualCamera");
            _vcam = go.AddComponent<CinemachineCamera>();
            var tgt = _vcam.Target;
            tgt.TrackingTarget = _target;
            _vcam.Target = tgt;

            _follow = go.AddComponent<CinemachineThirdPersonFollow>();
            _follow.ShoulderOffset = new Vector3(hipShoulderX, -0.4f, 0f);
            _follow.VerticalArmLength = 0.4f;
            _follow.CameraSide = 1f;
            _follow.CameraDistance = hipDistance;
            _follow.Damping = new Vector3(0.05f, 0.15f, 0.1f);
            // La cámara choca con el mundo (se acerca si hay una pared detrás), pero ignora la capa
            // "Ignore Raycast" (2) donde vive el jugador.
            var obstacles = _follow.AvoidObstacles;
            obstacles.Enabled = true;
            obstacles.CollisionFilter = ~(1 << 2);
            obstacles.CameraRadius = 0.25f;
            _follow.AvoidObstacles = obstacles;

            // Escucha los impulsos de PlayerImpulse (salto y aterrizaje). Reset() es privado y no se ejecuta con AddComponent
            // en tiempo de juego, así que los valores se fijan aquí.
            var listener = go.AddComponent<CinemachineImpulseListener>();
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.Use2DDistance = false;
            listener.UseCameraSpace = true;
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction { AmplitudeGain = 1f, FrequencyGain = 1f, Duration = 1f };

            LockCursor(true);
        }

        private void OnDestroy()
        {
            if (_target != null) Destroy(_target.gameObject);
            if (_vcam != null) Destroy(_vcam.gameObject);
        }

        public void Kick(float degrees) => Pitch = Mathf.Clamp(Pitch - degrees, minPitch, maxPitch);

        private void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (GameInput.EscapePressed) LockCursor(false);
            else if (!locked && GameInput.FirePressed) LockCursor(true);

            if (locked)
            {
                Vector2 look = GameInput.Look * sensitivity * (Aiming ? 0.6f : 1f);
                Yaw += look.x;
                Pitch = Mathf.Clamp(Pitch - look.y, minPitch, maxPitch);
            }

            Aiming = locked && GameInput.AimHeld;
            _aimBlend = Mathf.MoveTowards(_aimBlend, Aiming ? 1f : 0f, Time.deltaTime * 7f);
            float t = Mathf.SmoothStep(0f, 1f, _aimBlend);

            _follow.CameraDistance = Mathf.Lerp(hipDistance, aimDistance, t);
            var so = _follow.ShoulderOffset;
            so.x = Mathf.Lerp(hipShoulderX, aimShoulderX, t);
            _follow.ShoulderOffset = so;
            var lens = _vcam.Lens;
            lens.FieldOfView = Mathf.Lerp(hipFov, aimFov, t);
            _vcam.Lens = lens;
        }

        private void LateUpdate()
        {
            _pivotSmoothed = Mathf.Lerp(_pivotSmoothed, PivotHeight, 1f - Mathf.Exp(-12f * Time.deltaTime));
            _target.SetPositionAndRotation(_player.position + Vector3.up * _pivotSmoothed,
                Quaternion.Euler(Pitch, Yaw, 0f));
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

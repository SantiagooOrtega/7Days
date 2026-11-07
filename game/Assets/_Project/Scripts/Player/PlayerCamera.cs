using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// Cámara en tercera persona sobre el hombro con Cinemachine 3 (estilo Gears of War / Call of Duty).
    /// - El ratón gira un objeto invisible (CameraTarget) y Cinemachine coloca la cámara detrás de él, chocando con las paredes (AvoidObstacles).
    /// - Q cambia de hombro con una transición suave. Clic derecho apunta: FOV, distancia y sensibilidad cambian con ease in/out según el arma.
    /// - Armas con "aimFirstPerson" (francotirador): al apuntar, una segunda cámara virtual se pone detrás de la mira (SightPoint) y Cinemachine mezcla las dos.
    /// - La cámara de carrera (RunCamModel + RunCamExtension) y las sacudidas (PlayerImpulse) se suman sin pelear entre sí.
    /// - El retroceso (RecoilState) se suma a la rotación de la mira y se recupera al soltar el gatillo.
    /// </summary>
    [DefaultExecutionOrder(-60)]   // primero: decide si se apunta este frame (lo leen la cobertura y el disparo)
    public class PlayerCamera : MonoBehaviour
    {
        public CameraSettingsData Settings { get; private set; } = new CameraSettingsData();
        public RunCamModel RunCam { get; private set; }
        public RunCamOutput RunOutput { get; private set; }
        public ShoulderState Shoulder { get; } = new ShoulderState();
        public AimTransition Aim { get; } = new AimTransition();
        public RecoilState Recoil { get; } = new RecoilState();
        /// <summary>Contener la respiración con la mira (Shift).</summary>
        public BreathHold Breath { get; } = new BreathHold();
        /// <summary>Vaivén actual de la mira (grados), para el panel F5.</summary>
        public Vector2 ScopeSwayOffset { get; private set; }

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Aiming { get; private set; }
        public Camera Cam { get; private set; }
        /// <summary>Altura (sobre el suelo) del punto que mira la cámara; baja al agacharse.</summary>
        public float PivotHeight { get; set; } = 1.55f;
        /// <summary>Arma activa (la pone PlayerShooting): manda FOV, distancia, sensibilidad y tiempo de apuntado.</summary>
        public WeaponData Weapon { get; set; }
        /// <summary>Mira del arma (SightPoint) para el apuntado en primera persona.</summary>
        public Transform SightPoint { get; set; }
        /// <summary>Condición extra para poder apuntar (no durante el vault ni con arma cuerpo a cuerpo).</summary>
        public Func<bool> CanAim;
        /// <summary>Desplazamiento lateral extra (metros) hacia el hombro activo: el lean en cobertura.</summary>
        public float LeanOffset { get; set; }

        public int ScopeLevel { get; private set; }
        /// <summary>0..1: cuánto está la cámara detrás de la mira (primera persona).</summary>
        public float FirstPersonBlend { get; private set; }
        public bool IsScoped => Weapon != null && Weapon.HasScope && FirstPersonBlend > 0.9f;
        public float CurrentFov { get; private set; } = 60f;
        public float CurrentSensitivity { get; private set; }
        /// <summary>Mientras el ratón está sobre un panel de UI (ajustes, F5) un clic no vuelve a capturar el ratón.</summary>
        public static float UiHoverUntil;
        public Quaternion AimRotation => _target != null ? _target.rotation : transform.rotation;
        public Vector3 AimForward => AimRotation * Vector3.forward;

        private Transform _player, _target, _sightAnchor;
        private CinemachineCamera _vcam, _adsCam;
        private CinemachineBrain _brain;
        private CinemachineThirdPersonFollow _follow;
        private PlayerMotor _motor;
        private PlayerCover _cover;
        private PlayerImpulse _impulse;
        private float _pivotSmoothed = 1.55f, _smYaw, _smPitch;
        private readonly List<Renderer> _bodyRenderers = new List<Renderer>();
        private bool _bodyHidden, _aimToggled;
        private AudioSource _gasp;
        private static readonly WeaponData Fallback = new WeaponData();

        public void Initialize(Transform player, Camera cam, float startYaw)
        {
            _player = player;
            Cam = cam;
            Yaw = _smYaw = startYaw;
            Pitch = _smPitch = 10f;
            Settings = CameraSettings.Load();
            RunCam = new RunCamModel(CameraRunSettings.Load());

            _brain = cam.GetComponent<CinemachineBrain>();
            if (_brain == null) _brain = cam.gameObject.AddComponent<CinemachineBrain>();
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.2f);

            _target = new GameObject("CameraTarget").transform;
            _target.position = player.position + Vector3.up * PivotHeight;
            _sightAnchor = new GameObject("SightAnchor").transform;

            var go = new GameObject("PlayerVirtualCamera");
            _vcam = go.AddComponent<CinemachineCamera>();
            var tgt = _vcam.Target; tgt.TrackingTarget = _target; _vcam.Target = tgt;
            _vcam.Priority = 10;

            _follow = go.AddComponent<CinemachineThirdPersonFollow>();
            _follow.ShoulderOffset = new Vector3(Settings.hipShoulderX, -0.4f, 0f);
            _follow.VerticalArmLength = 0.4f;
            _follow.CameraSide = 1f;
            _follow.CameraDistance = Settings.hipDistance;
            _follow.Damping = new Vector3(0.05f, 0.15f, 0.1f);
            // La cámara choca con el mundo (se acerca si hay una pared detrás), pero ignora la capa "Ignore Raycast" (2) donde vive el jugador.
            var obstacles = _follow.AvoidObstacles;
            obstacles.Enabled = true;
            obstacles.CollisionFilter = ~(1 << 2);
            obstacles.CameraRadius = 0.25f;
            _follow.AvoidObstacles = obstacles;
            AddListener(go);
            go.AddComponent<RunCamExtension>().Source = this;

            // Cámara de mira (primera persona): fija a la mira del arma y orientada como la mira de la cámara principal.
            var ads = new GameObject("AdsVirtualCamera");
            _adsCam = ads.AddComponent<CinemachineCamera>();
            var at = _adsCam.Target; at.TrackingTarget = _sightAnchor; _adsCam.Target = at;
            _adsCam.Priority = 20;
            ads.AddComponent<CinemachineHardLockToTarget>();
            ads.AddComponent<CinemachineRotateWithFollowTarget>();
            var lens = _adsCam.Lens; lens.NearClipPlane = 0.02f; _adsCam.Lens = lens;
            AddListener(ads);
            ads.SetActive(false);

            LockCursor(true);
        }

        /// <summary>Escucha los impulsos de PlayerImpulse. Reset() es privado y no se ejecuta con AddComponent en tiempo de juego, así que los valores se fijan aquí.</summary>
        private static void AddListener(GameObject go)
        {
            var listener = go.AddComponent<CinemachineImpulseListener>();
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.Use2DDistance = false;
            listener.UseCameraSpace = true;
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction { AmplitudeGain = 1f, FrequencyGain = 1f, Duration = 1f };
        }

        private void OnDestroy()
        {
            if (_target != null) Destroy(_target.gameObject);
            if (_sightAnchor != null) Destroy(_sightAnchor.gameObject);
            if (_vcam != null) Destroy(_vcam.gameObject);
            if (_adsCam != null) Destroy(_adsCam.gameObject);
        }

        /// <summary>Vuelve a buscar los renderers del cuerpo y del arma (se ocultan al apuntar en primera persona). Llamar al cambiar el aspecto del arma.</summary>
        public void RefreshBodyRenderers()
        {
            _bodyRenderers.Clear();
            if (_player != null) _player.GetComponentsInChildren(true, _bodyRenderers);
            _bodyHidden = false;
        }

        /// <summary>Retroceso de un disparo (lo llama PlayerShooting).</summary>
        public void AddRecoil(WeaponData w, bool crouched) => Recoil.OnShot(w, Aiming, crouched, UnityEngine.Random.value);

        /// <summary>Compatibilidad: sube la mira unos grados de golpe (sin recuperación).</summary>
        public void Kick(float degrees) => Pitch = Mathf.Clamp(Pitch - degrees, Settings.minPitch, Settings.maxPitch);

        private void Update()
        {
            if (_motor == null) { _motor = GetComponent<PlayerMotor>(); _cover = GetComponent<PlayerCover>(); _impulse = GetComponent<PlayerImpulse>(); }
            float dt = Time.deltaTime;
            var w = Weapon ?? Fallback;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (GameInput.EscapePressed) LockCursor(false);
            else if (!locked && GameInput.FirePressed && Time.unscaledTime > UiHoverUntil) LockCursor(true);

            if (locked && GameInput.ShoulderSwapPressed) Shoulder.Toggle();
            Shoulder.Tick(dt, Settings.shoulderSwapTime);

            // mantener o alternar (opción "Apuntar" en los ajustes de Esc); si no se puede apuntar (vault, cuerpo a cuerpo) se suelta
            bool canAim = CanAim == null || CanAim();
            if (Settings.aimToggle) { if (locked && GameInput.AimPressed) _aimToggled = !_aimToggled; if (!canAim) _aimToggled = false; }
            Aiming = locked && canAim && (Settings.aimToggle ? _aimToggled : GameInput.AimHeld);
            Aim.Tick(dt, Aiming, w.adsTime, w.adsTime * 0.85f);

            // zoom variable de la mira telescópica con la rueda (mientras apuntas)
            if (Aiming && w.HasScope)
            {
                float scroll = GameInput.Scroll;
                if (scroll > 0.1f) ScopeLevel = Mathf.Min(w.scopeFovs.Length - 1, ScopeLevel + 1);
                else if (scroll < -0.1f) ScopeLevel = Mathf.Max(0, ScopeLevel - 1);
            }
            if (!w.HasScope) ScopeLevel = 0;
            ScopeLevel = Mathf.Clamp(ScopeLevel, 0, Mathf.Max(0, (w.scopeFovs?.Length ?? 1) - 1));

            bool firstPerson = w.aimFirstPerson && Aiming;
            FirstPersonBlend = w.aimFirstPerson ? Aim.Eased : 0f;
            float adsFov = w.HasScope ? w.scopeFovs[ScopeLevel] : w.aimFov;

            // sensibilidad: menor apuntando; con mira telescópica, proporcional al zoom (la mira no "vuela" con zoom alto)
            float sens = Aim.Sensitivity(Settings.sensitivity, w.aimSensitivity);
            if (w.aimFirstPerson && w.HasScope) sens *= Mathf.Lerp(1f, adsFov / Mathf.Max(1f, w.aimFov), FirstPersonBlend);
            CurrentSensitivity = sens;
            if (locked)
            {
                Vector2 look = GameInput.Look * sens;
                Yaw += look.x;
                Pitch = CameraMath.ClampPitch(Pitch - look.y, Settings.minPitch, Settings.maxPitch);
            }

            Recoil.Tick(dt, GameInput.FireHeld, w);

            // con la mira en primera persona: vaivén lento (más con fatiga), que Shift reduce conteniendo la respiración
            bool scoped = w.aimFirstPerson && FirstPersonBlend > 0.5f;
            Breath.Tick(dt, GameInput.SprintHeld, scoped, Settings);
            if (Breath.JustExhausted) Gasp();
            float swayAmp = scoped ? Settings.scopeSway * w.swayAmount * Breath.SwayMultiplier(Settings) * (1f + (RunCam != null ? RunCam.Fatigue : 0f)) * FirstPersonBlend : 0f;
            ScopeSway.Offset(Time.time, swayAmp, out float swayPitch, out float swayYaw);
            ScopeSwayOffset = new Vector2(swayYaw, swayPitch);
            UpdateRunCam(dt);

            // cámara principal: distancia, hombro (+ lean) y FOV con ease in/out
            float e = Aim.Eased;
            _follow.CameraDistance = Mathf.Lerp(Settings.hipDistance, w.aimDistance, e) + RunOutput.DistanceDelta;
            var so = _follow.ShoulderOffset;
            so.x = Mathf.Lerp(Settings.hipShoulderX, Settings.aimShoulderX, e) + LeanOffset;
            _follow.ShoulderOffset = so;
            _follow.CameraSide = Shoulder.CinemachineSide;
            var lens = _vcam.Lens;
            lens.FieldOfView = Aim.Fov(Settings.hipFov, w.aimFov);
            _vcam.Lens = lens;

            // cámara de mira
            if (_adsCam.gameObject.activeSelf != firstPerson)
            {
                _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, w.adsTime);
                _adsCam.gameObject.SetActive(firstPerson);
            }
            var al = _adsCam.Lens; al.FieldOfView = adsFov; _adsCam.Lens = al;
            CurrentFov = Mathf.Lerp(lens.FieldOfView + RunOutput.FovDelta, adsFov, FirstPersonBlend);

            SetBodyHidden(FirstPersonBlend > 0.5f);
        }

        private void UpdateRunCam(float dt)
        {
            if (_motor == null) return;
            Vector2 move = GameInput.Move;
            var input = new RunCamInput
            {
                Sprinting = _motor.IsSprinting,
                Aiming = Aiming,
                Crouched = _motor.IsCrouched,
                InCover = _cover != null && _cover.Active,
                Vaulting = _motor.IsVaulting,
                Airborne = !_motor.IsGrounded,
                MoveX = move.x, MoveY = move.y,
                Speed = _motor.HorizontalSpeed,
                MotionScale = Settings.motionScale,
            };
            RunOutput = RunCam.Tick(dt, input);
            if (RunOutput.Stepped && RunOutput.Weight > 0.2f) _impulse?.RunStep(RunOutput.Weight);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            // suavizado de rotación (menor al apuntar para que la mira responda)
            float smooth = Settings.rotationSmoothing * (Aiming ? 0.5f : 1f);
            float k = CameraMath.SmoothFactor(dt, smooth);
            _smYaw += CameraMath.DeltaAngle(_smYaw, Yaw) * k;
            _smPitch += (Pitch - _smPitch) * k;

            _pivotSmoothed = Mathf.Lerp(_pivotSmoothed, PivotHeight, 1f - Mathf.Exp(-12f * dt));
            _target.SetPositionAndRotation(_player.position + Vector3.up * (_pivotSmoothed + RunOutput.HeightDelta),
                Quaternion.Euler(_smPitch - Recoil.Pitch + ScopeSwayOffset.y, _smYaw + Recoil.Yaw + ScopeSwayOffset.x, 0f));

            if (SightPoint != null) _sightAnchor.SetPositionAndRotation(SightPoint.position, _target.rotation);
            else _sightAnchor.SetPositionAndRotation(_target.position, _target.rotation);
        }

        /// <summary>Se agotó el aire: jadeo audible y un pequeño cabeceo.</summary>
        private void Gasp()
        {
            if (_gasp == null) { _gasp = gameObject.AddComponent<AudioSource>(); _gasp.playOnAwake = false; _gasp.spatialBlend = 0f; }
            _gasp.pitch = 1.3f;
            _gasp.PlayOneShot(SevenDays.Weapons.ProceduralAudio.Breath(), 0.9f);
            _impulse?.Shake(0.12f);
        }

        private void SetBodyHidden(bool hidden)
        {
            if (hidden == _bodyHidden) return;
            _bodyHidden = hidden;
            foreach (var r in _bodyRenderers)
                if (r != null) r.shadowCastingMode = hidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

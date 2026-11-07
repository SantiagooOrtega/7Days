using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>Lo que el personaje está haciendo este frame (lo rellena un "driver": el del jugador lee los parámetros de PlayerAnimatorHooks).</summary>
    public struct RigInputs
    {
        public float MoveX, MoveY;          // movimiento local (blend tree), 1 = correr
        public bool Grounded, Aiming, Sprinting, InCover, Vaulting, BlindFiring, Dead;
        public float Crouch;                // 0..1
        public float Fatigue, Exposure, Lean;   // lean -1..1 (asomarse por el lado)
        public Vector3 LookDirection;       // hacia dónde mira la mira (mundo)
        public Vector3 DesiredForward;      // hacia dónde quiere girar el cuerpo (anticipación)
        // manos (mundo): peso 0 = brazo libre (balanceo), 1 = mano en el objetivo
        public Vector3 RightHand, LeftHand;
        public Quaternion RightHandRot, LeftHandRot;
        public float RightWeight, LeftWeight;
        public float ArmSwing;              // 0..1 amplitud del balanceo de los brazos libres
        // vault
        public float VaultProgress;
        public Vector3 VaultTop, VaultDir;
        public float Hunch;                 // 0..1 encorvado extra (zombis)
    }

    /// <summary>Suavizados de cada capa (segundos). Se pueden tocar en vivo desde el panel F6.</summary>
    [System.Serializable]
    public class RigSmoothing
    {
        public float pelvis = 0.06f, footPlanted = 0.02f, footSwing = 0.05f, torso = 0.12f, head = 0.06f, hands = 0.05f, weights = 0.15f, velocity = 0.08f;
    }

    /// <summary>
    /// Animación procedural por capas sobre un esqueleto humanoide propio (HumanoidRigSpec), sin clips:
    ///  1) LOCOMOCIÓN: zancada ligada a la distancia real recorrida (no patina), pies que se plantan en el suelo con raycast e IK de dos huesos, pelvis con
    ///     rebote, balanceo y giro de cadera; idle con cambios de peso y pasos correctivos; aire con piernas recogidas; aterrizaje amortiguado.
    ///  2) TORSO: inclinación al correr, contrabalanceo de hombros, respiración, lean lateral (cobertura), aim offset y anticipación del giro; cabeza que se adelanta.
    ///  3) BRAZOS Y ARMA: IK de dos huesos de cada mano hacia sus objetivos (empuñadura y guardamano) mezclada con el balanceo libre.
    ///  4) REACCIONES: retroceso por impactos según la dirección y muerte (se desploma y cae).
    /// Todo pasa por muelles críticos: ninguna pose cambia de golpe.
    /// </summary>
    [DefaultExecutionOrder(100)]   // después del movimiento, la cámara y la pose del arma: lee el estado final del frame
    public class ProceduralRig : MonoBehaviour
    {
        public RigProportions Proportions { get; private set; }
        public BoneDef[] Spec { get; private set; }
        public Transform[] Bones { get; private set; }
        public RigInputs Inputs;
        public RigSmoothing Smoothing = new RigSmoothing();
        public LayerMask GroundMask = ~((1 << 2) | (1 << 31));

        // --- datos públicos para la cámara, el sonido y el panel F6
        public float GaitPhase => _gait.Phase;
        public float CycleLength { get; private set; }
        public float StanceFraction { get; private set; }
        public float MeasuredSpeed { get; private set; }
        public readonly float[] SpeedWeights = new float[4];
        public readonly float[] DirectionWeights = new float[8];
        public bool[] FootPlanted { get; } = new bool[2];
        public Vector3[] FootTargets { get; } = new Vector3[2];
        public Vector3[] HandTargets { get; } = new Vector3[2];
        public float LastCostMs { get; private set; }
        /// <summary>Media móvil del coste (ms) y rayos al suelo lanzados en el último frame actualizado (panel F6).</summary>
        public float AverageCostMs { get; private set; }
        public int RaycastsLastUpdate { get; private set; }
        /// <summary>Cada cuántos frames se actualiza (LOD por distancia a la cámara; 1 = todos).</summary>
        public int UpdateInterval { get; private set; } = 1;
        /// <summary>Rigs activos (para medir el coste total con muchos personajes).</summary>
        public static readonly System.Collections.Generic.List<ProceduralRig> Active = new System.Collections.Generic.List<ProceduralRig>();
        public bool DrawGizmos;
        /// <summary>Un pie se apoyó desde la última vez que se preguntó (la cámara de carrera y los pasos lo consumen).</summary>
        public bool ConsumeStep() { bool s = _stepPending; _stepPending = false; return s; }

        private readonly GaitCycle _gait = new GaitCycle();
        private bool _stepPending;
        private float _legLen, _thigh, _shin, _upperArm, _lowerArm, _hipHeight, _hipHalf, _footHeight;
        private Vector3 _lastPos;
        private CriticalSpring3 _vel;
        private CriticalSpring _pelvisHeight, _landing, _sway, _hipYaw, _lean, _sideLean, _twist, _aimPitch, _crouch, _breath;
        private CriticalSpring _headYaw, _headPitch, _death, _rightW, _leftW, _air;
        private CriticalSpring3 _hit;
        private readonly CriticalSpring3[] _foot = new CriticalSpring3[2];
        private readonly CriticalSpring3[] _hand = new CriticalSpring3[2];
        private readonly Vector3[] _plant = new Vector3[2], _liftFrom = new Vector3[2];
        private readonly bool[] _wasStance = { true, true };
        private readonly float[] _idleStep = { -1f, -1f };
        private readonly Vector3[] _idleFrom = new Vector3[2], _idleTo = new Vector3[2];
        private float _time, _deathTime, _pendingDt, _distance;
        private int _frameOffset, _rays;
        private bool _footRays = true;
        private readonly float[] _landingY = new float[2], _landingRayAt = { -1f, -1f };
        private static Camera _cam;
        private static int _camFrame = -1;
        private bool _built;
        private static readonly System.Diagnostics.Stopwatch Watch = new System.Diagnostics.Stopwatch();

        public Transform Bone(BoneId id) => Bones[(int)id];

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); _frameOffset = Random.Range(0, 6); }
        private void OnDisable() => Active.Remove(this);

        /// <summary>Crea los huesos (GameObjects vacíos) en la pose de reposo. Las mallas se cuelgan después de cada hueso (RigVisuals).</summary>
        public void Build(RigProportions p)
        {
            Proportions = p ?? new RigProportions();
            Proportions.Validate();
            Spec = HumanoidRigSpec.Build(Proportions);
            Bones = new Transform[Spec.Length];
            for (int i = 0; i < Spec.Length; i++)
            {
                var t = new GameObject(Spec[i].Id.ToString()).transform;
                t.SetParent(Spec[i].Parent < 0 ? transform : Bones[Spec[i].Parent], false);
                var lp = Spec[i].LocalPosition;
                t.localPosition = new Vector3(lp.X, lp.Y, lp.Z);
                Bones[i] = t;
            }
            _thigh = Spec[(int)BoneId.LeftUpperLeg].Length; _shin = Spec[(int)BoneId.LeftLowerLeg].Length; _legLen = _thigh + _shin;
            _upperArm = Spec[(int)BoneId.LeftUpperArm].Length; _lowerArm = Spec[(int)BoneId.LeftLowerArm].Length;
            _hipHeight = Spec[(int)BoneId.Hips].LocalPosition.Y;
            _hipHalf = Mathf.Abs(Spec[(int)BoneId.LeftUpperLeg].LocalPosition.X);
            _footHeight = Spec[(int)BoneId.LeftFoot].Length;
            ResetPose();
            _built = true;
        }

        /// <summary>Coloca los pies bajo el cuerpo y pone los muelles en reposo (al aparecer o reutilizarse desde un pool).</summary>
        public void ResetPose()
        {
            _lastPos = transform.position;
            _vel.Reset(new V3(0, 0, 0));
            _pelvisHeight = new CriticalSpring(CrouchPose.PelvisHeight(_hipHeight, 0f));
            _death = new CriticalSpring(0f); _deathTime = -1f;
            _rightW = new CriticalSpring(0f); _leftW = new CriticalSpring(0f);
            _crouch = new CriticalSpring(0f); _air = new CriticalSpring(0f);
            _hit.Reset(new V3(0, 0, 0));
            for (int f = 0; f < 2; f++)
            {
                _plant[f] = RestFoot(f);
                _foot[f].Reset(ToV3(_plant[f]));
                _wasStance[f] = true; _idleStep[f] = -1f;
                _hand[f].Reset(ToV3(transform.position + Vector3.up * _hipHeight));
            }
            _gait.Reset();
        }

        /// <summary>Impacto: el torso retrocede en la dirección del golpe (mundo).</summary>
        public void Hit(Vector3 direction, float strength)
        {
            Vector3 local = transform.InverseTransformDirection(direction.sqrMagnitude > 1e-4f ? direction.normalized : -transform.forward);
            float k = Mathf.Clamp(strength, 0.2f, 3f) * 140f;
            _hit.X.Kick(local.z * k);    // golpe de frente: se echa atrás (pitch)
            _hit.Z.Kick(-local.x * k);   // de lado: se inclina (roll)
            _hit.Y.Kick(local.x * k * 0.5f);
        }

        /// <summary>Aterrizaje: la pelvis baja según la altura de la caída y vuelve con suavidad.</summary>
        public void Land(float fall) => _landing.Kick(-Mathf.Clamp(1.2f + fall * 0.9f, 1.2f, 6f));

        private void LateUpdate()
        {
            if (!_built) return;
            // LOD: lejos de la cámara se actualiza cada 2, 3 o 6 frames (con el tiempo acumulado, así la animación no se ralentiza) y sin rayos al suelo
            if (_camFrame != Time.frameCount) { _camFrame = Time.frameCount; _cam = Camera.main; }
            _distance = _cam != null ? Vector3.Distance(_cam.transform.position, transform.position) : 0f;
            UpdateInterval = RigLod.Interval(_distance);
            _footRays = RigLod.FootRaycasts(_distance);
            _pendingDt += Time.deltaTime;
            if (UpdateInterval > 1 && (Time.frameCount + _frameOffset) % UpdateInterval != 0) return;
            Watch.Restart();
            _rays = 0;
            float dt = Mathf.Min(_pendingDt, 0.1f);
            _pendingDt = 0f;
            if (dt <= 0f) return;
            _time += dt;
            var I = Inputs;
            var s = Smoothing;

            // --- velocidad medida del cuerpo (lo que de verdad se movió: CharacterController o NavMeshAgent)
            Vector3 pos = transform.position;
            Vector3 delta = pos - _lastPos; delta.y = 0f;
            _lastPos = pos;
            if (delta.magnitude > 2f) { ResetPose(); delta = Vector3.zero; }   // teletransporte (pool, cobertura): sin zancadas fantasma
            Vector3 rawVel = delta / dt;
            V3 v3 = _vel.Step(ToV3(rawVel), s.velocity, dt);
            Vector3 vel = new Vector3(v3.X, 0f, v3.Z);
            float speed = vel.magnitude;
            MeasuredSpeed = speed;

            // --- mezcla continua (blend tree propio)
            LocomotionBlend.SpeedWeights(speed, SpeedWeights);
            Vector3 localVel = transform.InverseTransformDirection(vel);
            LocomotionBlend.DirectionWeights(localVel.x, localVel.z, DirectionWeights);
            float run = SpeedWeights[LocomotionBlend.Run], jog = SpeedWeights[LocomotionBlend.Jog], walk = SpeedWeights[LocomotionBlend.Walk];
            float moving = 1f - SpeedWeights[LocomotionBlend.Idle];
            float crouch = _crouch.Step(I.Crouch, 0.12f, dt);
            float dead = _death.Step(I.Dead ? 1f : 0f, 0.35f, dt);
            if (I.Dead && _deathTime < 0f) _deathTime = _time;
            if (!I.Dead) _deathTime = -1f;
            float air = _air.Step(I.Grounded || I.Vaulting ? 0f : 1f, 0.08f, dt);

            // --- zancada (por distancia: sin patinar). Con fatiga, pasos más cortos.
            float legScale = _legLen / 0.86f;
            CycleLength = GaitCycle.CycleLength(speed, legScale, I.Fatigue * 0.12f) * Mathf.Lerp(1f, 0.75f, crouch);
            StanceFraction = GaitCycle.StanceFraction(speed);
            if (I.Grounded && !I.Vaulting && !I.Dead && speed > 0.15f)
            {
                if (_gait.Advance(delta.magnitude, CycleLength) >= 0) _stepPending = true;
            }

            UpdateFeet(I, vel, speed, moving, crouch, air, dt);
            UpdatePelvis(I, speed, run, jog, walk, moving, crouch, air, dead, localVel, dt);
            SolveLegs();
            UpdateTorso(I, run, jog, moving, crouch, dead, dt);
            UpdateArms(I, run, jog, walk, moving, dead, dt);
            UpdateDeathFall(dead);

            Watch.Stop();
            LastCostMs = (float)Watch.Elapsed.TotalMilliseconds;
            AverageCostMs = Mathf.Lerp(AverageCostMs, LastCostMs, 0.05f);
            RaycastsLastUpdate = _rays;
        }

        // ------------------------------------------------------------------ capa 1: locomoción
        private Vector3 RestFoot(int f, float crouch = 0f)
        {
            float side = f == 0 ? -1f : 1f;
            return transform.position + transform.rotation * new Vector3(side * CrouchPose.FootSide(_hipHalf, crouch), 0f, (f == 0 ? 0.02f : -0.02f) + CrouchPose.FootAhead(crouch));
        }

        private Vector3 Ground(Vector3 p, out Vector3 normal)
        {
            normal = Vector3.up;
            if (!_footRays) return new Vector3(p.x, transform.position.y, p.z);
            _rays++;
            if (Physics.Raycast(p + Vector3.up * 0.9f, Vector3.down, out RaycastHit hit, 2.2f, GroundMask, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return new Vector3(p.x, hit.point.y, p.z);
            }
            return new Vector3(p.x, transform.position.y, p.z);
        }

        private void UpdateFeet(RigInputs I, Vector3 vel, float speed, float moving, float crouch, float air, float dt)
        {
            float lift = GaitCycle.LiftHeight(speed) * Mathf.Lerp(1f, 0.6f, crouch);
            float cycleTime = speed > 0.05f ? CycleLength / speed : 1f;
            bool walking = I.Grounded && !I.Vaulting && !I.Dead && speed > 0.15f;
            for (int f = 0; f < 2; f++)
            {
                float side = f == 0 ? -1f : 1f;
                Vector3 hipW = transform.position + transform.rotation * new Vector3(side * CrouchPose.FootSide(_hipHalf, crouch), 0f, CrouchPose.FootAhead(crouch));
                Vector3 target;
                bool planted;
                if (walking)
                {
                    float fp = _gait.FootPhase(f);
                    bool stance = GaitCycle.InStance(fp, StanceFraction);
                    if (stance && !_wasStance[f]) { Vector3 p = _foot[f].Value.ToUnity(); _plant[f] = new Vector3(p.x, _landingY[f], p.z); }   // aterriza: queda clavado (altura ya medida en el vuelo)
                    if (!stance && _wasStance[f]) _liftFrom[f] = _plant[f];                             // despega
                    _wasStance[f] = stance;
                    if (stance) { target = _plant[f]; planted = true; _landingRayAt[f] = -1f; }
                    else
                    {
                        float swing = GaitCycle.SwingProgress(fp, StanceFraction);
                        float timeToPlant = (1f - swing) * (1f - StanceFraction) * cycleTime;
                        // dónde aterrizará: bajo la cadera a mitad del próximo apoyo; de lado, el pie que cruza pasa un poco por delante
                        V3 plant = FootPlacement.PredictPlant(ToV3(hipW), ToV3(vel), timeToPlant, StanceFraction * cycleTime, new V3(0, 0, 0));
                        // la altura del suelo donde aterrizará se mide como mucho 10 veces por segundo (no en cada frame)
                        Vector3 landing = plant.ToUnity();
                        if (_landingRayAt[f] < 0f || _time - _landingRayAt[f] > 0.1f) { _landingY[f] = Ground(landing, out _).y; _landingRayAt[f] = _time; }
                        landing.y = _landingY[f];
                        float crossing = Mathf.Abs(Vector3.Dot(vel.normalized, transform.right)) * 0.08f;
                        landing += transform.forward * crossing * Mathf.Sin(Mathf.PI * swing);
                        target = FootPlacement.Swing(ToV3(_liftFrom[f]), ToV3(landing), swing, lift).ToUnity();
                        planted = false;
                    }
                    _idleStep[f] = -1f;
                }
                else if (!I.Grounded && !I.Vaulting || I.Vaulting)
                {
                    // en el aire (salto, caída, vault): piernas recogidas bajo la cadera
                    float tuck = I.Vaulting ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(I.VaultProgress)) * 0.5f : 0.35f;
                    _landingY[f] = transform.position.y; _landingRayAt[f] = -1f;
                    target = transform.position + transform.rotation * new Vector3(side * _hipHalf, _hipHeight * tuck, (f == 0 ? 0.12f : -0.05f) + (I.Vaulting ? 0.25f : 0f));
                    _plant[f] = target; _wasStance[f] = true;
                    planted = false;
                }
                else
                {
                    // quieto: los pies se quedan; si el cuerpo gira o se aleja, un paso corto los recoloca (uno cada vez)
                    // quieto no se lanza ningún rayo: solo al empezar un paso correctivo se mide el suelo de destino
                    Vector3 rest = RestFoot(f, crouch);
                    if (_idleStep[f] < 0f && _idleStep[1 - f] < 0f && (Flat(_plant[f] - rest).magnitude > 0.18f))
                    { _idleStep[f] = 0f; _idleFrom[f] = _plant[f]; _idleTo[f] = Ground(rest, out _); }
                    if (_idleStep[f] >= 0f)
                    {
                        _idleStep[f] += dt / 0.22f;
                        target = FootPlacement.Swing(ToV3(_idleFrom[f]), ToV3(_idleTo[f]), Mathf.Clamp01(_idleStep[f]), 0.07f).ToUnity();
                        if (_idleStep[f] >= 1f) { _plant[f] = _idleTo[f]; _idleStep[f] = -1f; _stepPending = true; }
                        planted = false;
                    }
                    else { target = _plant[f]; planted = true; }
                    _wasStance[f] = true;
                }
                FootPlanted[f] = planted;
                V3 smoothed = _foot[f].Step(ToV3(target), planted ? Smoothing.footPlanted : Smoothing.footSwing, dt);
                FootTargets[f] = smoothed.ToUnity();
            }
        }

        private void UpdatePelvis(RigInputs I, float speed, float run, float jog, float walk, float moving, float crouch, float air, float dead, Vector3 localVel, float dt)
        {
            float phase = _gait.Phase * Mathf.PI * 2f;
            // rebote: baja en cada apoyo (dos veces por ciclo); más corriendo
            float bobAmp = 0.012f * walk + 0.022f * jog + 0.035f * run;
            float bob = -bobAmp * (0.5f + 0.5f * Mathf.Cos(phase * 2f));
            // pelvis más baja si un pie está más bajo que el cuerpo (bordes, rampas)
            float lowest = Mathf.Min(FootTargets[0].y, FootTargets[1].y) - transform.position.y;
            float terrain = Mathf.Clamp(lowest, -0.35f, 0f);
            // respiración y cambios de peso en reposo
            float idle = SpeedWeights[LocomotionBlend.Idle];
            float breath = Mathf.Sin(_time * Mathf.PI * 2f * (0.25f + 0.35f * I.Fatigue)) * (0.006f + 0.01f * I.Fatigue);
            float weightShift = Mathf.Sin(_time * 0.4f) * 0.025f * idle;
            float targetHeight = CrouchPose.PelvisHeight(_hipHeight, crouch) + bob + terrain + breath * idle - air * 0.04f;
            float h = _pelvisHeight.Step(targetHeight, Smoothing.pelvis, dt);
            float landing = _landing.Step(0f, 0.12f, dt);
            h += Mathf.Max(-0.35f, landing * 0.1f);
            h = Mathf.Lerp(h, 0.32f, Mathf.SmoothStep(0f, 1f, dead));

            float sway = _sway.Step(Mathf.Sin(phase) * 0.03f * (walk + jog * 0.7f + run * 0.4f) + weightShift + I.Lean * 0.12f, Smoothing.pelvis, dt);
            float hipYaw = _hipYaw.Step(Mathf.Sin(phase) * (6f * walk + 9f * jog + 12f * run), Smoothing.pelvis, dt);
            float tilt = 4f * run + 2f * jog + crouch * CrouchPose.PelvisTilt;

            var hips = Bone(BoneId.Hips);
            hips.localPosition = new Vector3(sway, h, CrouchPose.PelvisBack(crouch));
            hips.localRotation = Quaternion.Euler(tilt, hipYaw, -Mathf.Sin(phase) * 2f * moving);
        }

        private void SolveLegs()
        {
            for (int f = 0; f < 2; f++)
            {
                var upper = Bone(f == 0 ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg);
                var lower = Bone(f == 0 ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg);
                var foot = Bone(f == 0 ? BoneId.LeftFoot : BoneId.RightFoot);
                Vector3 hip = upper.position;
                Vector3 ankle = FootTargets[f] + Vector3.up * _footHeight;
                Vector3 fwd = transform.forward;
                Vector3 pole = CrouchPose.KneePole(ToV3(hip), ToV3(fwd), ToV3(transform.right), f == 0 ? -1f : 1f).ToUnity();   // rodillas hacia delante y algo hacia fuera: nunca se cruzan
                TwoBoneIK.Solve(ToV3(hip), ToV3(ankle), ToV3(pole), _thigh, _shin, out V3 mid, out V3 end);
                Vector3 knee = mid.ToUnity(), ank = end.ToUnity();
                Vector3 bendDir = Vector3.ProjectOnPlane(knee - (hip + ank) * 0.5f, (ank - hip).normalized);
                if (bendDir.sqrMagnitude < 1e-6f) bendDir = fwd;
                upper.rotation = Quaternion.LookRotation(bendDir.normalized, hip - knee);
                lower.rotation = Quaternion.LookRotation(bendDir.normalized, knee - ank);
                // pie plano sobre el suelo mirando hacia delante; punta levantada al despegar
                Vector3 footFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
                float toe = FootPlanted[f] ? 0f : -18f;
                foot.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(toe, Vector3.Cross(Vector3.up, footFwd)) * footFwd, Vector3.up);
            }
        }

        // ------------------------------------------------------------------ capa 2: torso y cabeza
        private void UpdateTorso(RigInputs I, float run, float jog, float moving, float crouch, float dead, float dt)
        {
            float phase = _gait.Phase * Mathf.PI * 2f;
            float lean = _lean.Step(14f * run + 6f * jog + crouch * CrouchPose.TorsoLean + I.Hunch * 35f + (Proportions.hunch * 30f) - (I.Aiming ? 6f * run : 0f), Smoothing.torso, dt);
            float side = _sideLean.Step(-I.Lean * 14f, Smoothing.torso * 0.8f, dt);
            // anticipación: el torso empieza a girar hacia donde quiere ir antes que las piernas
            float desiredYaw = I.DesiredForward.sqrMagnitude > 0.01f ? CameraMath.DeltaAngle(transform.eulerAngles.y, Quaternion.LookRotation(I.DesiredForward).eulerAngles.y) : 0f;
            float twist = _twist.Step(Mathf.Clamp(desiredYaw, -50f, 50f) * 0.6f, Smoothing.torso * 0.7f, dt);
            // contrabalanceo de hombros contra la cadera
            float counter = -Mathf.Sin(phase) * (8f * jog + 12f * run) * moving;
            // aim offset: el pecho sigue el pitch de la mira al apuntar
            float lookPitch = I.LookDirection.sqrMagnitude > 0.01f ? -Mathf.Asin(Mathf.Clamp(I.LookDirection.normalized.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            float aimPitch = _aimPitch.Step(I.Aiming ? lookPitch * 0.5f : lookPitch * 0.15f, Smoothing.torso * 0.6f, dt);
            float breath = _breath.Step(Mathf.Sin(_time * Mathf.PI * 2f * (0.25f + 0.35f * I.Fatigue)) * (0.8f + 2.5f * I.Fatigue), 0.05f, dt);
            V3 hit = _hit.Step(new V3(0, 0, 0), 0.18f, dt);
            float deathBend = dead * 35f;

            float pitch = lean + aimPitch + breath + deathBend;
            Bone(BoneId.Spine).localRotation = Quaternion.Euler(pitch * 0.3f + hit.X * 0.4f, twist * 0.2f + counter * 0.3f, side * 0.35f + hit.Z * 0.4f);
            Bone(BoneId.Chest).localRotation = Quaternion.Euler(pitch * 0.35f + hit.X * 0.35f, twist * 0.3f + counter * 0.35f + hit.Y * 0.5f, side * 0.35f + hit.Z * 0.35f);
            Bone(BoneId.UpperChest).localRotation = Quaternion.Euler(pitch * 0.35f - breath * 0.5f + hit.X * 0.25f, twist * 0.5f + counter * 0.35f + hit.Y * 0.5f, side * 0.3f + hit.Z * 0.25f);

            // cabeza: se adelanta (suavizado más rápido) y mira hacia la mira, con límites
            var upper = Bone(BoneId.UpperChest);
            Vector3 look = I.LookDirection.sqrMagnitude > 0.01f ? I.LookDirection : transform.forward;
            Vector3 localLook = Quaternion.Inverse(upper.rotation) * look;
            float yaw = Mathf.Clamp(Mathf.Atan2(localLook.x, localLook.z) * Mathf.Rad2Deg + twist * 0.4f, -70f, 70f);
            float pitchH = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localLook.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -45f, 50f);
            float hy = _headYaw.StepAngle(Mathf.Lerp(yaw, 0f, dead), Smoothing.head, dt);
            float hp = _headPitch.Step(Mathf.Lerp(pitchH, 40f, dead), Smoothing.head, dt);
            Bone(BoneId.Neck).localRotation = Quaternion.Euler(hp * 0.4f, hy * 0.4f, 0f);
            Bone(BoneId.Head).localRotation = Quaternion.Euler(hp * 0.6f, hy * 0.6f, dead * 25f);
        }

        // ------------------------------------------------------------------ capa 3: brazos y arma (IK)
        private void UpdateArms(RigInputs I, float run, float jog, float walk, float moving, float dead, float dt)
        {
            float phase = _gait.Phase * Mathf.PI * 2f;
            float rw = _rightW.Step(I.Dead ? 0f : I.RightWeight, Smoothing.weights, dt);
            float lw = _leftW.Step(I.Dead ? 0f : I.LeftWeight, Smoothing.weights, dt);
            for (int a = 0; a < 2; a++)
            {
                bool left = a == 0;
                float side = left ? -1f : 1f;
                var upper = Bone(left ? BoneId.LeftUpperArm : BoneId.RightUpperArm);
                var lower = Bone(left ? BoneId.LeftLowerArm : BoneId.RightLowerArm);
                var hand = Bone(left ? BoneId.LeftHand : BoneId.RightHand);
                Vector3 shoulder = upper.position;
                Vector3 chestFwd = Bone(BoneId.UpperChest).forward;
                Vector3 chestRight = Bone(BoneId.UpperChest).right;
                Vector3 up = Bone(BoneId.UpperChest).up;

                // brazo libre: cuelga y se balancea contrario a su pierna; corriendo, codo doblado y más amplitud
                float armLen = _upperArm + _lowerArm;
                float bend = Mathf.Lerp(0.95f, 0.72f, run + jog * 0.4f);
                float swing = Mathf.Sin(phase + (left ? 0f : Mathf.PI)) * (0.12f * walk + 0.2f * jog + 0.32f * run) * I.ArmSwing;
                float idleSway = Mathf.Sin(_time * 0.9f + side) * 0.015f;
                Vector3 free = shoulder - up * armLen * bend + chestFwd * (swing + 0.04f + run * 0.12f) + chestRight * side * (0.05f + idleSway) + up * run * 0.12f;
                if (dead > 0.01f) free = Vector3.Lerp(free, shoulder - up * armLen * 0.98f + chestRight * side * 0.15f, dead);

                float w = left ? lw : rw;
                Vector3 goal = Vector3.Lerp(free, left ? I.LeftHand : I.RightHand, w);
                V3 g = _hand[a].Step(ToV3(goal), Smoothing.hands, dt);
                Vector3 target = g.ToUnity();
                HandTargets[a] = target;

                Vector3 pole = shoulder - chestFwd * 0.5f + chestRight * side * 0.45f - up * 0.4f;   // codos atrás y hacia fuera
                TwoBoneIK.Solve(ToV3(shoulder), ToV3(target), ToV3(pole), _upperArm, _lowerArm, out V3 mid, out V3 end);
                Vector3 elbow = mid.ToUnity(), wrist = end.ToUnity();
                Vector3 bendDir = Vector3.ProjectOnPlane(elbow - (shoulder + wrist) * 0.5f, (wrist - shoulder).normalized);
                if (bendDir.sqrMagnitude < 1e-6f) bendDir = -chestFwd;
                upper.rotation = Quaternion.LookRotation(bendDir.normalized, shoulder - elbow);
                lower.rotation = Quaternion.LookRotation(bendDir.normalized, elbow - wrist);
                Quaternion freeRot = Quaternion.LookRotation(chestFwd, elbow - wrist);
                hand.rotation = Quaternion.Slerp(freeRot, left ? I.LeftHandRot : I.RightHandRot, w);
            }
        }

        // ------------------------------------------------------------------ capa 4: reacciones (muerte)
        private void UpdateDeathFall(float dead)
        {
            // primero se desploma (pelvis, rodillas, torso) y después cae de espaldas con suavidad: secuencia sin físicas, sin atravesar el suelo
            float fall = _deathTime < 0f ? 0f : Mathf.SmoothStep(0f, 1f, (_time - _deathTime - 0.35f) / 0.6f);
            var hips = Bone(BoneId.Hips);
            if (fall > 0f) hips.localRotation = hips.localRotation * Quaternion.Euler(-80f * fall, 0f, 10f * fall);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        private static V3 ToV3(Vector3 v) => new V3(v.x, v.y, v.z);

        private void OnDrawGizmos()
        {
            if (!DrawGizmos || Bones == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < Bones.Length; i++) if (Spec[i].Parent >= 0 && Bones[i] != null) Gizmos.DrawLine(Bones[Spec[i].Parent].position, Bones[i].position);
            Gizmos.color = Color.green; foreach (var f in FootTargets) Gizmos.DrawWireSphere(f, 0.05f);
            Gizmos.color = Color.yellow; foreach (var h in HandTargets) Gizmos.DrawWireSphere(h, 0.04f);
        }
    }

    public static class V3Extensions
    {
        public static Vector3 ToUnity(this V3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}

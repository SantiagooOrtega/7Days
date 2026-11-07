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
        public float Kneel;                 // 0..1: arrodillado (cobertura baja, quieto)
        public int KneelSide;               // +1: rodilla derecha al suelo; -1: izquierda
        public Collider CoverCollider;      // los pies no pueden entrar en la cobertura
        public float AimTwist;              // grados: postura de perfil al encarar un arma con culata
        public float CheekWeld;             // 0..1: la cabeza se pega a la culata (apuntando con rifle)
        public Vector3 RightHandGrip, LeftHandGrip;   // punto de agarre del arma que sujeta cada mano (mundo): para medir y depurar
        public float WeaponWeight;          // 0..1: armas pesadas bajan un poco el hombro y llevan más inercia
        public Transform HandSpace;         // las manos se suavizan en este espacio (el arma): al moverse el arma no se quedan atrás
    }

    /// <summary>Suavizados de cada capa (segundos). Se pueden tocar en vivo desde el panel F6.</summary>
    [System.Serializable]
    public class RigSmoothing
    {
        public float pelvis = 0.06f, footPlanted = 0.02f, footSwing = 0.05f, torso = 0.12f, head = 0.06f, hands = 0.05f, weights = 0.15f, velocity = 0.08f;
        /// <summary>Velocidad máxima de una mano yendo a otro objetivo (m/s) y giro máximo (°/s): como una mano real recargando, sin saltos.</summary>
        public float handSpeed = 2.6f, handTurn = 540f;
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
        /// <summary>Variante con la que está vestido (la pone RigVisuals).</summary>
        public CharacterVariantData Variant { get; set; }
        public BoneDef[] Spec { get; private set; }
        public Transform[] Bones { get; private set; }
        public RigInputs Inputs;
        public RigSmoothing Smoothing = new RigSmoothing();
        /// <summary>Pose de locomoción (flexión de rodilla por fase, rodar del pie...). Editable en F6.</summary>
        public LocomotionPoseData Pose = new LocomotionPoseData();
        /// <summary>Manos con dedos (las crea RigVisuals); el driver elige su pose.</summary>
        public RigHand LeftHandMesh, RightHandMesh;
        /// <summary>Flexión de codo de cada brazo (F6).</summary>
        public float[] ElbowFlexion { get; } = new float[2];
        public LayerMask GroundMask = ~((1 << 2) | (1 << 31));

        // --- datos públicos para la cámara, el sonido y el panel F6
        public float GaitPhase => _gait.Phase;
        /// <summary>Fase propia de un pie: 0 = contacto, hasta StanceFraction apoyado, después en el aire.</summary>
        public float FootPhase(int foot) => _gait.FootPhase(foot);
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
        /// <summary>true: no se anima solo en LateUpdate; lo avanza quien lo maneja con Tick (escenarios de prueba y ciclo F7).</summary>
        public bool ManualTick;
        /// <summary>Suelo bajo cada pie, flexión de cada rodilla y si la pelvis se limitó (para el panel F6).</summary>
        public float[] FootGround { get; } = new float[2];
        public float[] KneeFlexion { get; } = new float[2];
        /// <summary>Dónde puso el IK cada rodilla y tobillo (mundo). El panel "Esqueleto vs malla" lo compara con los huesos y las mallas.</summary>
        public Vector3[] IkKnees { get; } = new Vector3[2];
        public Vector3[] IkAnkles { get; } = new Vector3[2];
        public bool PelvisLimited { get; private set; }
        public float KneelWeight => _kneel.Value;
        /// <summary>Desplazamiento del pecho respecto a su reposo (espacio del cuerpo): el arma lo sigue con retraso elástico.</summary>
        public Vector3 ChestOffsetLocal { get; private set; }
        /// <summary>Un pie se apoyó desde la última vez que se preguntó (la cámara de carrera y los pasos lo consumen).</summary>
        public bool ConsumeStep() { bool s = _stepPending; _stepPending = false; return s; }

        private readonly GaitCycle _gait = new GaitCycle();
        private bool _stepPending;
        private float _legLen, _thigh, _shin, _upperArm, _lowerArm, _hipHeight, _hipHalf, _footHeight;
        private Vector3 _lastPos;
        private CriticalSpring3 _vel;
        private CriticalSpring _pelvisHeight, _landing, _sway, _hipYaw, _lean, _sideLean, _twist, _aimPitch, _crouch, _breath;
        private CriticalSpring _headYaw, _headPitch, _death, _rightW, _leftW, _air, _kneel, _aimTwist, _cheek;
        private float _runWeight, _moveLeanValue, _pelvisLeanValue;
        private CriticalSpring _moveLean;
        private float _hipJointDrop, _toeLength;
        private FootShape _footShape;
        private Vector3 _chestRestLocal;
        private readonly float[] _swingFrom = new float[2];
        private CriticalSpring3 _hit;
        private readonly CriticalSpring3[] _foot = new CriticalSpring3[2];
        private readonly CriticalSpring3[] _hand = new CriticalSpring3[2];
        private readonly Quaternion[] _handRot = { Quaternion.identity, Quaternion.identity };
        private readonly Vector3[] _handGoal = new Vector3[2];
        private Transform _handSpace;
        private bool _handSpaceSet;
        private CriticalSpring _shoulderDrop;
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

        /// <summary>Se llama tras colocar torso y cabeza y antes del IK de los brazos: aquí se pone el arma y se fijan las manos (Inputs) con el pecho de este frame.</summary>
        public event System.Action<ProceduralRig, float> BeforeArms;

        /// <summary>Medidas reales de la bota (las mide RigVisuals de la malla): con ellas ningún vértice del pie baja del suelo al inclinarlo.</summary>
        public void SetFootShape(FootShape shape) => _footShape = shape;
        public FootShape FootShapeMeasured => _footShape;

        /// <summary>Altura de la pelvis de pie: sale de la pierna real (bota medida + muslo + pantorrilla con la rodilla casi recta), no de un número aparte.</summary>
        public float StandPelvis => GaitPose.StandHipJoint(_footShape.Drop + LegSolver.Sole, _thigh, _shin, Pose.standKnee) + _hipJointDrop;
        /// <summary>Inclinación total al moverse (grados, F6) y la parte que pone la pelvis.</summary>
        public float MoveLean => _moveLeanValue;
        public float PelvisLean => _pelvisLeanValue;

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
            _hipJointDrop = -Spec[(int)BoneId.LeftUpperLeg].LocalPosition.Y;
            _toeLength = Spec[(int)BoneId.LeftToes].LocalPosition.Z;
            _footShape = FootShape.FromSpec(_footHeight, _toeLength);
            var chest = HumanoidRigSpec.RestPosition(Spec, BoneId.UpperChest);
            _chestRestLocal = new Vector3(chest.X, chest.Y - CrouchPose.StandBend, chest.Z);
            ResetPose();
            _built = true;
        }

        /// <summary>Vuelve a crear el esqueleto con otras proporciones (F6: "Aplicar complexión"). Las mallas las vuelve a colgar RigVisuals.Rebuild.</summary>
        public void Rebuild(RigProportions p)
        {
            if (Bones != null && Bones.Length > 0 && Bones[0] != null)
            {
                var old = Bones[0].gameObject;
                old.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            _built = false; _handSpaceSet = false;
            LeftHandMesh = RightHandMesh = null;
            Build(p);
        }

        /// <summary>Coloca los pies bajo el cuerpo y pone los muelles en reposo (al aparecer o reutilizarse desde un pool).</summary>
        public void ResetPose()
        {
            _lastPos = transform.position;
            _vel.Reset(new V3(0, 0, 0));
            _pelvisHeight = new CriticalSpring(StandPelvis);
            _death = new CriticalSpring(0f); _deathTime = -1f;
            _rightW = new CriticalSpring(0f); _leftW = new CriticalSpring(0f);
            _crouch = new CriticalSpring(0f); _air = new CriticalSpring(0f); _kneel = new CriticalSpring(0f);
            _hit.Reset(new V3(0, 0, 0));
            for (int f = 0; f < 2; f++)
            {
                _plant[f] = RestFoot(f);
                _foot[f].Reset(ToV3(_plant[f]));
                _wasStance[f] = true; _idleStep[f] = -1f;
                _hand[f].Reset(ToV3(transform.position + Vector3.up * _hipHeight));
                _handSpaceSet = false;
            }
            _gait.Reset();
        }

        /// <summary>
        /// Recoloca los pies bajo el cuerpo al instante. Al entrar en cobertura el jugador se teletransporta junto al muro: si los pies se quedaban clavados en el sitio
        /// anterior, la IK estiraba las piernas en diagonal hacia el suelo de atrás (se veían bajo el asfalto).
        /// </summary>
        public void SnapFeet()
        {
            for (int f = 0; f < 2; f++)
            {
                _plant[f] = Ground(RestFoot(f, _crouch.Value, _kneel.Value), out _);
                _foot[f].Reset(ToV3(_plant[f]));
                _wasStance[f] = true; _idleStep[f] = -1f; _landingRayAt[f] = -1f;
            }
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

        /// <summary>Retroceso del disparo: el pecho se echa un poco atrás y vuelve (los brazos lo siguen por la jerarquía).</summary>
        public void Recoil(float strength) => _hit.X.Kick(-Mathf.Clamp(strength, 0f, 2f) * 45f);

        /// <summary>Aterrizaje: la pelvis baja según la altura de la caída y vuelve con suavidad.</summary>
        public void Land(float fall) => _landing.Kick(-Mathf.Clamp(1.2f + fall * 0.9f, 1.2f, 6f));

        private void LateUpdate()
        {
            if (!_built || ManualTick) return;
            // LOD: lejos de la cámara se actualiza cada 2, 3 o 6 frames (con el tiempo acumulado, así la animación no se ralentiza) y sin rayos al suelo
            if (_camFrame != Time.frameCount) { _camFrame = Time.frameCount; _cam = Camera.main; }
            _distance = _cam != null ? Vector3.Distance(_cam.transform.position, transform.position) : 0f;
            UpdateInterval = RigLod.Interval(_distance);
            _footRays = RigLod.FootRaycasts(_distance);
            _pendingDt += Time.deltaTime;
            if (UpdateInterval > 1 && (Time.frameCount + _frameOffset) % UpdateInterval != 0) return;
            float dt = Mathf.Min(_pendingDt, 0.1f);
            _pendingDt = 0f;
            Tick(dt);
        }

        /// <summary>Avanza la animación "dt" segundos con las entradas actuales (lo llama LateUpdate; las pruebas y el ciclo F7 lo llaman directamente).</summary>
        public void Tick(float dt)
        {
            if (!_built || dt <= 0f) return;
            Watch.Restart();
            _rays = 0;
            _time += dt;
            var I = Inputs;
            var s = Smoothing;

            // --- velocidad medida del cuerpo (lo que de verdad se movió: CharacterController o NavMeshAgent)
            Vector3 pos = transform.position;
            Vector3 delta = pos - _lastPos; delta.y = 0f;
            _lastPos = pos;
            if (delta.magnitude > 3f) { ResetPose(); delta = Vector3.zero; }                        // teletransporte largo (pool): pose nueva
            else if (delta.magnitude > 0.4f && delta.magnitude / dt > 12f) { SnapFeet(); delta = Vector3.zero; }   // salto corto (entrar en cobertura): pies bajo el cuerpo ya
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
            CycleLength = GaitCycle.CycleLength(speed, legScale, I.Fatigue * 0.12f) * Mathf.Lerp(1f, 0.75f, crouch) * Mathf.Lerp(1f, 0.6f, _kneel.Value);
            // el apoyo dura lo que la pierna alcanza sin hundir la cadera (corriendo hay vuelo y la cadera va alta)
            StanceFraction = GaitPose.ReachStance(GaitCycle.StanceFraction(speed), CycleLength, StandPelvis - _hipJointDrop,
                FootGeometry.MinAnkleHeight(_footShape, -Pose.heelStrikePitch, 0f), _footShape.Ball * 0.6f, Pose, _thigh, _shin);
            if (I.Grounded && !I.Vaulting && !I.Dead && speed > 0.15f)
            {
                if (_gait.Advance(delta.magnitude, CycleLength) >= 0) _stepPending = true;
            }

            UpdateFeet(I, vel, speed, moving, crouch, air, dt);
            UpdatePelvis(I, speed, run, jog, walk, moving, crouch, air, dead, localVel, dt);
            SolveLegs();
            UpdateTorso(I, run, jog, moving, crouch, dead, dt);
            // el arma y los objetivos de las manos se colocan AQUÍ, con el pecho de este frame (si no, las manos irían un frame por detrás del arma)
            if (BeforeArms != null) { BeforeArms(this, dt); I = Inputs; }
            UpdateArms(I, run, jog, walk, moving, dead, dt);
            UpdateDeathFall(dead);

            ChestOffsetLocal = transform.InverseTransformPoint(Bone(BoneId.UpperChest).position) - _chestRestLocal;
            Watch.Stop();
            LastCostMs = (float)Watch.Elapsed.TotalMilliseconds;
            AverageCostMs = Mathf.Lerp(AverageCostMs, LastCostMs, 0.05f);
            RaycastsLastUpdate = _rays;
        }

        // ------------------------------------------------------------------ capa 1: locomoción
        private Vector3 RestFoot(int f, float crouch = 0f, float kneel = 0f)
        {
            float side = f == 0 ? -1f : 1f;
            Vector3 local = new Vector3(side * CrouchPose.FootSide(_hipHalf, crouch), 0f, (f == 0 ? 0.02f : -0.02f) + CrouchPose.FootAhead(crouch));
            if (kneel > 0.001f)
            {
                // arrodillado: el pie de la rodilla apoyada queda detrás (pantorrilla plana) y el otro delante, bajo la rodilla doblada
                // media rodilla: la pierna del hombro activo va atrás (rodilla casi en el suelo), la otra delante a ~90° con el pie plano
                bool rear = (side > 0f) == (Inputs.KneelSide >= 0);
                float hipJ = HalfKneel.HipJointHeight(_thigh);
                V3 k = rear ? HalfKneel.RearAnkle(side * _hipHalf, _shin, FootGeometry.MinAnkleHeight(_footShape, HalfKneel.RearFootPitch, FootGeometry.FlatToeBend(HalfKneel.RearFootPitch))) : HalfKneel.FrontAnkle(side * _hipHalf, _thigh, _shin, _footHeight, hipJ);
                local = Vector3.Lerp(local, new Vector3(k.X * 1.1f, 0f, k.Z), kneel);
            }
            return transform.position + transform.rotation * local;
        }

        /// <summary>Un pie nunca entra en el collider de la cobertura: si cae dentro, sale por el lado libre más cercano (en horizontal).</summary>
        private static Vector3 OutOfCover(Vector3 p, Collider c)
        {
            if (!(c is BoxCollider box)) return p;
            Vector3 local = box.transform.InverseTransformPoint(p) - box.center;
            Vector3 half = box.size * 0.5f;
            float m = 0.12f / Mathf.Max(0.01f, box.transform.lossyScale.x);
            if (!ShotOrigin.PushOut(local.x, local.y, local.z, -half.x, -half.y, -half.z, half.x, half.y, half.z, m, PopDirection.Left, out float ox, out float oy, out float oz)) return p;
            Vector3 world = box.transform.TransformPoint(new Vector3(ox, oy, oz) + box.center);
            return new Vector3(world.x, p.y, world.z);
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

        private static bool walkingNow(RigInputs I, float speed) => I.Grounded && !I.Vaulting && !I.Dead && speed > 0.15f;

        private void UpdateFeet(RigInputs I, Vector3 vel, float speed, float moving, float crouch, float air, float dt)
        {
            // deslizándose en cobertura baja se mantiene medio arrodillado (pasos cortos y pelvis baja); quieto, media rodilla completa
            float kneel = _kneel.Step(walkingNow(I, speed) ? I.Kneel * 0.5f : I.Kneel, 0.15f, dt);
            _runWeight = SpeedWeights[LocomotionBlend.Run] + SpeedWeights[LocomotionBlend.Jog] * 0.4f;
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
                    if (stance) { target = _plant[f]; planted = true; _landingRayAt[f] = -1f; FootGround[f] = _plant[f].y; }
                    else
                    {
                        float swing = GaitCycle.SwingProgress(fp, StanceFraction);
                        float timeToPlant = (1f - swing) * (1f - StanceFraction) * cycleTime;
                        // dónde aterrizará: bajo la cadera a mitad del próximo apoyo; de lado, el pie que cruza pasa un poco por delante
                        V3 plant = FootPlacement.PredictPlant(ToV3(hipW), ToV3(vel), timeToPlant, StanceFraction * cycleTime * 0.84f, new V3(0, 0, 0));   // toca algo más cerca del cuerpo: al despegar, la punta da alcance de más
                        // la altura del suelo donde aterrizará se mide como mucho 10 veces por segundo (no en cada frame)
                        Vector3 landing = plant.ToUnity();
                        if (_landingRayAt[f] < 0f || _time - _landingRayAt[f] > 0.1f) { _landingY[f] = Ground(landing, out _).y; _landingRayAt[f] = _time; }
                        landing.y = _landingY[f];
                        float crossing = Mathf.Abs(Vector3.Dot(vel.normalized, transform.right)) * 0.08f;
                        landing += transform.forward * crossing * Mathf.Sin(Mathf.PI * swing);
                        Vector3 planned = FootPlacement.Swing(ToV3(_liftFrom[f]), ToV3(landing), swing, lift).ToUnity();
                        FootGround[f] = Mathf.Lerp(_liftFrom[f].y, landing.y, swing);
                        // pierna en el aire con flexión real: muslo adelante y talón hacia el glúteo (cinemática directa en el plano del movimiento), que converge
                        // al punto de aterrizaje al final del vuelo (así no patina). Solo hacia delante; hacia atrás o de lado se usa el arco planeado.
                        Vector3 moveDir = vel.sqrMagnitude > 0.01f ? vel.normalized : transform.forward;
                        float forwardness = Vector3.Dot(moveDir, transform.forward);
                        if (forwardness > 0.3f && crouch < 0.5f)
                        {
                            float knee = GaitPose.KneeFlexion(Pose, fp, StanceFraction, _runWeight);
                            float thighA = GaitPose.ThighAngle(Pose, fp, StanceFraction, _runWeight);
                            V3 a = GaitPose.AnkleFromAngles(thighA, knee, _thigh, _shin);
                            Vector3 hipJoint = transform.position + transform.right * side * _hipHalf + Vector3.up * (_pelvisHeight.Value - _hipJointDrop);
                            Vector3 fk = hipJoint + Vector3.up * a.Y + moveDir * a.Z - Vector3.up * _footHeight;
                            float w = GaitPose.Smooth((swing - Pose.swingBlend) / (1f - Pose.swingBlend));
                            target = Vector3.Lerp(Vector3.Lerp(planned, fk, Mathf.Clamp01((forwardness - 0.3f) / 0.4f)), planned, w);
                        }
                        else target = planned;
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
                    FootGround[f] = transform.position.y - 10f;   // en el aire no hay suelo que limite
                    planted = false;
                }
                else
                {
                    // quieto: los pies se quedan; si el cuerpo gira o se aleja, un paso corto los recoloca (uno cada vez)
                    // quieto no se lanza ningún rayo: solo al empezar un paso correctivo se mide el suelo de destino
                    Vector3 rest = RestFoot(f, crouch, kneel);
                    if (_idleStep[f] < 0f && _idleStep[1 - f] < 0f && (Flat(_plant[f] - rest).magnitude > 0.18f))
                    { _idleStep[f] = 0f; _idleFrom[f] = _plant[f]; _idleTo[f] = Ground(rest, out _); }
                    if (_idleStep[f] >= 0f)
                    {
                        _idleStep[f] += dt / 0.22f;
                        target = FootPlacement.Swing(ToV3(_idleFrom[f]), ToV3(_idleTo[f]), Mathf.Clamp01(_idleStep[f]), 0.07f).ToUnity();
                        FootGround[f] = Mathf.Lerp(_idleFrom[f].y, _idleTo[f].y, Mathf.Clamp01(_idleStep[f]));
                        if (_idleStep[f] >= 1f) { _plant[f] = _idleTo[f]; _idleStep[f] = -1f; _stepPending = true; }
                        planted = false;
                    }
                    else { target = _plant[f]; planted = true; FootGround[f] = _plant[f].y; }
                    _wasStance[f] = true;
                }
                if (I.CoverCollider != null) target = OutOfCover(target, I.CoverCollider);
                FootPlanted[f] = planted;
                // en el aire el arco ya es suave: el muelle solo quita tirones. Su retraso crece con la velocidad (a 6.8 m/s, ~30 cm): el pie aterrizaba bajo la
                // cadera y el talón no subía. Por eso el suavizado del vuelo se acorta con la velocidad.
                float swingSmooth = Smoothing.footSwing / (1f + speed * 1.5f);
                V3 smoothed = _foot[f].Step(ToV3(target), planted ? Smoothing.footPlanted : swingSmooth, dt);
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
            float kneel = _kneel.Value;
            float stand = StandPelvis;
            float baseHeight = Mathf.Lerp(Mathf.Lerp(stand, CrouchPose.PelvisHeight(_hipHeight, 1f), crouch), HalfKneel.HipJointHeight(_thigh) + _hipJointDrop, kneel);
            // caminando/corriendo: la pelvis va a la altura que da la flexión de rodilla de cada fase en las piernas apoyadas (baja al contactar: absorción;
            // sube en el apoyo medio). Sustituye al rebote genérico. Si ninguna pierna está apoyada (vuelo al correr), conserva la altura.
            float gaitH = float.MaxValue;
            if (moving > 0.05f && I.Grounded && !I.Vaulting && !I.Dead)
                for (int f = 0; f < 2; f++)
                {
                    if (!FootPlanted[f]) continue;
                    float fp = _gait.FootPhase(f), side = f == 0 ? -1f : 1f;
                    float knee = GaitPose.KneeFlexion(Pose, fp, StanceFraction, _runWeight);
                    Vector3 hipW = transform.position + transform.right * side * _hipHalf;
                    float horiz = Flat(FootTargets[f] - hipW).magnitude;
                    // la amortiguación llega justo DESPUÉS del contacto (campana en el primer 40 % del apoyo): al tocar, la rodilla está a la de contacto (~15°)
                    float contact = Mathf.Sin(Mathf.PI * Mathf.Clamp01(fp / Mathf.Max(0.01f, StanceFraction * 0.4f)));
                    // tobillo a la altura real de la bota inclinada (al despegar rueda sobre la bola y el tobillo sube): el pie de atrás no hunde la cadera
                    float footPitch = GaitPose.FootPitch(Pose, fp, StanceFraction);
                    float ankleUp = FootGeometry.MinAnkleHeight(_footShape, footPitch, FootGeometry.FlatToeBend(footPitch));
                    // corriendo, la amortiguación ya la da la rodilla del apoyo medio: "absorb" es para caminar
                    float hipJ = FootGround[f] + ankleUp + GaitPose.HipAboveAnkle(knee, horiz, _thigh, _shin) - Pose.absorb * contact * (1f - _runWeight);
                    gaitH = Mathf.Min(gaitH, hipJ - transform.position.y + _hipJointDrop);
                }
            // en el vuelo (ningún pie apoyado) la cadera vuelve arriba; nunca más alta que de pie ni más baja que lo que permite el apoyo (sin hundirse al correr)
            if (gaitH == float.MaxValue && moving > 0.05f && I.Grounded && !I.Vaulting && !I.Dead)
            {
                // vuelo: la cadera sube y baja en parábola; al final ya está a la altura con la que la pierna que aterriza toca con la rodilla de contacto (~15°)
                // donde va a caer el pie (el muelle de la pelvis tarda 1-2 frames: si llegara al tocar, la pierna aterrizaría estirada)
                float fd = GaitPose.FlightDrop(Pose, _gait.Phase, StanceFraction);
                gaitH = stand - (fd > 0f ? fd : 0f);
                int land = _gait.FootPhase(0) > _gait.FootPhase(1) ? 0 : 1;
                float swing = GaitCycle.SwingProgress(_gait.FootPhase(land), StanceFraction);
                if (swing > 0.6f)
                {
                    // delante de la cadera en el primer frame de apoyo (aterriza para quedar bajo ella a ~mitad del apoyo; corriendo, el cuerpo avanza 11 cm por
                    // frame y en ese primer frame el pie ya está a ~70 % de la distancia prevista: medido en las pruebas visuales)
                    float horiz = StanceFraction * CycleLength * 0.42f * 0.7f;
                    float hipLand = _landingY[land] + FootGeometry.MinAnkleHeight(_footShape, -Pose.heelStrikePitch, 0f) + GaitPose.HipAboveAnkle(Pose.contactKnee, horiz, _thigh, _shin)
                                    - transform.position.y + _hipJointDrop;
                    gaitH = Mathf.Min(gaitH, Mathf.Lerp(gaitH, hipLand, (swing - 0.6f) / 0.4f));
                }
            }
            if (gaitH == float.MaxValue) gaitH = stand;
            gaitH = Mathf.Clamp(gaitH, stand - Pose.stanceDrop - 0.03f, stand);
            float gaitWeight = moving * (1f - crouch) * (1f - kneel) * (I.Grounded ? 1f : 0f);
            float walkHeight = Mathf.Lerp(baseHeight, gaitH, gaitWeight);
            float targetHeight = walkHeight + bob * (1f - gaitWeight) + terrain + breath * idle - air * 0.04f;
            float h = _pelvisHeight.Step(targetHeight, Smoothing.pelvis / (1f + speed * 0.5f), dt);   // corriendo el apoyo dura ~80 ms: un muelle lento aplanaría el rebote
            float landing = _landing.Step(0f, 0.12f, dt);
            h += Mathf.Max(-0.35f, landing * 0.1f);
            // regla dura: la pelvis no baja más de lo que las piernas permiten (rodilla <= 150°) con los pies en el suelo; en vez de hundir el pie, se queda
            PelvisLimited = false;
            if (dead < 0.01f && I.Grounded)
            {
                float minH = float.MinValue;
                for (int f = 0; f < 2; f++)
                {
                    float side = f == 0 ? -1f : 1f;
                    Vector3 hipW = transform.position + transform.right * side * _hipHalf;
                    float horiz = Flat(FootTargets[f] - hipW).magnitude;
                    minH = Mathf.Max(minH, LegSolver.MinHipHeight(FootGround[f], _footHeight, horiz, _thigh, _shin) - transform.position.y + _hipJointDrop);
                }
                if (h < minH) { h = minH; _pelvisHeight.Value = Mathf.Max(_pelvisHeight.Value, minH); PelvisLimited = true; }
            }
            h = Mathf.Lerp(h, 0.32f, Mathf.SmoothStep(0f, 1f, dead));

            // quieto, la pelvis se centra sobre la base de apoyo (entre los dos pies); al asomarse por el lado la pelvis NO se mueve: se inclina el torso
            Vector3 feetCenter = transform.InverseTransformPoint((FootTargets[0] + FootTargets[1]) * 0.5f);
            float centerX = Mathf.Clamp(feetCenter.x, -0.12f, 0.12f) * 0.6f * idle;
            float sway = _sway.Step(Mathf.Sin(phase) * 0.03f * (walk + jog * 0.7f + run * 0.4f) + weightShift + centerX, Smoothing.pelvis, dt);
            float hipYaw = _hipYaw.Step(Mathf.Sin(phase) * Mathf.Lerp(Pose.walkHipYaw, Pose.runHipYaw, _runWeight) * moving, Smoothing.pelvis, dt);
            // inclinación al moverse (suave, muelle crítico): con arma en las manos menos; la pelvis pone 2-4° y la columna el resto
            float armed = Mathf.Max(_rightW.Value, _leftW.Value);
            _moveLeanValue = _moveLean.Step(GaitPose.MoveLean(Pose, walk, jog, run, armed) * (1f - kneel), 0.25f, dt);
            _pelvisLeanValue = GaitPose.PelvisLean(Pose, _moveLeanValue);
            float tilt = _pelvisLeanValue + crouch * CrouchPose.PelvisTilt * (1f - kneel);

            var hips = Bone(BoneId.Hips);
            float centerZ = Mathf.Clamp(feetCenter.z, -0.15f, 0.15f) * 0.5f * idle * (1f - kneel);
            hips.localPosition = new Vector3(sway, h, Mathf.Lerp(CrouchPose.PelvisBack(crouch), 0.02f, kneel) + centerZ);
            hips.localRotation = Quaternion.Euler(tilt, hipYaw, -Mathf.Sin(phase) * 2f * moving);
        }

        private void SolveLegs()
        {
            float kneel = _kneel.Value;
            for (int f = 0; f < 2; f++)
            {
                float side = f == 0 ? -1f : 1f;
                var upper = Bone(f == 0 ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg);
                var lower = Bone(f == 0 ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg);
                var foot = Bone(f == 0 ? BoneId.LeftFoot : BoneId.RightFoot);
                Vector3 hip = upper.position;
                Vector3 fwd = transform.forward;
                bool kneelingLeg = kneel > 0.5f && (side > 0f) == (Inputs.KneelSide >= 0);
                bool frontKneelLeg = kneel > 0.5f && !kneelingLeg;
                Vector3 pole = kneelingLeg
                    ? transform.TransformPoint(HalfKneel.RearKnee(side * _hipHalf).ToUnity()) + fwd * 0.5f      // rodilla trasera hacia el suelo
                    : frontKneelLeg ? hip + fwd * 1.2f + Vector3.up * 0.3f + transform.right * side * 0.15f   // delantera: adelante, a ~90°
                    : CrouchPose.KneePole(ToV3(hip), ToV3(fwd), ToV3(transform.right), side).ToUnity();       // rodillas hacia delante y algo hacia fuera: nunca se cruzan
                Vector3 ankleTarget = FootTargets[f] + Vector3.up * _footHeight;
                // inclinación del pie: talón al contactar, plano, punta al despegar; en el aire algo de punta arriba; arrodillado, el pie de atrás apoya la punta
                bool walkingLegs = MeasuredSpeed > 0.15f && Inputs.Grounded && !Inputs.Vaulting && !Inputs.Dead;
                float pitch = walkingLegs ? GaitPose.FootPitch(Pose, _gait.FootPhase(f), StanceFraction) : (FootPlanted[f] ? 0f : -18f);
                if (kneelingLeg) pitch = Mathf.Lerp(pitch, HalfKneel.RearFootPitch, (kneel - 0.5f) * 2f);
                bool onGround = FootPlanted[f] || kneelingLeg;
                // con el pie apoyado la punta se dobla para quedar plana (rueda sobre la bola); la altura mínima del tobillo sale de la bota REAL inclinada
                float toeBend = onGround ? FootGeometry.FlatToeBend(pitch) : 0f;
                float minAnkle = FootGeometry.MinAnkleHeight(_footShape, pitch, toeBend);
                if (onGround) ankleTarget.y = Mathf.Max(ankleTarget.y, FootGround[f] + minAnkle);

                // reglas duras: ningún vértice de la bota ni la rodilla bajo el suelo, rodilla 0-150° sin hiperextensión
                var r = LegSolver.Solve(ToV3(hip), ToV3(ankleTarget), FootGround[f], minAnkle - LegSolver.Sole, ToV3(pole), _thigh, _shin);
                // sentadilla muy profunda: se levanta el talón (la punta sigue en el suelo) para no forzar la rodilla
                float heel = FootPlanted[f] && !kneelingLeg ? Mathf.Clamp01((r.KneeFlexion - 120f) / 30f) * 25f : 0f;
                if (heel > 0.5f)
                {
                    pitch = Mathf.Max(pitch, heel); toeBend = FootGeometry.FlatToeBend(pitch);
                    minAnkle = FootGeometry.MinAnkleHeight(_footShape, pitch, toeBend);
                    ankleTarget.y = Mathf.Max(ankleTarget.y, FootGround[f] + minAnkle);
                    r = LegSolver.Solve(ToV3(hip), ToV3(ankleTarget), FootGround[f], minAnkle - LegSolver.Sole, ToV3(pole), _thigh, _shin);
                }
                KneeFlexion[f] = r.KneeFlexion;

                Vector3 knee = r.Knee.ToUnity(), ank = r.Ankle.ToUnity();
                IkKnees[f] = knee; IkAnkles[f] = ank;
                Vector3 bendDir = Vector3.ProjectOnPlane(knee - (hip + ank) * 0.5f, (ank - hip).normalized);
                if (bendDir.sqrMagnitude < 1e-6f) bendDir = fwd;
                upper.rotation = SegmentRotation(hip, knee, bendDir);
                lower.rotation = SegmentRotation(knee, ank, bendDir);
                Vector3 footFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
                foot.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(pitch, Vector3.Cross(Vector3.up, footFwd)) * footFwd, Vector3.up);
                Bone(f == 0 ? BoneId.LeftToes : BoneId.RightToes).localRotation = Quaternion.Euler(-toeBend, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ capa 2: torso y cabeza
        private void UpdateTorso(RigInputs I, float run, float jog, float moving, float crouch, float dead, float dt)
        {
            float phase = _gait.Phase * Mathf.PI * 2f;
            float kneelW = _kneel.Value;
            // con arma en las manos el pecho va erguido: la inclinación la pone la parte baja de la columna
            float armedW = Mathf.Max(_rightW.Value, _leftW.Value) * (1f - kneelW);
            float spineShare = 0.3f + 0.25f * armedW, upperShare = 0.35f - 0.25f * armedW;
            // el ángulo pedido (correr, media rodilla) es el que SE VE: la línea de la cadera al cuello. Repartido entre vértebras, esa línea gira menos que el pecho,
            // así que se divide por cuánto la gira cada grado de columna (depende de los largos de los segmentos y del reparto)
            float line = SpineLineFactor(spineShare, upperShare);
            float visible = (_moveLeanValue - _pelvisLeanValue + kneelW * Pose.kneelTorsoLean) / line;
            float lean = _lean.Step(visible + crouch * CrouchPose.TorsoLean * (1f - kneelW) + I.Hunch * 35f + (Proportions.hunch * 30f), Smoothing.torso, dt);
            float side = _sideLean.Step(-I.Lean * 14f, Smoothing.torso * 0.8f, dt);
            // anticipación: el torso empieza a girar hacia donde quiere ir antes que las piernas
            float desiredYaw = I.DesiredForward.sqrMagnitude > 0.01f ? CameraMath.DeltaAngle(transform.eulerAngles.y, Quaternion.LookRotation(I.DesiredForward).eulerAngles.y) : 0f;
            // postura de perfil al encarar un arma con culata: el hombro izquierdo se adelanta y llega al guardamano
            float twist = _twist.Step(Mathf.Clamp(desiredYaw, -50f, 50f) * 0.6f, Smoothing.torso * 0.7f, dt) + _aimTwist.Step(I.AimTwist, 0.12f, dt);
            // contrabalanceo de hombros contra la cadera
            float counter = -Mathf.Sin(phase) * (8f * jog + 12f * run) * moving;
            // aim offset: el pecho sigue el pitch de la mira al apuntar
            float lookPitch = I.LookDirection.sqrMagnitude > 0.01f ? -Mathf.Asin(Mathf.Clamp(I.LookDirection.normalized.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            float aimPitch = _aimPitch.Step(I.Aiming ? lookPitch * 0.5f : lookPitch * 0.15f, Smoothing.torso * 0.6f, dt);
            float breath = _breath.Step(Mathf.Sin(_time * Mathf.PI * 2f * (0.25f + 0.35f * I.Fatigue)) * (0.8f + 2.5f * I.Fatigue), 0.05f, dt);
            V3 hit = _hit.Step(new V3(0, 0, 0), 0.18f, dt);
            float deathBend = dead * 35f;

            float pitch = lean + aimPitch + breath + deathBend;
            Bone(BoneId.Spine).localRotation = Quaternion.Euler(pitch * spineShare + hit.X * 0.4f, twist * 0.2f + counter * 0.3f, side * 0.35f + hit.Z * 0.4f);
            Bone(BoneId.Chest).localRotation = Quaternion.Euler(pitch * 0.35f + hit.X * 0.35f, twist * 0.3f + counter * 0.35f + hit.Y * 0.5f, side * 0.35f + hit.Z * 0.35f);
            Bone(BoneId.UpperChest).localRotation = Quaternion.Euler(pitch * upperShare - breath * 0.5f + hit.X * 0.25f, twist * 0.5f + counter * 0.35f + hit.Y * 0.5f, side * 0.3f + hit.Z * 0.25f);

            // cabeza: se adelanta (suavizado más rápido) y mira hacia la mira, con límites
            var upper = Bone(BoneId.UpperChest);
            Vector3 look = I.LookDirection.sqrMagnitude > 0.01f ? I.LookDirection : transform.forward;
            Vector3 localLook = Quaternion.Inverse(upper.rotation) * look;
            float yaw = Mathf.Clamp(Mathf.Atan2(localLook.x, localLook.z) * Mathf.Rad2Deg + twist * 0.4f, -70f, 70f);
            float pitchH = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localLook.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -45f, 50f);
            float hy = _headYaw.StepAngle(Mathf.Lerp(yaw, 0f, dead), Smoothing.head, dt);
            float hp = _headPitch.Step(Mathf.Lerp(pitchH, 40f, dead), Smoothing.head, dt);
            // mejilla a la culata: la cabeza se inclina hacia el arma y un poco adelante (la mira queda alineada con el ojo)
            float cheek = _cheek.Step(I.CheekWeld, 0.1f, dt);
            Bone(BoneId.Neck).localRotation = Quaternion.Euler(hp * 0.4f + cheek * 6f, hy * 0.4f, cheek * 5f);
            Bone(BoneId.Head).localRotation = Quaternion.Euler(hp * 0.6f + cheek * 4f, hy * 0.6f, dead * 25f + cheek * 12f);
        }

        // ------------------------------------------------------------------ capa 3: brazos y arma (IK)
        private void UpdateArms(RigInputs I, float run, float jog, float walk, float moving, float dead, float dt)
        {
            float phase = _gait.Phase * Mathf.PI * 2f;
            float rw = _rightW.Step(I.Dead ? 0f : I.RightWeight, Smoothing.weights, dt);
            float lw = _leftW.Step(I.Dead ? 0f : I.LeftWeight, Smoothing.weights, dt);
            // espacio de las manos: el del arma (sus objetivos se quedan quietos ahí aunque el arma se mueva con el cuerpo). Si cambia, se re-expresa sin salto.
            if (!_handSpaceSet || I.HandSpace != _handSpace)
            {
                Vector3 w0 = _handSpaceSet ? FromHandSpace(_hand[0].Value.ToUnity()) : _hand[0].Value.ToUnity(), w1 = _handSpaceSet ? FromHandSpace(_hand[1].Value.ToUnity()) : _hand[1].Value.ToUnity();
                Quaternion r0 = _handSpaceSet ? HandSpaceRot() * _handRot[0] : Bone(BoneId.LeftHand).rotation, r1 = _handSpaceSet ? HandSpaceRot() * _handRot[1] : Bone(BoneId.RightHand).rotation;
                _handSpace = I.HandSpace; _handSpaceSet = true;
                _hand[0].Reset(ToV3(ToHandSpace(w0))); _hand[1].Reset(ToV3(ToHandSpace(w1)));
                _handGoal[0] = ToHandSpace(w0); _handGoal[1] = ToHandSpace(w1);
                _handRot[0] = Quaternion.Inverse(HandSpaceRot()) * r0; _handRot[1] = Quaternion.Inverse(HandSpaceRot()) * r1;
            }
            // armas pesadas: los hombros bajan un poco
            float drop = _shoulderDrop.Step(I.Dead ? 0f : I.WeaponWeight * Mathf.Max(rw, lw), 0.2f, dt);
            var rs = Spec[(int)BoneId.RightShoulder].LocalPosition; var ls = Spec[(int)BoneId.LeftShoulder].LocalPosition;
            Bone(BoneId.RightShoulder).localPosition = new Vector3(rs.X, rs.Y - 0.022f * drop, rs.Z);
            Bone(BoneId.LeftShoulder).localPosition = new Vector3(ls.X, ls.Y - 0.012f * drop, ls.Z);
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
                // brazo libre en oposición a su pierna; corriendo, codo a ~90°
                float bend = Mathf.Lerp(0.95f, ArmSwingMath.RunningReach, run + jog * 0.4f);
                float swing = ArmSwingMath.Offset(_gait.Phase, left) * (0.12f * walk + 0.2f * jog + 0.3f * run) * I.ArmSwing;
                float idleSway = Mathf.Sin(_time * 0.9f + side) * 0.015f;
                Vector3 free = shoulder - up * armLen * bend + chestFwd * (swing + 0.04f + run * 0.12f) + chestRight * side * (0.05f + idleSway) + up * run * 0.12f;
                if (dead > 0.01f) free = Vector3.Lerp(free, shoulder - up * armLen * 0.98f + chestRight * side * 0.15f, dead);

                float w = left ? lw : rw;
                Vector3 goal = Vector3.Lerp(free, left ? I.LeftHand : I.RightHand, w);
                // el objetivo avanza como mucho a la velocidad de una mano real (al cambiar de agarre o recargar no salta) y el muelle quita los tirones
                _handGoal[a] = Vector3.MoveTowards(_handGoal[a], ToHandSpace(goal), Smoothing.handSpeed * dt);
                V3 g = _hand[a].Step(ToV3(_handGoal[a]), Smoothing.hands, dt);
                Vector3 target = FromHandSpace(g.ToUnity());
                HandTargets[a] = target;
                // alcance: si la mano no llega, el hombro se adelanta hacia ella (protracción de la clavícula, hasta 8 cm), como un brazo real que estira
                float deficit = Vector3.Distance(shoulder, target) - armLen * 0.92f;   // empieza antes del tope: el codo no queda bloqueado
                if (deficit > 0f)
                {
                    var clavicle = Bone(left ? BoneId.LeftShoulder : BoneId.RightShoulder);
                    clavicle.position += (target - shoulder).normalized * Mathf.Min(deficit, 0.12f) * Mathf.Max(w, 0f);
                    shoulder = upper.position;
                }

                // codos: con el arma en las manos, hacia abajo y un poco hacia dentro (nunca hacia fuera); brazo libre, hacia atrás
                Vector3 holdPole = shoulder + Vector3.down * 0.5f - chestRight * side * 0.15f - chestFwd * 0.1f;   // codos abajo (mundo) y algo hacia dentro
                Vector3 freePole = shoulder - chestFwd * 0.5f - up * 0.3f + chestRight * side * 0.05f;
                Vector3 pole = Vector3.Lerp(freePole, holdPole, left ? lw : rw);
                TwoBoneIK.Solve(ToV3(shoulder), ToV3(target), ToV3(pole), _upperArm, _lowerArm, out V3 mid, out V3 end);
                Vector3 elbow = mid.ToUnity(), wrist = end.ToUnity();
                ElbowFlexion[a] = LegSolver.KneeFlexion(ToV3(shoulder), mid, end);
                Vector3 bendDir = Vector3.ProjectOnPlane(elbow - (shoulder + wrist) * 0.5f, (wrist - shoulder).normalized);
                if (bendDir.sqrMagnitude < 1e-6f) bendDir = -chestFwd;
                upper.rotation = SegmentRotation(shoulder, elbow, bendDir);
                lower.rotation = SegmentRotation(elbow, wrist, bendDir);
                Quaternion freeRot = Quaternion.LookRotation(chestFwd, elbow - wrist);
                // el giro de la mano también se suaviza (en el espacio del arma): al cambiar de agarre no salta
                Quaternion goalRot = Quaternion.Inverse(HandSpaceRot()) * Quaternion.Slerp(freeRot, left ? I.LeftHandRot : I.RightHandRot, w);
                Quaternion smoothRot = Quaternion.Slerp(_handRot[a], goalRot, 1f - Mathf.Exp(-dt / Mathf.Max(0.005f, Smoothing.hands)));
                _handRot[a] = Quaternion.RotateTowards(_handRot[a], smoothRot, Smoothing.handTurn * dt);
                hand.rotation = HandSpaceRot() * _handRot[a];
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

        /// <summary>
        /// Rotación de un hueso de extremidad que va de "from" a "to" (su -Y local apunta al hijo) con su +Z hacia el lado al que se dobla la articulación.
        /// El EJE del hueso manda: Quaternion.LookRotation conserva exacto el primer vector y proyecta el segundo, así que se pasa primero la dirección de doblado
        /// ya perpendicular al hueso. (Antes se pasaba al revés: el eje se proyectaba sobre la línea cadera-tobillo, la pierna quedaba recta y el pie se hundía.)
        /// </summary>
        private static Quaternion SegmentRotation(Vector3 from, Vector3 to, Vector3 bendDir)
        {
            Vector3 up = from - to;
            Vector3 fwd = Vector3.ProjectOnPlane(bendDir, up.normalized);
            if (fwd.sqrMagnitude < 1e-8f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up.normalized);
            return Quaternion.LookRotation(fwd.normalized, up);
        }

        /// <summary>Cuánto gira la línea cadera-cuello por cada grado de "pitch" de columna repartido spine/chest/upperChest (ángulo medio pesado por largo).</summary>
        private float SpineLineFactor(float spineShare, float upperShare)
        {
            float chestShare = 1f - spineShare - upperShare;
            float a = Spec[(int)BoneId.Spine].LocalPosition.Y, b = Spec[(int)BoneId.Chest].LocalPosition.Y, c = Spec[(int)BoneId.UpperChest].LocalPosition.Y, d = Spec[(int)BoneId.Neck].LocalPosition.Y;
            float total = a + b + c + d;
            return total > 1e-4f ? Mathf.Max(0.3f, (b * spineShare + c * (spineShare + chestShare) + d) / total) : 1f;
        }

        private Vector3 ToHandSpace(Vector3 world) => _handSpace != null ? _handSpace.InverseTransformPoint(world) : transform.InverseTransformPoint(world);
        private Vector3 FromHandSpace(Vector3 local) => _handSpace != null ? _handSpace.TransformPoint(local) : transform.TransformPoint(local);
        private Quaternion HandSpaceRot() => _handSpace != null ? _handSpace.rotation : transform.rotation;

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

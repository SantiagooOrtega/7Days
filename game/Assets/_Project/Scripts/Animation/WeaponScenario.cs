using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Animation
{
    public enum GripStep { Hip, Aim, Fire, Reload, LowReady, Cover, Draw, Melee, HeavyMelee }

    /// <summary>
    /// Soldado con un arma real (GunParts) manejado sin el jugador, con EL MISMO código de agarre que el juego (WeaponGripSolver): sacar, cadera, apuntar,
    /// disparar, recargar, bajar el arma (correr), cobertura y golpes cuerpo a cuerpo. Avanza con pasos fijos. Lo usan las pruebas visuales y el ciclo F7.
    /// </summary>
    public class WeaponScenario
    {
        public ProceduralRig Rig { get; private set; }
        public Transform Holder { get; private set; }
        public Transform Pose { get; private set; }
        public GunParts Parts { get; private set; }
        public WeaponData Data { get; private set; }
        public ReloadState Reload { get; } = new ReloadState();
        public GripStep Step { get; private set; } = GripStep.Hip;
        public float StepTime { get; private set; }
        public GripState State => _state;

        private CriticalSpring _aim, _sprint, _cover, _reloadW;
        private readonly GripInertia _inertia = new GripInertia();
        private CriticalSpring3 _meleeRot;
        private float _draw = 1f, _kick, _lastShot = -10f, _actionStart = -10f, _time, _swingStart = -10f;
        private bool _heavy;
        private GripState _state;

        public static WeaponScenario Create(WeaponData data, Vector3 position, Transform parent = null)
        {
            var sc = new WeaponScenario();
            sc.Rig = RigScenario.CreateSoldier(position, parent);
            sc.Rig.ManualTick = true;   // lo avanza Tick (en Play, el LateUpdate del rig no lo avanza otra vez)
            // soporte del arma como en PlayerFactory (hijo de la raíz del jugador a la altura del pecho)
            sc.Holder = new GameObject("WeaponHolder").transform;
            sc.Holder.SetParent(sc.Rig.transform, false);
            sc.Holder.localPosition = new Vector3(0.3f, 1.2f, 0.25f);
            sc.Pose = new GameObject("WeaponPose").transform;
            sc.Pose.SetParent(sc.Holder, false);
            sc.Equip(data, false);
            sc.Rig.BeforeArms += sc.OnBeforeArms;
            return sc;
        }

        /// <summary>Cambia de arma (con "draw" la saca desde la cadera como en el juego).</summary>
        public void Equip(WeaponData data, bool draw = true)
        {
            if (Parts != null) Object.DestroyImmediate(Parts.gameObject);
            Data = data;
            Parts = GunParts.Build(data, Pose);
            Reload.Cancel();
            _draw = draw ? 0f : 1f;
        }

        public void SetStep(GripStep step)
        {
            Step = step; StepTime = 0f;
            if (step == GripStep.Reload && !Data.IsMelee) Reload.Begin(Data, Data.magazineSize, 999);
            if (step == GripStep.Draw) _draw = 0f;
            if (step == GripStep.Melee || step == GripStep.HeavyMelee) { _swingStart = _time; _heavy = step == GripStep.HeavyMelee; }
        }

        /// <summary>Avanza dt segundos (el cuerpo corre en LowReady; el resto, quieto).</summary>
        public void Tick(float dt)
        {
            _time += dt; StepTime += dt;
            bool aiming = Step == GripStep.Aim || Step == GripStep.Fire;
            if (Step == GripStep.Fire && !Data.IsMelee && _time - _lastShot > 60f / Mathf.Max(60f, Data.roundsPerMinute) + 0.15f)
            {
                _lastShot = _time; _kick = Mathf.Min(1.5f, _kick + 1f);
                if (Data.kind == WeaponKind.Shotgun || Data.kind == WeaponKind.SniperRifle) _actionStart = _time + 0.12f;
            }
            Reload.Tick(dt, out _);
            _draw = Mathf.Min(1f, _draw + dt / DrawState.DrawSeconds);
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 9f);

            var mode = Step == GripStep.LowReady ? RigScenarioMode.Run : RigScenarioMode.Idle;
            if (Step == GripStep.Cover) mode = RigScenarioMode.HalfKneel;
            Rig.transform.position += Rig.transform.forward * RigScenario.Speed(mode) * dt;
            RigScenario.Drive(Rig, mode);   // piernas y torso; las manos las pone OnBeforeArms

            float action = (_time - _actionStart) / (Data.kind == WeaponKind.SniperRifle ? 0.45f : 0.3f);
            float meleeT = Data.IsMelee && _time - _swingStart <= MeleeSwing.Total(_heavy) ? _time - _swingStart : -1f;
            _state = new GripState
            {
                Aim = _aim.Step(aiming ? 1f : 0f, 0.1f, dt), Sprint = _sprint.Step(Step == GripStep.LowReady ? 1f : 0f, 0.12f, dt),
                Cover = _cover.Step(Step == GripStep.Cover ? 1f : 0f, 0.15f, dt), Draw = CameraMath.EaseInOut(_draw), Reload = _reloadW.Step(Reload.Active ? 1f : 0f, 0.12f, dt),
                Aiming = aiming, InCover = Step == GripStep.Cover, Sprinting = Step == GripStep.LowReady, ReloadInfo = Reload,
                ActionCycle = action >= 0f && action <= 1f ? Mathf.Sin(action * Mathf.PI) : 0f, SinceShot = _time - _lastShot,
                MeleeT = meleeT, MeleeHeavy = _heavy, TriggerOn = _time - _lastShot < 0.25f,
            };
            V3 m = meleeT >= 0f ? MeleeSwing.Pose(meleeT, _heavy, false) : new V3(0, 0, 0);
            _meleeRot.Step(m, 0.025f, dt);
            WeaponGripSolver.AnimateParts(Parts, Data, Reload, _state.ActionCycle, _time - _lastShot);
            Rig.Tick(dt);
        }

        private void OnBeforeArms(ProceduralRig rig, float dt)
        {
            float k = CameraMath.EaseInOut(Mathf.Clamp01(_kick));
            float kickMul = Mathf.Lerp(1f, Data.recoilAimMultiplier, _state.Aim);
            WeaponGripSolver.ApplyPose(Pose, Data, Rig, _state, new PoseExtras
            {
                KickPos = new Vector3(0f, 0f, -Data.kickback * k * kickMul), KickPitch = -Data.kickbackPitch * k * kickMul,
                ReloadRot = WeaponGripSolver.ReloadTilt * _state.Reload, MeleeRot = _meleeRot.Value,
            }, _inertia, dt);
            var i = Rig.Inputs;
            WeaponGripSolver.Hands(Data, Pose, Parts, Rig, _state, ref i, out HandPose rp, out HandPose lp);
            Rig.Inputs = i;
            if (Rig.RightHandMesh != null) Rig.RightHandMesh.Blend(rp, dt);
            if (Rig.LeftHandMesh != null) Rig.LeftHandMesh.Blend(lp, dt);
        }

        public void Destroy()
        {
            if (Rig != null) { Rig.BeforeArms -= OnBeforeArms; Object.DestroyImmediate(Rig.gameObject); }
        }
    }
}

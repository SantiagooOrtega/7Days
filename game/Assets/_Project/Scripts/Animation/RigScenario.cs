using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    public enum RigScenarioMode { Idle, Walk, Jog, Run, RunWeapon, HalfKneel, Squat }

    /// <summary>
    /// Un soldado de prueba conducido sin el jugador: crea el rig con sus mallas y lo hace caminar, trotar, correr (con o sin arma), arrodillarse o agacharse
    /// avanzando la animación con pasos fijos de 1/60 s. Lo usan las pruebas visuales (EditMode, sin Play) y el ciclo automático F7.
    /// </summary>
    public static class RigScenario
    {
        public const float Dt = 1f / 60f;

        public static ProceduralRig CreateSoldier(Vector3 position, Transform parent = null)
        {
            var go = new GameObject("Rig de prueba");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var rig = go.AddComponent<ProceduralRig>();
            rig.Build(SevenDays.Animation.ProportionSettings.For(CharacterVariantLibrary.Current));
            RigVisuals.BuildSoldier(rig, RigPalette.Soldier);
            return rig;
        }

        /// <summary>Velocidad del modo (m/s): caminar 1.6, trotar 4.2 (el paso normal del jugador), correr 6.8 (sprint).</summary>
        public static float Speed(RigScenarioMode mode)
        {
            switch (mode)
            {
                case RigScenarioMode.Walk: return LocomotionBlend.SpeedKeys[1];
                case RigScenarioMode.Jog: return LocomotionBlend.SpeedKeys[2];
                case RigScenarioMode.Run: case RigScenarioMode.RunWeapon: return LocomotionBlend.SpeedKeys[3];
                default: return 0f;
            }
        }

        /// <summary>Rellena las entradas del rig para el modo (mira al frente; con arma, las manos en "low ready" delante del pecho).</summary>
        public static void Drive(ProceduralRig rig, RigScenarioMode mode)
        {
            var t = rig.transform;
            float speed = Speed(mode);
            bool weapon = mode == RigScenarioMode.RunWeapon;
            rig.Inputs = new RigInputs
            {
                MoveY = speed / LocomotionBlend.SpeedKeys[3], Grounded = true,
                Sprinting = mode == RigScenarioMode.Run || mode == RigScenarioMode.RunWeapon,
                Crouch = mode == RigScenarioMode.Squat ? 1f : 0f,
                Kneel = mode == RigScenarioMode.HalfKneel ? 1f : 0f, KneelSide = 1,
                InCover = mode == RigScenarioMode.HalfKneel,
                LookDirection = t.forward, DesiredForward = t.forward, ArmSwing = 1f,
                RightHand = t.TransformPoint(0.17f, 1.08f, 0.32f), LeftHand = t.TransformPoint(-0.03f, 1.16f, 0.58f),
                RightHandRot = t.rotation, LeftHandRot = t.rotation,
                RightWeight = weapon ? 1f : 0f, LeftWeight = weapon ? 1f : 0f,
            };
        }

        /// <summary>Avanza "seconds" segundos: el cuerpo se mueve hacia delante a la velocidad del modo y el rig se actualiza cada 1/60 s.</summary>
        public static void Simulate(ProceduralRig rig, RigScenarioMode mode, float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) Step(rig, mode);
        }

        public static void Step(ProceduralRig rig, RigScenarioMode mode)
        {
            rig.transform.position += rig.transform.forward * Speed(mode) * Dt;
            Drive(rig, mode);
            rig.Tick(Dt);
        }

        /// <summary>
        /// Avanza (tras 2 s para asentar los muelles) hasta que el pie "foot" llegue a la fase pedida de su zancada (0 = contacto, ~0.2 apoyo medio, apoyo..1 vuelo).
        /// Devuelve false si no la alcanzó (quieto, o fase fuera del rango recorrido).
        /// </summary>
        public static bool SimulateToFootPhase(ProceduralRig rig, RigScenarioMode mode, int foot, float phase)
        {
            Simulate(rig, mode, 2f);
            for (int i = 0; i < 600; i++)
            {
                float before = rig.FootPhase(foot);
                Step(rig, mode);
                float after = rig.FootPhase(foot);
                bool crossed = after >= before ? (before < phase && after >= phase) : (before < phase || after >= phase);
                if (crossed) return true;
            }
            return false;
        }
    }
}

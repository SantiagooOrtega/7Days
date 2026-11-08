using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Muelle con amortiguación crítica (solución exacta, estable con cualquier dt): llega al objetivo lo más rápido posible SIN rebotar. Es la base de la fluidez
    /// de toda la animación procedural: ninguna pose cambia de golpe. "smoothTime" es el tiempo característico (como en SmoothDamp).
    /// </summary>
    public struct CriticalSpring
    {
        public float Value, Velocity;

        public CriticalSpring(float value) { Value = value; Velocity = 0f; }

        public float Step(float target, float smoothTime, float dt)
        {
            if (dt <= 0f) return Value;
            if (smoothTime <= 1e-4f) { Velocity = 0f; Value = target; return Value; }
            float omega = 2f / smoothTime;
            float e = (float)Math.Exp(-omega * dt);
            float j0 = Value - target;
            float j1 = Velocity + j0 * omega;
            Value = e * (j0 + j1 * dt) + target;
            Velocity = e * (Velocity - j1 * omega * dt);
            return Value;
        }

        /// <summary>Igual pero para ángulos en grados (toma el camino corto).</summary>
        public float StepAngle(float target, float smoothTime, float dt)
        {
            float d = CameraMath.DeltaAngle(Value, target);
            return Step(Value + d, smoothTime, dt);
        }

        /// <summary>Un golpe: suma velocidad (reacciones a impactos, aterrizajes).</summary>
        public void Kick(float velocity) => Velocity += velocity;
    }

    /// <summary>Tres muelles críticos juntos (posiciones o pequeños ángulos).</summary>
    public struct CriticalSpring3
    {
        public CriticalSpring X, Y, Z;
        public V3 Value => new V3(X.Value, Y.Value, Z.Value);

        public void Reset(V3 v) { X = new CriticalSpring(v.X); Y = new CriticalSpring(v.Y); Z = new CriticalSpring(v.Z); }

        public V3 Step(V3 target, float smoothTime, float dt) => new V3(X.Step(target.X, smoothTime, dt), Y.Step(target.Y, smoothTime, dt), Z.Step(target.Z, smoothTime, dt));
    }

    /// <summary>
    /// IK de dos huesos (muslo-pierna, brazo-antebrazo) por la ley de los cosenos. La articulación media se dobla hacia el "pole" (rodilla hacia delante, codo hacia
    /// atrás/fuera). Si el objetivo está fuera de alcance, el extremo queda en la recta hacia él a la longitud máxima (nunca se estira ni se rompe la articulación).
    /// </summary>
    public static class TwoBoneIK
    {
        /// <returns>fracción del alcance usada (1 = brazo/pierna estirada del todo)</returns>
        public static float Solve(V3 root, V3 target, V3 pole, float upper, float lower, out V3 mid, out V3 end)
        {
            V3 toTarget = target - root;
            float dist = toTarget.Length;
            float maxReach = (upper + lower) * 0.999f;
            float minReach = Math.Abs(upper - lower) + 1e-3f;
            V3 dir = dist > 1e-6f ? toTarget * (1f / dist) : new V3(0, -1, 0);
            float d = Math.Max(minReach, Math.Min(maxReach, dist));
            end = root + dir * d;

            // dirección de doblado: componente del pole perpendicular a la recta raíz-objetivo
            V3 toPole = pole - root;
            V3 bend = toPole - dir * V3.Dot(toPole, dir);
            if (bend.Length < 1e-5f) bend = AnyPerpendicular(dir);
            bend = bend.Normalized;

            // ley de los cosenos: distancia a lo largo de la recta y altura de la articulación media
            float a = (upper * upper - lower * lower + d * d) / (2f * d);
            float h = (float)Math.Sqrt(Math.Max(0f, upper * upper - a * a));
            mid = root + dir * a + bend * h;
            return d / (upper + lower);
        }

        private static V3 AnyPerpendicular(V3 d)
        {
            V3 c = Math.Abs(d.Y) < 0.9f ? V3.Cross(d, new V3(0, 1, 0)) : V3.Cross(d, new V3(1, 0, 0));
            return c.Normalized;
        }
    }

    /// <summary>
    /// Ciclo de zancada ligado a la DISTANCIA recorrida (no al tiempo): así los pies no patinan aunque cambie la velocidad. Un ciclo = dos pasos (izquierdo en 0,
    /// derecho en 0.5). La zancada se alarga con la velocidad; el tiempo de apoyo baja al correr (más tiempo en el aire).
    /// </summary>
    public class GaitCycle
    {
        public float Phase { get; private set; }   // 0..1
        public int LastFootDown { get; private set; } = -1;   // 0 izquierdo, 1 derecho

        /// <summary>Metros de un ciclo completo (dos pasos) a esa velocidad. "shrink" acorta la zancada (fatiga: pasos más cortos y rápidos).</summary>
        public static float CycleLength(float speed, float scale = 1f, float shrink = 0f)
            => Math.Max(0.6f, Math.Min(3.4f, (0.95f + 0.3f * Math.Max(0f, speed)) * scale * (1f - Math.Max(0f, Math.Min(0.3f, shrink)))));

        /// <summary>Fracción del ciclo que cada pie está apoyado: 0.62 caminando, 0.4 corriendo.</summary>
        public static float StanceFraction(float speed) => 0.62f - 0.22f * CameraMath.Clamp01((speed - 2f) / 4.8f);

        /// <summary>Altura máxima del pie en el aire (m).</summary>
        public static float LiftHeight(float speed) => 0.06f + 0.035f * Math.Max(0f, Math.Min(7f, speed));

        /// <summary>Avanza con la distancia recorrida en este frame. Devuelve el pie que se apoyó (0/1) o -1.</summary>
        public int Advance(float distance, float cycleLength)
        {
            if (distance <= 0f || cycleLength <= 0f) return -1;
            float before = Phase;
            Phase += distance / cycleLength;
            int down = -1;
            if (before < 0.5f && Phase >= 0.5f) down = 1;
            if (Phase >= 1f) { Phase -= (float)Math.Floor(Phase); down = 0; }
            if (down >= 0) LastFootDown = down;
            return down;
        }

        /// <summary>Fase propia de un pie (el derecho va medio ciclo por detrás): 0 = acaba de apoyarse.</summary>
        public float FootPhase(int foot) => foot == 0 ? Phase : Frac(Phase + 0.5f);

        public static bool InStance(float footPhase, float stance) => footPhase < stance;

        /// <summary>0..1 dentro del vuelo del pie (0 despega, 1 aterriza).</summary>
        public static float SwingProgress(float footPhase, float stance) => footPhase < stance ? 0f : (footPhase - stance) / Math.Max(1e-3f, 1f - stance);

        /// <summary>Altura del pie en el vuelo: arco suave que empieza y termina en 0.</summary>
        public static float SwingHeight(float swing, float lift) => (float)Math.Sin(Math.PI * CameraMath.Clamp01(swing)) * lift;

        public void Reset(float phase = 0f) { Phase = Frac(phase); LastFootDown = -1; }

        private static float Frac(float v) => v - (float)Math.Floor(v);
    }

    public static class FootPlacement
    {
        /// <summary>
        /// Dónde aterrizará el pie que está en el aire: donde estará la cadera a mitad del próximo apoyo (así el pie queda bajo el cuerpo cuando carga peso), más el
        /// desplazamiento lateral de su lado.
        /// </summary>
        public static V3 PredictPlant(V3 hip, V3 velocity, float timeToPlant, float stanceDuration, V3 lateralOffset)
            => hip + velocity * (timeToPlant + stanceDuration * 0.5f) + lateralOffset;

        /// <summary>Posición del pie en el vuelo: de donde despegó a donde aterrizará, con un arco.</summary>
        public static V3 Swing(V3 from, V3 to, float progress, float lift)
        {
            float t = CameraMath.EaseInOut(progress);
            V3 p = from + (to - from) * t;
            return p + new V3(0f, GaitCycle.SwingHeight(progress, lift), 0f);
        }
    }

    /// <summary>
    /// Mezcla de locomoción con pesos continuos (blend tree propio): idle, caminar, trotar y correr según la velocidad (suman 1, sin saltos), y 8 direcciones según
    /// (MoveX, MoveY) interpolando entre las dos direcciones vecinas.
    /// </summary>
    public static class LocomotionBlend
    {
        public static readonly float[] SpeedKeys = { 0f, 1.6f, 4.2f, 6.8f };   // idle, caminar, trotar (= caminar rápido del jugador), correr
        public const int Idle = 0, Walk = 1, Jog = 2, Run = 3;

        public static void SpeedWeights(float speed, float[] weights)
        {
            for (int i = 0; i < 4; i++) weights[i] = 0f;
            if (speed <= SpeedKeys[0]) { weights[0] = 1f; return; }
            for (int i = 0; i < 3; i++)
            {
                if (speed <= SpeedKeys[i + 1])
                {
                    float t = CameraMath.EaseInOut((speed - SpeedKeys[i]) / (SpeedKeys[i + 1] - SpeedKeys[i]));
                    weights[i] = 1f - t; weights[i + 1] = t;
                    return;
                }
            }
            weights[3] = 1f;
        }

        /// <summary>Pesos de 8 direcciones (0 = delante, en sentido horario de 45 en 45 grados). Sin movimiento: todo 0.</summary>
        public static void DirectionWeights(float moveX, float moveY, float[] weights)
        {
            for (int i = 0; i < 8; i++) weights[i] = 0f;
            float mag = (float)Math.Sqrt(moveX * moveX + moveY * moveY);
            if (mag < 1e-4f) return;
            float ang = (float)(Math.Atan2(moveX, moveY) * 180.0 / Math.PI);   // 0 delante, 90 derecha
            if (ang < 0f) ang += 360f;
            float f = ang / 45f;
            int a = (int)Math.Floor(f) % 8, b = (a + 1) % 8;
            float t = f - (float)Math.Floor(f);
            weights[a] = 1f - t; weights[b] += t;
        }

        /// <summary>Valor mezclado: suma de pesos x valores por clave.</summary>
        public static float Blend(float[] weights, float[] values)
        {
            float s = 0f;
            for (int i = 0; i < weights.Length && i < values.Length; i++) s += weights[i] * values[i];
            return s;
        }
    }

    /// <summary>Proporciones de un cuerpo humanoide: con ellas el mismo rig genera soldados, zombis delgados o un tanque enorme.</summary>
    [Serializable]
    public class RigProportions
    {
        public float height = 1.8f;          // de los pies a la coronilla
        public float shoulderWidth = 0.5f;   // de hombro a hombro
        public float hipWidth = 0.28f;
        public float bulk = 1f;              // grosor de las mallas (1 normal, 1.4 corpulento)
        public float legRatio = 0.5f;        // altura de la cadera / altura total
        public float armRatio = 1f;          // multiplica la longitud de los brazos
        public float neckRatio = 1f;
        public float headSize = 1f;
        public float hunch;                  // 0..1: encorvado (corredor zombi)
        public float torsoDepth = 1f;        // grosor del torso delante-detrás (chaleco incluido)
        public float limbThickness = 1f;     // grosor de brazos y piernas

        public void Validate()
        {
            height = CameraMath.Clamp(height, 1.2f, 4.5f); shoulderWidth = CameraMath.Clamp(shoulderWidth, 0.25f, 1.4f); hipWidth = CameraMath.Clamp(hipWidth, 0.15f, 0.8f);
            bulk = CameraMath.Clamp(bulk, 0.5f, 2.5f); legRatio = CameraMath.Clamp(legRatio, 0.4f, 0.58f); armRatio = CameraMath.Clamp(armRatio, 0.7f, 1.5f);
            neckRatio = CameraMath.Clamp(neckRatio, 0.5f, 2f); headSize = CameraMath.Clamp(headSize, 0.6f, 1.6f); hunch = CameraMath.Clamp01(hunch);
            torsoDepth = CameraMath.Clamp(torsoDepth, 0.6f, 1.6f); limbThickness = CameraMath.Clamp(limbThickness, 0.6f, 1.6f);
        }

        /// <summary>
        /// Soldado atlético y fuerte (ni esbelto ni cuadrado): misma altura (cámara, mira y pies no cambian). Respecto a la versión esbelta: torso +13 % de ancho y
        /// de fondo (bulk), hombros +9 %, cadera +5 %, brazos y piernas +11 % y cuello +13 %.
        /// </summary>
        public static RigProportions Soldier() => new RigProportions { height = 1.82f, shoulderWidth = SoldierShoulders, hipWidth = SoldierHips, bulk = SoldierBulk, torsoDepth = 0.9f, limbThickness = SoldierLimbs };

        public const float SoldierShoulders = 0.48f, SoldierHips = 0.285f, SoldierBulk = 1.13f, SoldierLimbs = 0.905f;
        /// <summary>Valores de la versión esbelta (los que guardan los assets anteriores a la versión 2 de ProportionSettings).</summary>
        public const float SlimShoulders = 0.44f, SlimHips = 0.27f, SlimBulk = 1f, SlimLimbs = 0.92f;

        /// <summary>Copia con el grosor de la variante aplicado (la Calavera es algo más corpulenta; el Explorador, algo más ligero).</summary>
        public RigProportions WithBuild(float bulkScale)
        {
            var c = (RigProportions)MemberwiseClone();
            c.bulk *= bulkScale;
            c.Validate();
            return c;
        }
    }

    public enum BoneId
    {
        Hips, Spine, Chest, UpperChest, Neck, Head,
        LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand,
        RightShoulder, RightUpperArm, RightLowerArm, RightHand,
        LeftUpperLeg, LeftLowerLeg, LeftFoot, LeftToes,
        RightUpperLeg, RightLowerLeg, RightFoot, RightToes,
    }

    public struct BoneDef
    {
        public BoneId Id;
        public int Parent;           // índice del padre en la lista; -1 = raíz
        public V3 LocalPosition;     // en la pose de reposo (A-pose: brazos hacia abajo)
        public float Length;         // hasta su hijo principal (0 en las puntas)
    }

    /// <summary>
    /// Esqueleto humanoide propio (nombres estilo Humanoid de Unity: Hips, Spine, Chest...). Pose de reposo con brazos y piernas hacia abajo; el eje de cada hueso de
    /// extremidad apunta a -Y local y el de la columna a +Y. Las longitudes salen de RigProportions.
    /// </summary>
    public static class HumanoidRigSpec
    {
        public const int BoneCount = 22;

        public static BoneDef[] Build(RigProportions p)
        {
            p = p ?? new RigProportions();
            float H = p.height;
            float hip = H * p.legRatio;                  // altura de la cadera
            float foot = H * 0.045f;                     // altura del tobillo
            float hipJoint = H * 0.02f;                  // la articulación de la cadera queda un poco por debajo del hueso Hips
            float leg = hip - foot - hipJoint;
            float thigh = leg * 0.52f, shin = leg * 0.48f;
            float head = H * 0.13f * p.headSize;
            float neck = H * 0.04f * p.neckRatio;
            float torso = H - hip - neck - head;         // de la cadera a la base del cuello
            float s1 = torso * 0.3f, s2 = torso * 0.3f, s3 = torso * 0.4f;
            float upperArm = H * 0.175f * p.armRatio, lowerArm = H * 0.15f * p.armRatio, hand = H * 0.05f;
            float halfShoulder = p.shoulderWidth * 0.5f, halfHip = p.hipWidth * 0.5f;
            float toe = H * 0.075f;

            var b = new BoneDef[BoneCount];
            void Set(BoneId id, BoneId parent, float x, float y, float z, float len) => b[(int)id] = new BoneDef { Id = id, Parent = id == BoneId.Hips ? -1 : (int)parent, LocalPosition = new V3(x, y, z), Length = len };

            Set(BoneId.Hips, BoneId.Hips, 0f, hip, 0f, s1);
            Set(BoneId.Spine, BoneId.Hips, 0f, s1 * 0.25f, 0f, s1);
            Set(BoneId.Chest, BoneId.Spine, 0f, s1, 0f, s2);
            Set(BoneId.UpperChest, BoneId.Chest, 0f, s2, 0f, s3);
            Set(BoneId.Neck, BoneId.UpperChest, 0f, s3 - s1 * 0.25f, 0f, neck);
            Set(BoneId.Head, BoneId.Neck, 0f, neck, 0f, head);
            foreach (int side in new[] { -1, 1 })
            {
                bool left = side < 0;
                Set(left ? BoneId.LeftShoulder : BoneId.RightShoulder, BoneId.UpperChest, side * halfShoulder * 0.35f, s3 * 0.85f, 0f, halfShoulder * 0.65f);
                Set(left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, left ? BoneId.LeftShoulder : BoneId.RightShoulder, side * halfShoulder * 0.65f, 0f, 0f, upperArm);
                Set(left ? BoneId.LeftLowerArm : BoneId.RightLowerArm, left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, 0f, -upperArm, 0f, lowerArm);
                Set(left ? BoneId.LeftHand : BoneId.RightHand, left ? BoneId.LeftLowerArm : BoneId.RightLowerArm, 0f, -lowerArm, 0f, hand);
                Set(left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, BoneId.Hips, side * halfHip, -hipJoint, 0f, thigh);
                Set(left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg, left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, 0f, -thigh, 0f, shin);
                Set(left ? BoneId.LeftFoot : BoneId.RightFoot, left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg, 0f, -shin, 0f, foot);
                Set(left ? BoneId.LeftToes : BoneId.RightToes, left ? BoneId.LeftFoot : BoneId.RightFoot, 0f, -foot, toe, 0f);
            }
            return b;
        }

        /// <summary>Posición de reposo de un hueso relativa a los pies (suma de los offsets de sus padres).</summary>
        public static V3 RestPosition(BoneDef[] bones, BoneId id)
        {
            V3 p = new V3(0, 0, 0);
            int i = (int)id;
            while (i >= 0) { p = p + bones[i].LocalPosition; i = bones[i].Parent; }
            return p;
        }

        public static float LegLength(BoneDef[] b) => b[(int)BoneId.LeftUpperLeg].Length + b[(int)BoneId.LeftLowerLeg].Length;
        public static float ArmLength(BoneDef[] b) => b[(int)BoneId.LeftUpperArm].Length + b[(int)BoneId.LeftLowerArm].Length;
    }
}

namespace SevenDays.Procedural
{
    /// <summary>
    /// Pose de agachado (pura): la pelvis baja y se va atrás, los pies se abren un poco y quedan bajo las rodillas, el torso se inclina. Las mismas medidas sirven
    /// quieto y caminando (así no hay saltos al pasar de uno a otro). crouch 0..1.
    /// </summary>
    public static class CrouchPose
    {
        public const float Drop = 0.36f, Back = 0.12f, FootForward = 0.07f, FootWiden = 0.3f, TorsoLean = 18f, PelvisTilt = 12f;

        /// <summary>De pie las rodillas no se bloquean: la pelvis baja unos centímetros (piernas algo flexionadas, pies siempre apoyados, nada rígido).</summary>
        public const float StandBend = 0.035f;

        public static float PelvisHeight(float hipHeight, float crouch) => hipHeight * (1f - Drop * CameraMath.Clamp01(crouch)) - StandBend;
        public static float PelvisBack(float crouch) => -Back * CameraMath.Clamp01(crouch);
        /// <summary>Separación lateral de cada pie desde el centro.</summary>
        public static float FootSide(float hipHalf, float crouch) => hipHalf * (1.05f + FootWiden * CameraMath.Clamp01(crouch));
        public static float FootAhead(float crouch) => FootForward * CameraMath.Clamp01(crouch);

        /// <summary>Polo de la rodilla: delante y un poco hacia fuera de su cadera, para que las rodillas se doblen hacia delante y nunca se crucen.</summary>
        public static V3 KneePole(V3 hip, V3 forward, V3 right, float side) => hip + forward * 1.2f + right * (side * 0.25f) + new V3(0f, -0.4f, 0f);
    }

    /// <summary>LOD de animación: cada cuántos frames se actualiza un rig según su distancia a la cámara (cerca, cada frame).</summary>
    public static class RigLod
    {
        public const float Near = 18f, Mid = 35f, Far = 60f;

        public static int Interval(float distance) => distance < Near ? 1 : (distance < Mid ? 2 : (distance < Far ? 3 : 6));
        /// <summary>A partir de esta distancia los pies no lanzan rayos al suelo (usa la altura del cuerpo): no se nota y ahorra física.</summary>
        public static bool FootRaycasts(float distance) => distance < Mid;
    }
}

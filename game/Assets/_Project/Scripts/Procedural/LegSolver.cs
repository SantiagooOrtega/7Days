using System;

namespace SevenDays.Procedural
{
    public struct LegResult
    {
        public V3 Knee, Ankle;
        public float KneeFlexion;      // grados: 0 pierna recta .. 150 máximo
        public bool AnkleLifted;       // el tobillo pedía estar bajo el suelo y se subió
        public bool FlexionLimited;    // la rodilla iba a doblarse más de lo posible
        public bool KneeLifted;        // la rodilla iba a quedar bajo el suelo
    }

    /// <summary>
    /// Pierna con reglas duras (pura): (1) el tobillo nunca queda bajo el suelo (altura del suelo + altura del tobillo + suela); (2) la rodilla se dobla entre 0 y 150°,
    /// nunca hacia atrás; (3) la rodilla nunca baja del suelo (como mucho apoyada); (4) se dobla hacia el polo (delante y algo hacia fuera), así las rodillas no se cruzan.
    /// Si la pelvis está demasiado baja para respetar todo, se usa <see cref="MinHipHeight"/> para limitar la pelvis en vez de hundir el pie.
    /// </summary>
    public static class LegSolver
    {
        public const float Sole = 0.015f, KneeRadius = 0.065f, MaxKneeFlexion = 150f, MinKneeFlexion = 2f;

        public static LegResult Solve(V3 hip, V3 ankleTarget, float groundY, float ankleHeight, V3 pole, float thigh, float shin)
        {
            var r = new LegResult();
            float minAnkleY = groundY + ankleHeight + Sole;
            if (ankleTarget.Y < minAnkleY) { ankleTarget = new V3(ankleTarget.X, minAnkleY, ankleTarget.Z); r.AnkleLifted = true; }

            // flexión máxima: la cadera y el tobillo no pueden acercarse más que esto
            float minDist = Distance(thigh, shin, MaxKneeFlexion);
            V3 toAnkle = ankleTarget - hip;
            float d = toAnkle.Length;
            if (d < minDist)
            {
                // el tobillo se aleja de la cadera EN HORIZONTAL (hacia donde ya estaba, o hacia el polo) sin bajar del suelo, hasta la flexión máxima
                float dy = hip.Y - ankleTarget.Y;
                float horiz = (float)Math.Sqrt(Math.Max(0f, minDist * minDist - dy * dy)) + 1e-3f;
                V3 flat = new V3(toAnkle.X, 0f, toAnkle.Z);
                if (flat.Length < 1e-3f) flat = new V3(pole.X - hip.X, 0f, pole.Z - hip.Z);
                if (flat.Length < 1e-3f) flat = new V3(0f, 0f, 1f);
                flat = flat.Normalized;
                ankleTarget = new V3(hip.X + flat.X * horiz, ankleTarget.Y, hip.Z + flat.Z * horiz);
                r.FlexionLimited = true;
            }

            TwoBoneIK.Solve(hip, ankleTarget, pole, thigh, shin, out V3 knee, out V3 ankle);
            // la rodilla no baja del suelo: el pie se adelanta en horizontal (hacia el polo) hasta que la rodilla quede como mucho apoyada.
            // Nunca se gira el plano de doblado: eso pondría la rodilla hacia atrás.
            if (knee.Y < groundY + KneeRadius - 0.01f)
            {
                V3 ahead = new V3(pole.X - hip.X, 0f, pole.Z - hip.Z);
                ahead = ahead.Length > 1e-3f ? ahead.Normalized : new V3(0f, 0f, 1f);
                for (int i = 0; i < 30 && (knee.Y < groundY + KneeRadius - 0.01f || (ankleTarget - hip).Length < minDist); i++)
                {
                    ankleTarget = ankleTarget + ahead * 0.04f;   // al pasar bajo la cadera también se respeta la flexión máxima
                    TwoBoneIK.Solve(hip, ankleTarget, pole, thigh, shin, out knee, out ankle);
                }
                r.KneeLifted = true;
            }
            r.Knee = knee; r.Ankle = ankle;
            r.KneeFlexion = KneeFlexion(hip, knee, ankle);
            return r;
        }

        /// <summary>Distancia cadera-tobillo con una flexión de rodilla dada (0 = recta).</summary>
        public static float Distance(float thigh, float shin, float flexionDeg)
        {
            double interior = (180.0 - flexionDeg) * Math.PI / 180.0;
            return (float)Math.Sqrt(thigh * thigh + shin * shin - 2.0 * thigh * shin * Math.Cos(interior));
        }

        /// <summary>Flexión de la rodilla (0 recta .. 180 plegada del todo).</summary>
        public static float KneeFlexion(V3 hip, V3 knee, V3 ankle)
        {
            V3 a = (hip - knee).Normalized, b = (ankle - knee).Normalized;
            float cos = Math.Max(-1f, Math.Min(1f, V3.Dot(a, b)));
            return 180f - (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Altura mínima de la articulación de la cadera para que, con el tobillo en el suelo a "horizontal" metros, la rodilla no pase de 150°. La pelvis se limita
        /// con esto: si quiere bajar más, se queda y la rodilla se dobla al máximo en vez de hundir el pie.
        /// </summary>
        public static float MinHipHeight(float groundY, float ankleHeight, float horizontal, float thigh, float shin)
        {
            float minDist = Distance(thigh, shin, MaxKneeFlexion);
            float v = minDist * minDist - horizontal * horizontal;
            return groundY + ankleHeight + Sole + (v > 0f ? (float)Math.Sqrt(v) : 0f);
        }
    }

    /// <summary>
    /// Arrodillado de cobertura baja (puro): una rodilla apoyada en el suelo con la pantorrilla plana y la otra pierna doblada ~90° con el pie delante; el torso
    /// erguido. Devuelve la altura de la cadera y los objetivos de los tobillos (en el espacio del cuerpo: x lateral, y alto, z adelante; suelo en y = 0).
    /// </summary>
    public static class KneelPose
    {
        public const float KneeAhead = 0.12f;   // la rodilla apoyada queda un poco delante de su cadera

        public static float HipHeight(float thigh) => LegSolver.KneeRadius + (float)Math.Sqrt(Math.Max(0f, thigh * thigh - KneeAhead * KneeAhead));

        /// <summary>Tobillo de la pierna apoyada: detrás de la rodilla a la longitud de la pantorrilla, casi en el suelo.</summary>
        public static V3 KneelingAnkle(float hipX, float thigh, float shin, float ankleHeight)
        {
            float dy = ankleHeight - LegSolver.KneeRadius;
            float back = (float)Math.Sqrt(Math.Max(0f, shin * shin - dy * dy));
            return new V3(hipX, ankleHeight + LegSolver.Sole, KneeAhead - back);
        }

        /// <summary>Rodilla apoyada (para el polo del IK).</summary>
        public static V3 KneelingKnee(float hipX) => new V3(hipX, LegSolver.KneeRadius, KneeAhead);

        /// <summary>Tobillo de la pierna delantera: el pie delante, bajo la rodilla doblada ~90°.</summary>
        public static V3 FrontAnkle(float hipX, float thigh, float ankleHeight) => new V3(hipX, ankleHeight + LegSolver.Sole, thigh * 0.95f);
    }

    /// <summary>Balanceo de los brazos en oposición a las piernas: cuando un pie está delante, el brazo de ese lado va atrás.</summary>
    public static class ArmSwingMath
    {
        /// <summary>Desplazamiento hacia delante (+) o atrás (-) de la mano, 0..1 de la amplitud. Fase del ciclo: el pie izquierdo se apoya (delante) en 0.</summary>
        public static float Offset(float gaitPhase01, bool left) => (left ? -1f : 1f) * (float)Math.Cos(gaitPhase01 * 2.0 * Math.PI);

        /// <summary>Cuánto adelantado está el pie (+1 delante al apoyarse, -1 atrás al despegar), para comprobar la oposición.</summary>
        public static float FootForward(float gaitPhase01, bool left) => (left ? 1f : -1f) * (float)Math.Cos(gaitPhase01 * 2.0 * Math.PI);

        /// <summary>Distancia hombro-mano que da un codo de ~90° corriendo (fracción de la longitud del brazo).</summary>
        public const float RunningReach = 0.72f;
    }
}

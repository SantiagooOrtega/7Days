using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Parámetros de la pose de locomoción (ScriptableObject LocomotionPoseSettings, editables en F6). Ángulos en grados: flexión de rodilla (0 recta), ángulo del
    /// muslo respecto a la vertical (+ adelante), inclinación del pie (+ punta abajo).
    /// </summary>
    [Serializable]
    public class LocomotionPoseData
    {
        public float contactKnee = 15f;      // contacto del talón: 10-20°
        public float midStanceKnee = 5f;     // apoyo medio: casi extendida
        public float toeOffKnee = 32f;       // despegue: empieza a doblar
        public float walkSwingKnee = 62f;    // vuelo caminando: 50-70°
        public float runSwingKnee = 112f;    // vuelo corriendo: 100-120° (talón hacia el glúteo)
        public float walkThigh = 30f;        // muslo adelante al final del vuelo caminando
        public float runThigh = 55f;         // corriendo: 50-60°
        public float backThigh = -14f;       // muslo atrás al despegar
        public float heelStrikePitch = 12f;  // contacto: punta arriba (rueda sobre el talón)
        public float toeOffPitch = 28f;      // despegue: flexión plantar (rueda sobre la punta)
        public float absorb = 0.035f;        // la pelvis baja unos cm al contactar (absorción)
        public float walkLean = 3f, runLean = 14f;          // torso hacia delante según la velocidad
        public float walkPelvisTilt = 3f, runPelvisTilt = 6f;
        public float walkHipYaw = 6f, runHipYaw = 12f;       // giro de cadera (los hombros van al contrario)
        public float swingBlend = 0.62f;     // fracción del vuelo desde la que el pie converge a su aterrizaje (sin patinar)
        public float kneelTorsoLean = 30f;   // media rodilla: torso 25-35° adelante

        public void Validate()
        {
            contactKnee = Clamp(contactKnee, 0f, 40f); midStanceKnee = Clamp(midStanceKnee, 0f, 30f); toeOffKnee = Clamp(toeOffKnee, 0f, 60f);
            walkSwingKnee = Clamp(walkSwingKnee, 20f, 150f); runSwingKnee = Clamp(runSwingKnee, 30f, 150f);
            walkThigh = Clamp(walkThigh, 5f, 80f); runThigh = Clamp(runThigh, 5f, 80f); backThigh = Clamp(backThigh, -40f, 0f);
            heelStrikePitch = Clamp(heelStrikePitch, 0f, 30f); toeOffPitch = Clamp(toeOffPitch, 0f, 45f); absorb = Clamp(absorb, 0f, 0.1f);
            walkLean = Clamp(walkLean, 0f, 30f); runLean = Clamp(runLean, 0f, 30f); walkPelvisTilt = Clamp(walkPelvisTilt, 0f, 15f); runPelvisTilt = Clamp(runPelvisTilt, 0f, 15f);
            walkHipYaw = Clamp(walkHipYaw, 0f, 25f); runHipYaw = Clamp(runHipYaw, 0f, 25f); swingBlend = Clamp(swingBlend, 0.3f, 0.9f); kneelTorsoLean = Clamp(kneelTorsoLean, 0f, 45f);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>
    /// Pose de la pierna en cada fase de la zancada (pura). Fase del pie 0..1 (0 = contacto del talón). En el apoyo la rodilla va de 15° al contactar, ~5° en el
    /// apoyo medio y empieza a doblar al despegar; en el vuelo el talón sube hacia el glúteo (60° caminando, 110° corriendo) y el muslo va adelante. El pie rueda:
    /// talón al contactar, plano en el apoyo, punta al despegar. run = peso de "correr" (0..1).
    /// </summary>
    public static class GaitPose
    {
        public static float KneeFlexion(LocomotionPoseData d, float footPhase, float stance, float run)
        {
            if (footPhase < stance)
            {
                float s = footPhase / Math.Max(1e-3f, stance);
                if (s < 0.5f) return Lerp(d.contactKnee, d.midStanceKnee, Smooth(s / 0.5f));
                return Lerp(d.midStanceKnee, d.toeOffKnee, Smooth((s - 0.5f) / 0.5f));
            }
            float u = (footPhase - stance) / Math.Max(1e-3f, 1f - stance);
            float peak = Lerp(d.walkSwingKnee, d.runSwingKnee, run);
            if (u < 0.38f) return Lerp(d.toeOffKnee, peak, Smooth(u / 0.38f));
            return Lerp(peak, d.contactKnee, Smooth((u - 0.38f) / 0.62f));
        }

        public static float ThighAngle(LocomotionPoseData d, float footPhase, float stance, float run)
        {
            float fwd = Lerp(d.walkThigh, d.runThigh, run);
            if (footPhase < stance) return Lerp(fwd * 0.6f, d.backThigh, footPhase / Math.Max(1e-3f, stance));
            float u = (footPhase - stance) / Math.Max(1e-3f, 1f - stance);
            if (u < 0.8f) return Lerp(d.backThigh, fwd, Smooth(u / 0.8f));
            return Lerp(fwd, fwd * 0.6f, Smooth((u - 0.8f) / 0.2f));   // se recoge un poco antes de contactar
        }

        /// <summary>Inclinación del pie: negativa = punta arriba (talón), positiva = punta abajo (despegue).</summary>
        public static float FootPitch(LocomotionPoseData d, float footPhase, float stance)
        {
            if (footPhase < stance)
            {
                float s = footPhase / Math.Max(1e-3f, stance);
                if (s < 0.15f) return -d.heelStrikePitch * (1f - Smooth(s / 0.15f));
                if (s < 0.65f) return 0f;
                return d.toeOffPitch * Smooth((s - 0.65f) / 0.35f);
            }
            float u = (footPhase - stance) / Math.Max(1e-3f, 1f - stance);
            if (u < 0.3f) return Lerp(d.toeOffPitch, -6f, Smooth(u / 0.3f));
            return Lerp(-6f, -d.heelStrikePitch, Smooth((u - 0.3f) / 0.7f));
        }

        /// <summary>Cuánto sube el tobillo por rodar sobre la punta (para que la punta siga en el suelo al levantar el talón).</summary>
        public static float HeelRaise(float pitchDeg, float toeLength) => pitchDeg > 0f ? toeLength * (float)Math.Sin(pitchDeg * Math.PI / 180.0) : 0f;

        /// <summary>
        /// Cinemática directa en el plano sagital: tobillo relativo a la cadera (y arriba, z adelante) con el muslo a "thighDeg" de la vertical y la rodilla doblada
        /// "kneeDeg" (la espinilla se dobla hacia atrás). Sirve para el pie en el aire: talón hacia el glúteo.
        /// </summary>
        public static V3 AnkleFromAngles(float thighDeg, float kneeDeg, float thigh, float shin)
        {
            double t = thighDeg * Math.PI / 180.0, k = (thighDeg - kneeDeg) * Math.PI / 180.0;
            float y = -(float)(Math.Cos(t) * thigh + Math.Cos(k) * shin);
            float z = (float)(Math.Sin(t) * thigh + Math.Sin(k) * shin);
            return new V3(0f, y, z);
        }

        /// <summary>
        /// Altura de la cadera sobre el tobillo para que una pierna apoyada a "horizontal" metros tenga la flexión pedida. La pelvis se pone a la menor de las de los
        /// pies apoyados: así baja al contactar (absorción) y sube en el apoyo medio, con el pie clavado en el suelo.
        /// </summary>
        public static float HipAboveAnkle(float kneeDeg, float horizontal, float thigh, float shin)
        {
            float d = LegSolver.Distance(thigh, shin, kneeDeg);
            float v = d * d - horizontal * horizontal;
            return v > 0f ? (float)Math.Sqrt(v) : 0f;
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));
        public static float Smooth(float t) => CameraMath.EaseInOut(t);
    }

    /// <summary>
    /// Media rodilla (cobertura baja): rodilla delantera a ~90° con la espinilla vertical y el pie plano; la trasera a 3-8 cm del suelo con la espinilla casi
    /// horizontal y la punta del pie apoyada; pelvis centrada entre los apoyos. Coordenadas del cuerpo (x lateral, y arriba, z adelante), suelo en y = 0.
    /// </summary>
    public static class HalfKneel
    {
        public const float RearKneeHeight = 0.055f, RearKneeBack = 0.04f, RearShinPitch = 8f;

        /// <summary>Altura de la articulación de la cadera: el muslo trasero baja casi vertical hasta la rodilla apoyada.</summary>
        public static float HipJointHeight(float thigh) => RearKneeHeight + (float)Math.Sqrt(Math.Max(0f, thigh * thigh - RearKneeBack * RearKneeBack));

        public static V3 RearKnee(float x) => new V3(x, RearKneeHeight, -RearKneeBack);

        /// <summary>Tobillo trasero: detrás de la rodilla, espinilla casi horizontal (unos grados), punta del pie en el suelo.</summary>
        public static V3 RearAnkle(float x, float shin)
        {
            double p = RearShinPitch * Math.PI / 180.0;
            return new V3(x, RearKneeHeight + (float)(Math.Sin(p) * shin), -RearKneeBack - (float)(Math.Cos(p) * shin));
        }

        /// <summary>Tobillo delantero: justo bajo la rodilla delantera (espinilla vertical), con la rodilla a la altura de la cadera delante (muslo horizontal, ~90°).</summary>
        public static V3 FrontAnkle(float x, float thigh, float shin, float ankleHeight, float hipJointY)
        {
            // rodilla delantera: a la altura del tobillo + espinilla; el muslo llega desde la cadera
            float kneeY = ankleHeight + LegSolver.Sole + shin;
            float dy = hipJointY - kneeY;
            float kneeZ = (float)Math.Sqrt(Math.Max(0f, thigh * thigh - dy * dy));
            return new V3(x, ankleHeight + LegSolver.Sole, kneeZ);
        }
    }
}

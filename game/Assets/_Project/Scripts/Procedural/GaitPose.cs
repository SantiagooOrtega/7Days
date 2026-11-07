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
        public float midStanceKnee = 5f;     // apoyo medio caminando: casi extendida
        public float runMidStanceKnee = 38f; // apoyo medio corriendo: la rodilla amortigua (da el rebote de 4-6 cm de la pelvis)
        public float standKnee = 6f;         // de pie: rodilla casi recta (la altura de la pelvis sale de aquí y de la pierna real)
        public float stanceDrop = 0.045f;    // lo más que baja la pelvis al contactar: limita lo largo del apoyo (la cadera no se hunde al correr)
        public float toeOffKnee = 32f;       // despegue: empieza a doblar
        public float walkSwingKnee = 62f;    // vuelo caminando: 50-70°
        public float runSwingKnee = 112f;    // vuelo corriendo: 100-120° (talón hacia el glúteo)
        public float walkThigh = 30f;        // muslo adelante al final del vuelo caminando
        public float runThigh = 55f;         // corriendo: 50-60°
        public float backThigh = -14f;       // muslo atrás al despegar
        public float heelStrikePitch = 12f;  // contacto: punta arriba (rueda sobre el talón)
        public float toeOffPitch = 28f;      // despegue: flexión plantar (rueda sobre la punta)
        public float absorb = 0.035f;        // la pelvis baja unos cm al contactar (absorción)
        public float walkLean = 3f, trotLean = 6.5f, runLean = 10f;   // torso hacia delante: trotar 5-8°, correr 8-12°
        public float maxRunLean = 15f;       // nunca más que esto por correr
        public float weaponLean = 4.5f;      // con arma en las manos (o low ready): 3-6°, pecho erguido
        public float pelvisLeanShare = 0.3f; // parte de la inclinación que pone la pelvis (2-4°); el resto la columna y el pecho
        /// <summary>Versión de estos valores: un asset más viejo se actualiza solo al cargarlo (ver LocomotionPoseSettings.Load).</summary>
        public int poseVersion;
        public const int CurrentPoseVersion = 2;
        public float walkHipYaw = 6f, runHipYaw = 12f;       // giro de cadera (los hombros van al contrario)
        public float swingBlend = 0.62f;     // fracción del vuelo desde la que el pie converge a su aterrizaje (sin patinar)
        public float kneelTorsoLean = 30f;   // media rodilla: torso 25-35° adelante

        public void Validate()
        {
            contactKnee = Clamp(contactKnee, 0f, 40f); midStanceKnee = Clamp(midStanceKnee, 0f, 30f); toeOffKnee = Clamp(toeOffKnee, 0f, 60f);
            walkSwingKnee = Clamp(walkSwingKnee, 20f, 150f); runSwingKnee = Clamp(runSwingKnee, 30f, 150f);
            walkThigh = Clamp(walkThigh, 5f, 80f); runThigh = Clamp(runThigh, 5f, 80f); backThigh = Clamp(backThigh, -40f, 0f);
            heelStrikePitch = Clamp(heelStrikePitch, 0f, 30f); toeOffPitch = Clamp(toeOffPitch, 0f, 45f); absorb = Clamp(absorb, 0f, 0.1f);
            maxRunLean = Clamp(maxRunLean, 0f, 15f);
            walkLean = Clamp(walkLean, 0f, maxRunLean); trotLean = Clamp(trotLean, 0f, maxRunLean); runLean = Clamp(runLean, 0f, maxRunLean); weaponLean = Clamp(weaponLean, 0f, maxRunLean);
            pelvisLeanShare = Clamp(pelvisLeanShare, 0f, 0.5f);
            runMidStanceKnee = Clamp(runMidStanceKnee, 0f, 60f); standKnee = Clamp(standKnee, 0f, 20f); stanceDrop = Clamp(stanceDrop, 0.01f, 0.12f);
            walkHipYaw = Clamp(walkHipYaw, 0f, 25f); runHipYaw = Clamp(runHipYaw, 0f, 25f); swingBlend = Clamp(swingBlend, 0.3f, 0.9f); kneelTorsoLean = Clamp(kneelTorsoLean, 0f, 45f);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>Pone al día un asset viejo: los valores que cambiaron de significado o de rango vuelven al nuevo por defecto (el resto se respeta).</summary>
        public bool Migrate()
        {
            if (poseVersion >= CurrentPoseVersion) return false;
            var d = new LocomotionPoseData();
            if (poseVersion < 2) { runLean = d.runLean; walkLean = d.walkLean; }   // v2: correr 8-12° (antes 14° más la pelvis)
            poseVersion = CurrentPoseVersion;
            return true;
        }
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
                float mid = Lerp(d.midStanceKnee, d.runMidStanceKnee, run);
                if (s < 0.5f) return Lerp(d.contactKnee, mid, Smooth(s / 0.5f));
                return Lerp(mid, d.toeOffKnee, Smooth((s - 0.5f) / 0.5f));
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

        /// <summary>
        /// Fracción del ciclo que un pie puede estar apoyado sin que la pelvis baje más de "stanceDrop": el pie se apoya bajo la cadera a mitad del apoyo, así que
        /// recorre ±alcance, con alcance = lo que llega la pierna (rodilla de contacto) desde la cadera de pie menos esa caída, más la palanca de la bola del pie.
        /// Corriendo sale ~0.3 (hay vuelo y la cadera no se hunde); caminando sigue > 0.5 (doble apoyo).
        /// </summary>
        public static float ReachStance(float baseStance, float cycleLength, float standHipJoint, float ankleAtContact, float ballLever, LocomotionPoseData d, float thigh, float shin)
        {
            // al contactar la pelvis aún viene alta (del vuelo): la rodilla de contacto se consigue con poca caída; la caída entera llega en el apoyo medio
            float v = standHipJoint - FlightDip(d) - ankleAtContact;
            float dist = LegSolver.Distance(thigh, shin, d.contactKnee);
            float h2 = dist * dist - v * v;
            float travel = 2f * (h2 > 0f ? (float)Math.Sqrt(h2) : 0f) + ballLever;
            float s = cycleLength > 1e-3f ? travel / cycleLength : baseStance;
            // caminando siempre hay doble apoyo (> 50 %); trotando y corriendo, vuelo
            float floor = baseStance >= 0.58f ? 0.53f : 0.17f;
            return Math.Max(Math.Min(floor, baseStance), Math.Min(baseStance, s));
        }

        /// <summary>Cuánto baja la cadera al final del vuelo (al contactar) respecto a lo más alto: el vuelo es una parábola, no una meseta.</summary>
        public static float FlightDip(LocomotionPoseData d) => d.stanceDrop * 0.6f;

        /// <summary>
        /// Altura de la cadera en el vuelo (los dos pies en el aire): sube desde el despegue hasta la altura de pie y baja hacia el contacto. Devuelve la caída
        /// respecto a "de pie" (0..FlightDip). gaitPhase = fase del ciclo (0..1); stance = fracción de apoyo de cada pie. -1 si no hay vuelo en esa fase.
        /// </summary>
        public static float FlightDrop(LocomotionPoseData d, float gaitPhase, float stance)
        {
            float half = gaitPhase % 0.5f;
            if (stance >= 0.5f || half < stance) return -1f;
            float u = (half - stance) / (0.5f - stance);
            return FlightDip(d) * (1f - (float)Math.Sin(Math.PI * u));
        }

        /// <summary>Altura de la articulación de la cadera de pie (sobre el suelo) con la rodilla a "kneeDeg" y el tobillo a "ankleHeight" (suela incluida).</summary>
        public static float StandHipJoint(float ankleHeight, float thigh, float shin, float kneeDeg) => ankleHeight + LegSolver.Distance(thigh, shin, kneeDeg);

        /// <summary>
        /// Inclinación del torso al moverse (grados), por pesos de la mezcla: andar, trotar, correr; con arma en las manos se acerca a "weaponLean"; nunca más de
        /// maxRunLean. Devuelve el total; la pelvis pone pelvisLeanShare (2-4°) y la columna y el pecho el resto.
        /// </summary>
        public static float MoveLean(LocomotionPoseData d, float walk, float jog, float run, float armed)
        {
            float lean = walk * d.walkLean + jog * d.trotLean + run * d.runLean;
            float moving = walk + jog + run;
            lean = Lerp(lean, Math.Min(lean, d.weaponLean * moving), armed);
            return Math.Min(lean, d.maxRunLean);
        }

        public static float PelvisLean(LocomotionPoseData d, float totalLean) => Math.Min(4f, totalLean * d.pelvisLeanShare);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));
        public static float Smooth(float t) => CameraMath.EaseInOut(t);
    }

    /// <summary>
    /// Media rodilla (cobertura baja): rodilla delantera a ~90° con la espinilla vertical y el pie plano; la trasera a 3-8 cm del suelo con la espinilla casi
    /// horizontal y la punta del pie apoyada; pelvis centrada entre los apoyos. Coordenadas del cuerpo (x lateral, y arriba, z adelante), suelo en y = 0.
    /// </summary>
    public static class HalfKneel
    {
        public const float RearKneeHeight = 0.065f, RearKneeBack = 0.04f, RearShinPitch = 8f;
        /// <summary>Pie trasero: punta abajo (apoya la bola y la punta doblada), en línea con la espinilla casi horizontal.</summary>
        public const float RearFootPitch = 70f;

        /// <summary>Altura de la articulación de la cadera: el muslo trasero baja casi vertical hasta la rodilla apoyada.</summary>
        public static float HipJointHeight(float thigh) => RearKneeHeight + (float)Math.Sqrt(Math.Max(0f, thigh * thigh - RearKneeBack * RearKneeBack));

        public static V3 RearKnee(float x) => new V3(x, RearKneeHeight, -RearKneeBack);

        /// <summary>Tobillo trasero: detrás de la rodilla, espinilla casi horizontal (unos grados), punta del pie en el suelo.</summary>
        public static V3 RearAnkle(float x, float shin)
        {
            double p = RearShinPitch * Math.PI / 180.0;
            return new V3(x, RearKneeHeight + (float)(Math.Sin(p) * shin), -RearKneeBack - (float)(Math.Cos(p) * shin));
        }

        /// <summary>Tobillo trasero a la altura que pide la bota con la punta apoyada ("ankleY", de FootGeometry): la espinilla sube lo justo desde la rodilla.</summary>
        public static V3 RearAnkle(float x, float shin, float ankleY)
        {
            float dy = Math.Max(0f, Math.Min(shin, ankleY - RearKneeHeight));
            return new V3(x, ankleY, -RearKneeBack - (float)Math.Sqrt(shin * shin - dy * dy));
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

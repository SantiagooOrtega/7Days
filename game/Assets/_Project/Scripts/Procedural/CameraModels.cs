using System;

namespace SevenDays.Procedural
{
    /// <summary>Ajustes generales de la cámara sobre el hombro (ScriptableObject CameraSettings). La cámara de carrera tiene su propio CameraRunSettings.</summary>
    [Serializable]
    public class CameraSettingsData
    {
        public float sensitivity = 0.12f;       // grados por píxel de ratón
        public float minPitch = -35f, maxPitch = 60f;
        public float rotationSmoothing = 0.025f; // segundos (0 = sin suavizado)
        public float hipDistance = 3.0f, hipFov = 60f;
        public float hipShoulderX = 0.5f, aimShoulderX = 0.65f;
        public float shoulderSwapTime = 0.25f;
        // sacudidas (Cinemachine Impulse)
        public float shakeScale = 1f;            // global
        public float fireShakeScale = 1f;        // multiplica el fireShake de cada arma
        public float damageShake = 0.25f;        // por cada 20 de daño recibido
        public float runStepShake = 0.012f;      // pequeño golpe en cada paso corriendo
        public float landShakeScale = 1f;
        public float motionScale = 1f;           // accesibilidad "Movimiento de cámara" (0..1); se guarda en PlayerPrefs

        public void Validate()
        {
            sensitivity = CameraMath.Clamp(sensitivity, 0.01f, 1f);
            minPitch = CameraMath.Clamp(minPitch, -89f, 0f); maxPitch = CameraMath.Clamp(maxPitch, 0f, 89f);
            rotationSmoothing = CameraMath.Clamp(rotationSmoothing, 0f, 0.2f);
            hipDistance = CameraMath.Clamp(hipDistance, 1f, 8f); hipFov = CameraMath.Clamp(hipFov, 40f, 90f);
            shoulderSwapTime = CameraMath.Clamp(shoulderSwapTime, 0.05f, 1f);
            shakeScale = CameraMath.Clamp(shakeScale, 0f, 3f); fireShakeScale = CameraMath.Clamp(fireShakeScale, 0f, 3f);
            damageShake = CameraMath.Clamp(damageShake, 0f, 2f); runStepShake = CameraMath.Clamp(runStepShake, 0f, 0.2f);
            landShakeScale = CameraMath.Clamp(landShakeScale, 0f, 3f); motionScale = CameraMath.Clamp(motionScale, 0f, 1f);
        }
    }

    public static class CameraMath
    {
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        /// <summary>Ease in/out (smoothstep).</summary>
        public static float EaseInOut(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }
        public static float ClampPitch(float pitch, float min, float max) => Clamp(pitch, min, max);

        /// <summary>Suavizado exponencial independiente de los FPS: "seconds" es el tiempo característico (0 = instantáneo).</summary>
        public static float SmoothFactor(float dt, float seconds) => seconds <= 0f ? 1f : 1f - (float)Math.Exp(-dt / seconds);

        /// <summary>Ángulo de diferencia más corto (para suavizar el yaw sin dar la vuelta larga).</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f; else if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>Tamaño en píxeles de un ángulo de dispersión en pantalla (para que la retícula marque dónde pueden caer las balas).</summary>
        public static float SpreadToPixels(float spreadDegrees, float verticalFov, float screenHeight)
            => (float)(Math.Tan(spreadDegrees * Math.PI / 180.0) / Math.Tan(verticalFov * 0.5 * Math.PI / 180.0)) * screenHeight * 0.5f;
    }

    /// <summary>Hombro de la cámara: -1 izquierdo, +1 derecho. Toggle (Q) cambia de lado con una transición suave (ease in/out).</summary>
    public class ShoulderState
    {
        public int Side { get; private set; } = 1;
        public float Value { get; private set; } = 1f;
        private float _from = 1f, _t = 1f;

        public void Toggle() { _from = Value; Side = -Side; _t = 0f; }

        public void Tick(float dt, float seconds)
        {
            if (_t >= 1f) { Value = Side; return; }
            _t = Math.Min(1f, _t + dt / Math.Max(0.01f, seconds));
            Value = CameraMath.Lerp(_from, Side, CameraMath.EaseInOut(_t));
        }

        /// <summary>CameraSide de CinemachineThirdPersonFollow: 0 = izquierdo, 1 = derecho.</summary>
        public float CinemachineSide => (Value + 1f) * 0.5f;
    }

    /// <summary>Transición de apuntado: avanza linealmente con tiempos de entrada y salida, y se lee con ease in/out para el FOV, la distancia y la sensibilidad.</summary>
    public class AimTransition
    {
        public float Raw { get; private set; }
        public float Eased => CameraMath.EaseInOut(Raw);

        public void Tick(float dt, bool aiming, float inSeconds, float outSeconds)
        {
            float speed = 1f / Math.Max(0.01f, aiming ? inSeconds : outSeconds);
            Raw = CameraMath.Clamp01(Raw + (aiming ? speed : -speed) * dt);
        }

        public float Fov(float hip, float aim) => CameraMath.Lerp(hip, aim, Eased);
        public float Sensitivity(float baseSens, float aimMultiplier) => baseSens * CameraMath.Lerp(1f, aimMultiplier, Eased);
        public void Reset() => Raw = 0f;
    }

    /// <summary>Ciclo de pasos: la fase avanza según la velocidad y la zancada; un paso cada media vuelta (pie izquierdo, pie derecho).</summary>
    public class StepCycle
    {
        public float Phase { get; private set; }   // 0..2π
        public int Foot { get; private set; }      // 0 izquierdo, 1 derecho (alterna en cada paso)
        public float StepsPerSecond { get; private set; }

        /// <returns>true si en este tick se dio un paso</returns>
        public bool Tick(float dt, float speed, float stride, float cadenceBoost = 0f)
        {
            StepsPerSecond = speed <= 0.05f ? 0f : speed / Math.Max(0.2f, stride) * (1f + cadenceBoost);
            float before = Phase;
            Phase += (float)Math.PI * StepsPerSecond * dt;
            bool stepped = false;
            if (before < Math.PI && Phase >= Math.PI) stepped = true;
            if (Phase >= 2f * Math.PI) { Phase -= 2f * (float)Math.PI; stepped = true; }
            if (stepped) Foot = 1 - Foot;
            return stepped;
        }
    }

    /// <summary>Valores de la cámara de carrera (ScriptableObject CameraRunSettings). Validate impone los límites duros.</summary>
    [Serializable]
    public class CameraRunSettingsData
    {
        public const float HardMaxRoll = 1.5f, HardMaxOffset = 0.08f;

        public float fovBoost = 6f;            // +4..+8 grados al correr
        public float distanceDelta = -0.3f;    // se acerca un poco (metros)
        public float heightDelta = -0.08f;     // baja un poco (metros)
        public float swayMeters = 0.045f;      // ±3..6 cm de lado a lado
        public float rollDegrees = 0.7f;       // ±0.4..1.0 grados
        public float bobMeters = 0.03f;        // 2..4 cm de rebote
        public float runStride = 1.15f;        // metros por paso corriendo (marca la cadencia)
        public float walkStride = 0.8f;
        public float asymmetry = 0.2f;         // suma de dos senos lentos para que no sea mecánico
        public float fatigueDelay = 4f;        // segundos corriendo antes de cansarse
        public float fatigueRamp = 5f;         // segundos hasta la fatiga máxima
        public float fatigueAmplitudeBoost = 0.4f; // +40 % de amplitud con fatiga máxima
        public float fatigueCadenceBoost = 0.12f;  // pasos un poco más rápidos
        public float breathPitch = 0.5f;       // cabeceo del jadeo (grados)
        public float breathMeters = 0.006f;
        public float breathRate = 0.45f;       // respiraciones por segundo en reposo; con fatiga llega a x2
        public float recoverSeconds = 2.5f;    // de fatiga máxima a 0 al dejar de correr
        public float maxRoll = 1.5f, maxOffset = 0.08f;
        public float aimCancelSeconds = 0.15f; // al apuntar el efecto se anula en este tiempo
        public float sideOrBackFactor = 0.5f;  // correr de lado o hacia atrás
        public float blendIn = 0.35f, blendOut = 0.25f, contextBlend = 0.2f;

        public void Validate()
        {
            fovBoost = CameraMath.Clamp(fovBoost, 0f, 8f);
            distanceDelta = CameraMath.Clamp(distanceDelta, -1f, 0.5f);
            heightDelta = CameraMath.Clamp(heightDelta, -0.3f, 0.2f);
            maxRoll = CameraMath.Clamp(maxRoll, 0f, HardMaxRoll);
            maxOffset = CameraMath.Clamp(maxOffset, 0f, HardMaxOffset);
            swayMeters = CameraMath.Clamp(swayMeters, 0f, maxOffset);
            bobMeters = CameraMath.Clamp(bobMeters, 0f, maxOffset);
            rollDegrees = CameraMath.Clamp(rollDegrees, 0f, maxRoll);
            runStride = CameraMath.Clamp(runStride, 0.3f, 3f); walkStride = CameraMath.Clamp(walkStride, 0.3f, 3f);
            asymmetry = CameraMath.Clamp(asymmetry, 0f, 0.5f);
            fatigueDelay = CameraMath.Clamp(fatigueDelay, 0f, 30f); fatigueRamp = CameraMath.Clamp(fatigueRamp, 0.5f, 30f);
            fatigueAmplitudeBoost = CameraMath.Clamp(fatigueAmplitudeBoost, 0f, 0.4f);
            fatigueCadenceBoost = CameraMath.Clamp(fatigueCadenceBoost, 0f, 0.4f);
            breathPitch = CameraMath.Clamp(breathPitch, 0f, 1.5f); breathMeters = CameraMath.Clamp(breathMeters, 0f, 0.02f);
            breathRate = CameraMath.Clamp(breathRate, 0.1f, 1.5f);
            recoverSeconds = CameraMath.Clamp(recoverSeconds, 0.5f, 6f);
            aimCancelSeconds = CameraMath.Clamp(aimCancelSeconds, 0.05f, 0.5f);
            sideOrBackFactor = CameraMath.Clamp01(sideOrBackFactor);
            blendIn = CameraMath.Clamp(blendIn, 0.05f, 2f); blendOut = CameraMath.Clamp(blendOut, 0.05f, 2f); contextBlend = CameraMath.Clamp(contextBlend, 0.05f, 2f);
        }
    }

    /// <summary>Lo que el jugador está haciendo este frame (entrada del modelo de cámara de carrera).</summary>
    public struct RunCamInput
    {
        public bool Sprinting, Aiming, Crouched, InCover, Vaulting, Airborne;
        public float MoveX, MoveY;      // entrada local (-1..1): y > 0 hacia delante
        public float Speed;             // m/s horizontales
        public float MotionScale;       // accesibilidad 0..1
    }

    /// <summary>Resultado: offsets locales de la cámara (metros), roll y cabeceo (grados), cambios de FOV, distancia y altura.</summary>
    public struct RunCamOutput
    {
        public float OffsetX, OffsetY, Roll, Pitch, FovDelta, DistanceDelta, HeightDelta;
        public float Weight, Fatigue;
        public bool Stepped;
    }

    /// <summary>
    /// Cámara de carrera estilo Gears of War (pura, sin alocaciones): al correr baja, se acerca y abre un poco el FOV; se balancea con los pasos (alterna
    /// izquierda-derecha con una pequeña asimetría de dos senos lentos), con roll y rebote; tras N segundos corriendo aparece la fatiga (más amplitud, pasos más
    /// rápidos y jadeo) que se recupera con una curva suave al parar. Límites duros por parámetro; al apuntar se anula en 0.15 s; en cuclillas, en cobertura, en el
    /// vault y en el aire se desactiva, y de lado o hacia atrás se reduce a la mitad.
    /// </summary>
    public class RunCamModel
    {
        public CameraRunSettingsData Settings;
        public readonly StepCycle Steps = new StepCycle();

        public float RunTime { get; private set; }          // segundos corriendo sin parar
        public float FatigueRaw { get; private set; }       // 0..1 lineal
        public float Fatigue => CameraMath.EaseInOut(FatigueRaw);
        public float Weight { get; private set; }           // 0..1 intensidad de la carrera (lineal; se lee con ease)
        public float AimFactor { get; private set; } = 1f;  // 1 sin apuntar, 0 apuntando
        public float ContextFactor { get; private set; } = 1f;
        // telemetría para el director y las estadísticas
        public float TotalSprintSeconds { get; private set; }
        public float MaxFatigue { get; private set; }

        private float _time, _breathPhase;

        public RunCamModel(CameraRunSettingsData s) { Settings = s ?? new CameraRunSettingsData(); Settings.Validate(); }

        public RunCamOutput Tick(float dt, RunCamInput input)
        {
            var s = Settings;
            _time += dt;
            var o = new RunCamOutput();

            // --- contexto: en cuclillas, cobertura, vault o en el aire, nada; de lado o hacia atrás, la mitad
            float context = input.Crouched || input.InCover || input.Vaulting || input.Airborne ? 0f
                : (input.MoveY >= 0.7f ? 1f : s.sideOrBackFactor);
            ContextFactor = RecoilState.MoveTowards(ContextFactor, context, dt / s.contextBlend);

            // --- intensidad: sube al correr y baja al soltar Shift (ease in/out)
            bool running = input.Sprinting && input.Speed > 0.5f;
            Weight = RecoilState.MoveTowards(Weight, running ? 1f : 0f, dt / (running ? s.blendIn : s.blendOut));
            // --- apuntar lo anula en aimCancelSeconds
            AimFactor = RecoilState.MoveTowards(AimFactor, input.Aiming ? 0f : 1f, dt / (input.Aiming ? s.aimCancelSeconds : s.aimCancelSeconds * 2f));

            // --- fatiga
            if (running && context > 0f)
            {
                RunTime += dt;
                TotalSprintSeconds += dt;
                if (RunTime > s.fatigueDelay) FatigueRaw = Math.Min(1f, FatigueRaw + dt / s.fatigueRamp);
            }
            else
            {
                RunTime = 0f;
                FatigueRaw = Math.Max(0f, FatigueRaw - dt / s.recoverSeconds);
            }
            float fatigue = Fatigue;
            MaxFatigue = Math.Max(MaxFatigue, fatigue);

            // --- pasos (la cadencia sube con la fatiga: zancada más corta)
            float stride = CameraMath.Lerp(s.walkStride, s.runStride, Weight);
            o.Stepped = Steps.Tick(dt, input.Airborne ? 0f : input.Speed, stride, s.fatigueCadenceBoost * fatigue);

            float motion = CameraMath.Clamp01(input.MotionScale);
            float w = CameraMath.EaseInOut(Weight) * CameraMath.EaseInOut(ContextFactor) * AimFactor;
            float amp = w * motion * (1f + s.fatigueAmplitudeBoost * fatigue);
            float phase = Steps.Phase;
            float drift = s.asymmetry * (float)(Math.Sin(_time * 2.0 * Math.PI * 0.37) * 0.6 + Math.Sin(_time * 2.0 * Math.PI * 0.61 + 1.3) * 0.4);

            float x = amp * s.swayMeters * ((float)Math.Sin(phase) + drift);
            float y = amp * s.bobMeters * ((float)Math.Abs(Math.Sin(phase)) * 2f - 1f);
            float roll = amp * s.rollDegrees * ((float)Math.Sin(phase + 0.35) + drift * 0.5f);

            // --- jadeo: respiración que cabecea un poco; sigue mientras se recupera el aliento
            _breathPhase += dt * 2f * (float)Math.PI * s.breathRate * (1f + fatigue);
            if (_breathPhase > 2f * Math.PI) _breathPhase -= 2f * (float)Math.PI;
            float breath = fatigue * AimFactor * motion;
            float pitch = breath * s.breathPitch * (float)Math.Sin(_breathPhase);
            y += breath * s.breathMeters * (float)Math.Sin(_breathPhase);

            // --- límites duros
            float maxOff = Math.Min(s.maxOffset, CameraRunSettingsData.HardMaxOffset);
            float maxRoll = Math.Min(s.maxRoll, CameraRunSettingsData.HardMaxRoll);
            x = CameraMath.Clamp(x, -maxOff, maxOff);
            y = CameraMath.Clamp(y, -maxOff, maxOff);
            float len = (float)Math.Sqrt(x * x + y * y);
            if (len > maxOff) { x *= maxOff / len; y *= maxOff / len; }
            o.OffsetX = x; o.OffsetY = y;
            o.Roll = CameraMath.Clamp(roll, -maxRoll, maxRoll);
            o.Pitch = CameraMath.Clamp(pitch, -s.breathPitch, s.breathPitch);

            float frame = CameraMath.EaseInOut(Weight) * CameraMath.EaseInOut(ContextFactor) * AimFactor * motion;
            o.FovDelta = s.fovBoost * frame;
            o.DistanceDelta = s.distanceDelta * frame;
            o.HeightDelta = s.heightDelta * frame;
            o.Weight = w;
            o.Fatigue = fatigue;
            return o;
        }
    }

    /// <summary>
    /// Lean en cobertura: al apuntar en cobertura el jugador se asoma hacia el hombro activo (Q lo cambia). Tras una cobertura alta se inclina del todo; tras una
    /// baja apenas (se asoma por encima). Transición suave; 0 fuera de cobertura.
    /// </summary>
    public class LeanState
    {
        public float Value { get; private set; }   // -1 izquierda .. +1 derecha
        public float Seconds = 0.2f;

        public void Tick(float dt, bool inCover, bool aiming, int shoulderSide, bool tallCover)
        {
            float target = inCover && aiming ? shoulderSide * (tallCover ? 1f : 0.35f) : 0f;
            Value = RecoilState.MoveTowards(Value, target, dt / Math.Max(0.01f, Seconds));
        }

        /// <summary>Desplazamiento lateral de la cámara (metros) y roll del cuerpo (grados) para este lean.</summary>
        public float CameraOffset(float maxMeters = 0.4f) => Math.Abs(Value) * maxMeters;
        public float BodyRoll(float maxDegrees = 14f) => -Value * maxDegrees;
    }
}

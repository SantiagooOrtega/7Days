using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Valores del salto, el vault y la caída. Es serializable (Unity los guarda en el ScriptableObject PlayerMovementConfig)
    /// y no depende de UnityEngine, así que toda la lógica se prueba con EditMode.
    /// </summary>
    [Serializable]
    public class MovementSettings
    {
        // --- salto (ágil: sube con gravedad normal y cae con más fuerza)
        public float JumpHeight = 1.0f;
        public float Gravity = 22f;
        public float FallGravityMultiplier = 1.8f;
        public float CoyoteTime = 0.1f;        // se puede saltar este tiempo después de dejar el suelo
        public float JumpBufferTime = 0.1f;    // un salto pulsado justo antes de aterrizar se recuerda este tiempo
        public float AirControl = 0.35f;       // 0 = sin control en el aire, 1 = igual que en el suelo

        // --- vault / mantle (alturas medidas desde los pies)
        public float VaultMinHeight = 0.5f;
        public float VaultMaxHeight = 1.3f;
        public float MantleMaxHeight = 1.6f;   // por encima de esto no se puede pasar
        public bool AllowMantle = true;        // 1.3 a 1.6 m: solo con salto y trepada lenta
        public float VaultDuration = 0.55f;
        public float MantleDuration = 1.1f;
        public float VaultReach = 0.9f;        // distancia hacia delante a la que se busca el obstáculo
        public float VaultOverMaxDepth = 1.6f; // obstáculos más gruesos que esto: se sube encima en vez de pasarlos
        public bool AutoVaultWhenSprinting = true;

        // --- caída
        public float FallDamageThreshold = 4f; // metros
        public float FallDamagePerMeter = 18f; // daño por cada metro por encima del umbral

        // --- cámara (Cinemachine Impulse)
        public float JumpImpulse = 0.08f;
        public float LandImpulseMin = 0.15f;
        public float LandImpulseMax = 0.9f;

        public float JumpVelocity => (float)Math.Sqrt(2f * Gravity * JumpHeight);

        /// <summary>Corrige valores imposibles (por ejemplo editados a mano en el ScriptableObject).</summary>
        public void Validate()
        {
            JumpHeight = Clamp(JumpHeight, 0.2f, 3f);
            Gravity = Clamp(Gravity, 5f, 80f);
            FallGravityMultiplier = Clamp(FallGravityMultiplier, 1f, 4f);
            CoyoteTime = Clamp(CoyoteTime, 0f, 0.4f);
            JumpBufferTime = Clamp(JumpBufferTime, 0f, 0.4f);
            AirControl = Clamp(AirControl, 0f, 1f);
            VaultMinHeight = Clamp(VaultMinHeight, 0.2f, 1.2f);
            VaultMaxHeight = Math.Max(VaultMinHeight, Clamp(VaultMaxHeight, 0.4f, 2f));
            MantleMaxHeight = Math.Max(VaultMaxHeight, Clamp(MantleMaxHeight, 0.5f, 2.4f));
            VaultDuration = Clamp(VaultDuration, 0.2f, 2f);
            MantleDuration = Clamp(MantleDuration, 0.3f, 3f);
            VaultReach = Clamp(VaultReach, 0.4f, 2f);
            FallDamageThreshold = Math.Max(1f, FallDamageThreshold);
            FallDamagePerMeter = Math.Max(0f, FallDamagePerMeter);
            LandImpulseMax = Math.Max(LandImpulseMin, LandImpulseMax);
        }

        private static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
    }

    /// <summary>Coyote time y jump buffer: dos truquitos que hacen que el salto se sienta justo.</summary>
    public class JumpController
    {
        private readonly MovementSettings _s;
        private float _coyote;
        private float _buffer;

        public JumpController(MovementSettings settings) { _s = settings; }

        /// <summary>
        /// Avanza el reloj. Devuelve true si hay que saltar AHORA: se pulsó salto (o se pulsó hace menos de JumpBufferTime) y se
        /// está en el suelo (o se dejó el suelo hace menos de CoyoteTime). Consumir el salto impide saltar dos veces.
        /// </summary>
        public bool Tick(float dt, bool grounded, bool jumpPressed)
        {
            _coyote = grounded ? _s.CoyoteTime : Math.Max(0f, _coyote - dt);
            _buffer = jumpPressed ? _s.JumpBufferTime : Math.Max(0f, _buffer - dt);
            if (_buffer > 0f && _coyote > 0f)
            {
                _buffer = 0f;
                _coyote = 0f;
                return true;
            }
            return false;
        }

        /// <summary>Descarta un salto pendiente (por ejemplo, ya se usó para un vault).</summary>
        public void Consume() { _buffer = 0f; }
    }

    public enum VaultVerdict { TooLow, Vault, Mantle, TooHigh, Blocked }

    /// <summary>Decide si un obstáculo de cierta altura se puede saltar, trepar o no, y calcula el recorrido.</summary>
    public static class VaultMath
    {
        /// <param name="height">Altura de la superficie superior sobre los pies del jugador.</param>
        /// <param name="clearAbove">Hay espacio libre sobre la superficie para que quepa el jugador.</param>
        public static VaultVerdict Classify(float height, bool clearAbove, MovementSettings s)
        {
            if (height < s.VaultMinHeight) return VaultVerdict.TooLow;
            if (height > s.MantleMaxHeight) return VaultVerdict.TooHigh;
            if (!clearAbove) return VaultVerdict.Blocked;
            if (height <= s.VaultMaxHeight) return VaultVerdict.Vault;
            return s.AllowMantle ? VaultVerdict.Mantle : VaultVerdict.TooHigh;
        }

        public static float Duration(VaultVerdict v, MovementSettings s) => v == VaultVerdict.Mantle ? s.MantleDuration : s.VaultDuration;

        /// <summary>
        /// Posición del jugador durante el vault, con t en 0..1. Vault y "mantle sobre el obstáculo" (end sobre la superficie)
        /// usan el mismo recorrido; pasar de largo exige que el arco supere la superficie en el punto medio.
        /// </summary>
        /// <param name="start">Posición de los pies al empezar.</param>
        /// <param name="top">Punto de la superficie superior del obstáculo (por donde se cruza).</param>
        /// <param name="end">Posición de los pies al terminar.</param>
        public static V3 Position(VaultVerdict kind, V3 start, V3 top, V3 end, float t, float margin = 0.12f)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            if (kind == VaultVerdict.Mantle)
            {
                // trepada lenta: primero se sube casi en vertical hasta el borde y luego se desliza hacia delante
                float split = 0.6f;
                V3 pull = new V3(start.X + (top.X - start.X) * 0.3f, end.Y, start.Z + (top.Z - start.Z) * 0.3f);
                if (t < split)
                {
                    float u = Ease(t / split);
                    return new V3(start.X + (pull.X - start.X) * u, start.Y + (pull.Y - start.Y) * u, start.Z + (pull.Z - start.Z) * u);
                }
                float w = Ease((t - split) / (1f - split));
                return new V3(pull.X + (end.X - pull.X) * w, pull.Y + (end.Y - pull.Y) * w, pull.Z + (end.Z - pull.Z) * w);
            }

            // vault: curva de Bézier cuadrática cuyo punto de control hace que el punto medio pase por encima de la superficie
            float s = Ease(t);
            float cy = 2f * (top.Y + margin) - (start.Y + end.Y) * 0.5f;
            float cx = (start.X + end.X) * 0.5f, cz = (start.Z + end.Z) * 0.5f;
            float a = (1f - s) * (1f - s), b = 2f * (1f - s) * s, c = s * s;
            return new V3(a * start.X + b * cx + c * end.X, a * start.Y + b * cy + c * end.Y, a * start.Z + b * cz + c * end.Z);
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);
    }

    public static class FallDamage
    {
        /// <summary>Daño por una caída de "distance" metros: nada hasta el umbral, y luego proporcional a lo que se pasa.</summary>
        public static float Compute(float distance, MovementSettings s)
            => distance <= s.FallDamageThreshold ? 0f : (distance - s.FallDamageThreshold) * s.FallDamagePerMeter;

        /// <summary>Intensidad del impulso de cámara al aterrizar (0 si la caída es pequeña).</summary>
        public static float LandImpulse(float distance, MovementSettings s)
        {
            if (distance < 1f) return 0f;
            float k = Math.Min(1f, (distance - 1f) / Math.Max(0.01f, s.FallDamageThreshold * 1.25f - 1f));
            return s.LandImpulseMin + (s.LandImpulseMax - s.LandImpulseMin) * k;
        }
    }
}

namespace SevenDays.Procedural
{
    /// <summary>
    /// Decide cuándo bajar el nivel de detalle atmosférico: si el FPS se mantiene por debajo del objetivo
    /// durante unos segundos (pasado un calentamiento inicial), sube el nivel de recorte en 1 y espera un tiempo
    /// antes de volver a evaluar. Nivel 0 = todo; cada nivel recorta partículas y detalles. Solo baja, nunca sube sola.
    /// C# puro para poder probarlo sin Unity.
    /// </summary>
    public class QualityGovernor
    {
        public float TargetFps = 55f;
        public float WarmupSeconds = 6f;
        public float SustainSeconds = 2.5f;
        public float CooldownSeconds = 5f;
        public int MaxLevel = 3;

        public int Level { get; private set; }

        private float _time;
        private float _lowTime;
        private float _cooldown;

        /// <summary>Avanza el reloj. Devuelve true si el nivel cambió en esta llamada.</summary>
        public bool Update(float dt, float fps)
        {
            _time += dt;
            if (_time < WarmupSeconds) return false;
            if (_cooldown > 0f)
            {
                _cooldown -= dt;
                _lowTime = 0f;
                return false;
            }
            if (fps >= TargetFps)
            {
                _lowTime = 0f;
                return false;
            }
            _lowTime += dt;
            if (_lowTime < SustainSeconds || Level >= MaxLevel) return false;
            Level++;
            _lowTime = 0f;
            _cooldown = CooldownSeconds;
            return true;
        }

        /// <summary>Fija el nivel a mano (tecla F3) y reinicia las esperas.</summary>
        public void SetLevel(int level)
        {
            Level = level < 0 ? 0 : (level > MaxLevel ? MaxLevel : level);
            _lowTime = 0f;
            _cooldown = CooldownSeconds;
        }

        /// <summary>Factor de densidad (1 = completo) para un nivel: 1, 0.6, 0.3, 0.</summary>
        public static float DensityFactor(int level) => level <= 0 ? 1f : (level == 1 ? 0.6f : (level == 2 ? 0.3f : 0f));
    }
}

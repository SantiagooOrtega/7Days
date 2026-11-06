using System;

namespace SevenDays.Procedural
{
    public enum FuseState { Chasing, Burning, Exploded }
    public enum FuseEvent { None, Started, Cancelled, Detonate }

    /// <summary>
    /// Máquina de estados del zombi explosivo tipo creeper: persigue; al entrar en el radio de activación se detiene y arranca la mecha; si el jugador se aleja más
    /// que el radio de cancelación antes de que acabe, la mecha se cancela y vuelve a perseguir (con una pausa corta para no reactivarse al instante);
    /// al terminar explota. Si lo matan (también con un headshot) en cualquier momento explota igual: ForceDetonate.
    /// </summary>
    public class FuseBrain
    {
        public float TriggerRadius = 3f, CancelRadius = 5.5f, FuseSeconds = 1.35f, RetriggerDelay = 0.6f;
        public FuseState State { get; private set; } = FuseState.Chasing;
        public float Elapsed { get; private set; }
        public float Progress => State == FuseState.Burning ? Math.Min(1f, Elapsed / Math.Max(0.01f, FuseSeconds)) : (State == FuseState.Exploded ? 1f : 0f);
        private float _cooldown;

        public static FuseBrain For(ZombieTypeData t) => new FuseBrain { TriggerRadius = t.fuseTriggerRadius, CancelRadius = t.fuseCancelRadius, FuseSeconds = t.fuseSeconds };

        public void Reset() { State = FuseState.Chasing; Elapsed = 0f; _cooldown = 0f; }

        public FuseEvent Tick(float dt, float distance)
        {
            switch (State)
            {
                case FuseState.Chasing:
                    _cooldown = Math.Max(0f, _cooldown - dt);
                    if (_cooldown <= 0f && distance <= TriggerRadius) { State = FuseState.Burning; Elapsed = 0f; return FuseEvent.Started; }
                    return FuseEvent.None;
                case FuseState.Burning:
                    if (distance > CancelRadius) { State = FuseState.Chasing; Elapsed = 0f; _cooldown = RetriggerDelay; return FuseEvent.Cancelled; }
                    Elapsed += dt;
                    if (Elapsed >= FuseSeconds) { State = FuseState.Exploded; return FuseEvent.Detonate; }
                    return FuseEvent.None;
                default:
                    return FuseEvent.None;
            }
        }

        /// <summary>Lo mataron: explota ya (una sola vez).</summary>
        public FuseEvent ForceDetonate()
        {
            if (State == FuseState.Exploded) return FuseEvent.None;
            State = FuseState.Exploded;
            return FuseEvent.Detonate;
        }

        /// <summary>Inflado visual progresivo: 1 -> 1.35 al terminar la mecha, con pulsos que se aceleran.</summary>
        public float Inflate(float time) => 1f + 0.35f * Progress + (State == FuseState.Burning ? 0.03f * (float)Math.Sin(time * (14f + 30f * Progress)) : 0f);

        /// <summary>Parpadeo blanco-rojo: true = blanco. La frecuencia sube de 4 a 16 Hz a lo largo de la mecha.</summary>
        public bool FlashWhite(float time) => State == FuseState.Burning && Math.Sin(time * Math.PI * 2.0 * (4f + 12f * Progress)) > 0.0;
    }

    public static class Blast
    {
        /// <summary>Daño final de la explosión: caída con la distancia y, si una pared sólida tapa el trayecto, solo una fracción (blockedFactor).</summary>
        public static float Damage(float distance, float radius, float maxDamage, bool blockedBySolid, float blockedFactor)
        {
            float d = AreaDamage.At(distance, radius, maxDamage);
            return blockedBySolid ? d * Math.Max(0f, Math.Min(1f, blockedFactor)) : d;
        }

        /// <summary>Empuje (velocidad inicial en m/s) con la misma caída que el daño, sin pasar de "max".</summary>
        public static float Knockback(float distance, float radius, float max, bool blockedBySolid)
        {
            if (radius <= 0f || distance >= radius) return 0f;
            float k = max * (1f - Math.Max(0f, distance) / radius);
            return blockedBySolid ? k * 0.3f : k;
        }
    }
}

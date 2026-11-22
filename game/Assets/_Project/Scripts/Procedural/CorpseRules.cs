namespace SevenDays.Procedural
{
    /// <summary>
    /// Cadáveres de zombi: intangibles (sin colliders, sin agente ni obstáculo del NavMesh), visibles ~15 s con su animación de muerte, se hunden los últimos
    /// 2.5 s y vuelven al pool. Como mucho 20 a la vista: al pasar del tope, el más antiguo empieza a hundirse ya.
    /// </summary>
    public static class CorpseRules
    {
        public const float Seconds = 15f, SinkSeconds = 2.5f, SinkDepth = 0.9f;
        public const int MaxVisible = 20;
        public static float SinkStart => Seconds - SinkSeconds;

        /// <summary>Cuánto se ha hundido (m) a los "age" segundos de morir.</summary>
        public static float Sink(float age) => age <= SinkStart ? 0f : System.Math.Min(1f, (age - SinkStart) / SinkSeconds) * SinkDepth;

        /// <summary>¿Hay que retirarlo (volver al pool)?</summary>
        public static bool Expired(float age) => age >= Seconds;
    }
}

namespace SevenDays.Look
{
    /// <summary>
    /// Fuegos del mapa: barriles, fogatas entre escombros, coches incendiados y antorchas de la casa. Cada uno lleva llama, humo, chispas y una luz
    /// parpadeante SIN sombras; solo las MaxFireLights más cercanas al jugador la tienen encendida. Si el FPS baja de 55 se recortan partículas y luces.
    /// </summary>
    public class FireSettings
    {
        public bool Enabled = true;
        /// <summary>Tope de fuegos activos a la vez (los demás se ignoran).</summary>
        public int MaxFires = 16;
        /// <summary>Tope de luces de fuego encendidas a la vez (las más cercanas al jugador).</summary>
        public int MaxFireLights = 6;
        /// <summary>Daño por segundo si el jugador se queda dentro del fuego. 0 = no quema.</summary>
        public float BurnDps = 4f;
        public float BurnTickSeconds = 0.25f;
        public float SmokeAmount = 1f;
        public float SparkAmount = 1f;
        public float FlameScale = 1f;
        public float LightIntensity = 1f;
    }
}

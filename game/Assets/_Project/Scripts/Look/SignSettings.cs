namespace SevenDays.Look
{
    /// <summary>Señales de tráfico desgastadas: cantidad, tope, luces rojas y tamaño de las texturas. Density 0 = sin señales.</summary>
    public class SignSettings
    {
        public float Density = 1f;
        public int Seed = 11;
        /// <summary>Tope de señales colocadas (placas y postes). Los 60 FPS no dependen de esto: van con GPU instancing.</summary>
        public int MaxSigns = 24;
        /// <summary>Cuántas señales llevan una luz roja parpadeante.</summary>
        public int RedLightSigns = 2;
        /// <summary>Resolución de la textura de cada señal (256 recomendado).</summary>
        public int TextureSize = 256;
        /// <summary>Marcas viales (línea discontinua, pasos de cebra, flechas) sobre el asfalto.</summary>
        public bool RoadMarkings = true;
    }
}

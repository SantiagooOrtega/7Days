namespace SevenDays.Look
{
    /// <summary>
    /// Parámetros del ambiente apocalíptico (niebla baja, bruma, ceniza, humo, chispas, rayos de luz, suciedad del suelo).
    /// 0 apaga el efecto; "Density" multiplica todo de golpe. Si el FPS baja de 55, el gobernador recorta estos efectos solo.
    /// </summary>
    public class AtmosphereSettings
    {
        /// <summary>Multiplicador general de densidad de todos los efectos (0.5 = la mitad).</summary>
        public float Density = 1f;

        // Niebla baja y bruma
        public int GroundFogLayers = 2;
        public float GroundFogDensity = 1f;     // opacidad/cantidad de la niebla pegada al suelo
        public float GroundFogAlpha = 0.075f;   // transparencia de cada nube
        public int HazeBanks = 10;              // nubes grandes de bruma entre los edificios lejanos

        // Cielo apagado
        public float SkyDim = 0.3f;             // 0 = cielo del color de la niebla, 1 = gris oscuro

        // Partículas
        public float Ash = 1f;                  // ceniza flotando alrededor del jugador
        public int SmokeColumns = 3;            // columnas de humo negro a lo lejos
        public float Smoke = 1f;
        public float Sparks = 1f;               // chispas en los barriles con fuego
        public int GodRays = 7;                 // rayos de luz falsos
        public float GodRayIntensity = 0.13f;

        // Luces
        public int RedFlickerLamps = 2;         // farolas con parpadeo rojizo

        // Suciedad del suelo
        public int Cracks = 14;                 // grietas en el asfalto y el concreto
        public int WeedTufts = 70;              // matas de maleza que salen de las grietas y los bordes
        public int Puddles = 7;
        public int BloodStains = 8;

        // Skyline lejano
        public int SkylineBuildings = 22;
        /// <summary>Lado (m) de la zona cubierta por la niebla baja y la suciedad del suelo (56 en el mapa 1; lo fija el tamaño del mapa).</summary>
        public float Area = 56f;
        /// <summary>Luciérnagas alrededor del jugador (bosque).</summary>
        public float Fireflies = 0f;

        /// <summary>Cantidad ajustada por la densidad global.</summary>
        public int Scaled(int count) => count <= 0 ? 0 : System.Math.Max(0, (int)System.Math.Round(count * Density));
    }
}

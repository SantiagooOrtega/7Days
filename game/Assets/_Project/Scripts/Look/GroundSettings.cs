using SevenDays.Procedural;

namespace SevenDays.Look
{
    /// <summary>
    /// Piso fuera de la autopista: un mapa de mezcla de 60 x 60 m generado por código (sin repetición) con tierra agrietada, grava, ceniza,
    /// barro, hierba seca y concreto roto, más una textura de detalle repetida a otra escala. Las abundancias van de 0 (nada) a 2 (el doble).
    /// La autopista no se toca (el asfalto va encima).
    /// </summary>
    public class GroundSettings
    {
        public bool Enabled = true;
        /// <summary>Resolución del mapa: 1024 (≈ 6 cm por píxel) o 2048 (más nítido, ~4 veces más memoria y tiempo de carga).</summary>
        public int MapSize = 1024;
        public int Seed = 21;
        /// <summary>Resolución de la textura de detalle (256 recomendado) y cuántos metros cubre cada repetición.</summary>
        public int DetailSize = 256;
        public float DetailTileMeters = 1.2f;
        public float DetailStrength = 1f;

        public float ZoneScale = 1f;       // 1 = zonas de ~15 m
        public float Earth = 1f, Gravel = 1f, Ash = 1f, Mud = 1f, DryGrass = 1f, Concrete = 1f;
        public float RoadDebris = 1f;      // escombro y arena junto al borde de la vía
        public float Cracks = 1f, Pebbles = 1f, Ruts = 1f, Footprints = 1f;
        public float NormalStrength = 1.6f;
        public float Brightness = 1.35f; // igual que la paleta de las demás texturas; además escala con el Brillo global

        /// <summary>Calcomanías de manchas oscuras y quemaduras (además de las grietas, charcos y sangre de Atmosphere).</summary>
        public int StainDecals = 10;
        public int BurnDecals = 6;

        public GroundParams ToParams(float brightness = 1f) => new GroundParams
        {
            Size = MapSize, Seed = Seed, ZoneScale = ZoneScale,
            Earth = Earth, Gravel = Gravel, Ash = Ash, Mud = Mud, DryGrass = DryGrass, Concrete = Concrete,
            RoadDebris = RoadDebris, Cracks = Cracks, Pebbles = Pebbles, Ruts = Ruts, Footprints = Footprints,
            NormalStrength = NormalStrength, Brightness = Brightness * brightness,
        };
    }
}

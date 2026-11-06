namespace SevenDays.Look
{
    /// <summary>
    /// Densidad y topes de la vegetación y los props de abandono. Density multiplica las cantidades por defecto
    /// (0 = nada); los Max* son topes duros para cuidar los 60 FPS. Si existe Resources/Layouts/props.json, esa
    /// distribución (formato PropLayoutData) reemplaza a la generada, y así un mapa de la BD puede colocar todo.
    /// </summary>
    public class VegetationSettings
    {
        public float Density = 1f;
        public int Seed = 3;
        public int MaxTrees = 90;
        public int MaxBushes = 60;
        public int MaxGrass = 260;
        public int MaxProps = 48;
        /// <summary>Los árboles proyectan sombra (más realista, algo más de GPU). Arbustos y hierba nunca.</summary>
        public bool TreeShadows = false;
        public string LayoutResource = "Layouts/props";
    }
}

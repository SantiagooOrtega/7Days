namespace SevenDays.Look
{
    /// <summary>Casa del mapa. La casa en sí (posición, aberturas, muebles) está en el JSON Resources/Layouts/house.json o en HouseLayoutData.Default().</summary>
    public class HouseSettings
    {
        public bool Enabled = true;
        /// <summary>La ventana grande tiene un enlace de NavMesh (zona transitable); el director puede apagarlo al bloquearla.</summary>
        public bool WindowsWalkable = true;
    }
}

using UnityEngine;

namespace SevenDays.Core
{
    /// <summary>
    /// Atajos de depuración (daño y curación de prueba, saltar fase, zombis, oleada y jefe de prueba, cambiar de mapa, calidad y ambiente, FPS y paneles F5-F8):
    /// solo en el editor o en una Development Build. En la build final quedan los controles de juego, F1 (ayuda) y F4 (panel del director).
    /// </summary>
    public static class DebugKeys
    {
        /// <summary>Para las pruebas: se comporta como la build final aunque sea el editor.</summary>
        public static bool ForceRelease;
        public static bool Enabled => !ForceRelease && (Application.isEditor || Debug.isDebugBuild);
    }
}

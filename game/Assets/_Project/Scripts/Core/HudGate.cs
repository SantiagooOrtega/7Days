namespace SevenDays.Core
{
    /// <summary>
    /// Puerta única del HUD: mientras la intro o el epílogo en cómic están en pantalla solo se ven la lámina, la caja de VIGÍA, SALTAR y los puntos de
    /// progreso (los dibuja IntroPlayer). Todo lo demás (IMGUI y uGUI del juego) lo consulta y no se dibuja; vuelve solo al terminar o saltar.
    /// </summary>
    public static class HudGate
    {
        public static bool Hidden => SevenDays.Intro.IntroPlayer.Active;
    }
}

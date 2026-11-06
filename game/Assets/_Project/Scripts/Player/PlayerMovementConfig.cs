using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// ScriptableObject con los valores de salto, vault y caída (altura de salto, alturas mín/máx de vault, duración, coyote time,
    /// jump buffer, gravedad...). Es opcional: sin asset se usan los valores por defecto. Para crear uno:
    /// clic derecho en Assets/_Project/Resources/Config > Create > 7Days > Movimiento del jugador, y nómbralo "Movement".
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Movimiento del jugador", fileName = "Movement")]
    public class PlayerMovementConfig : ScriptableObject
    {
        public MovementSettings Settings = new MovementSettings();

        public static MovementSettings LoadSettings()
        {
            var cfg = Resources.Load<PlayerMovementConfig>("Config/Movement");
            var s = cfg != null ? cfg.Settings : new MovementSettings();
            s.Validate();
            return s;
        }
    }
}

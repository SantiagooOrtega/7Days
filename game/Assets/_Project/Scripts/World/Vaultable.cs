using UnityEngine;

namespace SevenDays.World
{
    /// <summary>
    /// Marca un obstáculo que el jugador puede saltar (vault) o trepar (mantle): cajas, muros bajos, barreras, coches.
    /// Si su superficie superior está entre VaultMinHeight y VaultMaxHeight pasa por encima; entre VaultMaxHeight y MantleMaxHeight
    /// solo se trepa con salto, despacio; por encima no se puede pasar. Los valores salen de MovementSettings.
    /// Para usarlo en un objeto propio del editor: añade este componente a cualquier objeto con un BoxCollider.
    /// Los CoverObject lo traen puesto automáticamente (una cobertura baja también se puede saltar).
    /// </summary>
    [DisallowMultipleComponent]
    public class Vaultable : MonoBehaviour
    {
        /// <summary>Decisión pendiente para la Fase 3: ¿los zombies pueden pasar por encima? Por ahora no.</summary>
        public bool AllowZombies = false;
    }
}

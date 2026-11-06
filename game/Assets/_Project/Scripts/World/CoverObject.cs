using UnityEngine;

namespace SevenDays.World
{
    /// <summary>
    /// Marca un objeto como cobertura. Para usarlo en un objeto propio del editor: añade este
    /// componente a cualquier objeto con un BoxCollider (cajas, muros bajos, barriles cuadrados...). Trae un Vaultable:
    /// una cobertura baja también se puede saltar (la altura decide si se puede: ver MovementSettings).
    /// </summary>
    [RequireComponent(typeof(Collider), typeof(Vaultable))]
    public class CoverObject : MonoBehaviour
    {
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (TryGetComponent<Collider>(out var c)) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
    }
}

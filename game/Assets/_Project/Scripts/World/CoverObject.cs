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
        private void Awake()
        {
            // Todas las coberturas se pueden romper con el ataque de un jefe: las pequeñas (cajas, muebles) con la carga o el lanzamiento, las grandes (contenedores, barreras) solo con un lanzamiento.
            if (GetComponent<Destructible>() != null) return;
            var d = gameObject.AddComponent<Destructible>();
            var c = GetComponent<Collider>();
            Vector3 size = c != null ? c.bounds.size : Vector3.one;
            d.Strength = Mathf.Max(size.x, size.z) > 2.6f || size.x * size.y * size.z > 4f ? 1 : 0;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (TryGetComponent<Collider>(out var c)) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
    }
}

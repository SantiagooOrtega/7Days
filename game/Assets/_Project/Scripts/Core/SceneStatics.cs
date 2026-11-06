using UnityEngine;
using UnityEngine.SceneManagement;
using SevenDays.Combat;
using SevenDays.Player;
using SevenDays.World;

namespace SevenDays.Core
{
    /// <summary>
    /// Limpia el estado estático que sobrevive a una recarga de escena (al morir y reiniciar). [RuntimeInitializeOnLoadMethod] solo corre al dar Play, NO al recargar:
    /// por eso listas como PlayerInteract.Registry conservaban pickups destruidos y daban MissingReferenceException en cada frame.
    /// </summary>
    public static class SceneStatics
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            SceneManager.sceneUnloaded -= OnUnloaded;
            SceneManager.sceneUnloaded += OnUnloaded;
        }

        private static void OnUnloaded(Scene s) => ResetAll();

        /// <summary>También se llama al construir los gestores de una partida, por si el orden de los eventos de escena cambia.</summary>
        public static void ResetAll()
        {
            PlayerInteract.Registry.Clear();
            Destructible.All.Clear();
            Projectile.ResetPool();
            GroundMarker.ResetPools();
            HudMessages.Clear();
        }
    }
}

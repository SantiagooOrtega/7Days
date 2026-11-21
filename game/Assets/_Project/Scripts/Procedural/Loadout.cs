using System;
using System.Collections.Generic;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>Un arma que lleva el jugador y las balas que tiene en el cargador. Las reservas son del inventario, por tipo de munición.</summary>
    public class WeaponState
    {
        public WeaponData Data;
        public int Magazine;
        public WeaponState(WeaponData data, int magazine) { Data = data; Magazine = magazine; }
    }

    public enum PickupResult { Picked, Full }

    /// <summary>Qué pasa al recoger un arma: se añade, se convierte en munición (ya la tienes) o, con las casillas llenas, se ofrece cambiarla por la activa.</summary>
    public enum WeaponPickupKind { Add, AmmoInstead, SwapOffer }

    /// <summary>
    /// Operaciones puras del inventario de armas (probadas en LoadoutTests): soltar, intercambiar y recoger. La munición no se pierde nunca: el cargador del arma
    /// soltada va con ella al suelo y vuelve al recogerla; si ya tienes esa arma, su cargador pasa a la reserva (y si no cabe entero, no se recoge).
    /// </summary>
    public static class Loadout
    {
        /// <summary>Cargador de un arma recogida: el que traía (si se soltó antes) o medio cargador si es nueva del mapa.</summary>
        public static int MagazineFor(WeaponData d, int carried) => d.IsMelee ? 0 : (carried >= 0 ? Math.Min(carried, d.magazineSize) : Math.Max(1, d.magazineSize / 2));

        public static WeaponPickupKind Decide(IList<WeaponState> weapons, string id, int max)
        {
            foreach (var w in weapons) if (w.Data.id == id) return WeaponPickupKind.AmmoInstead;
            return weapons.Count >= max ? WeaponPickupKind.SwapOffer : WeaponPickupKind.Add;
        }

        /// <summary>Se puede soltar si queda al menos otra arma (nunca te quedas con las manos vacías).</summary>
        public static bool CanDrop(IList<WeaponState> weapons, int index) => weapons.Count > 1 && index >= 0 && index < weapons.Count;

        /// <summary>Quita el arma de esa casilla y la devuelve (con su cargador) para dejarla en el suelo; null si no se puede.</summary>
        public static WeaponState Drop(List<WeaponState> weapons, int index)
        {
            if (!CanDrop(weapons, index)) return null;
            var w = weapons[index];
            weapons.RemoveAt(index);
            return w;
        }

        /// <summary>Casilla activa tras quitar "removed": la misma arma si sigue, o la anterior.</summary>
        public static int IndexAfterRemove(int removed, int current, int newCount)
        {
            if (newCount <= 0) return 0;
            if (current > removed) current--;
            else if (current == removed) current = Math.Max(0, removed - 1);
            return Math.Min(current, newCount - 1);
        }

        /// <summary>Cambia el arma activa por la recogida (misma casilla). Devuelve la que se suelta, con su cargador.</summary>
        public static WeaponState Swap(List<WeaponState> weapons, int active, WeaponData picked, int carriedMagazine)
        {
            var old = weapons[active];
            weapons[active] = new WeaponState(picked, MagazineFor(picked, carriedMagazine));
            return old;
        }

        /// <summary>Balas que da un arma repetida: su cargador si se había soltado, si no medio cargador.</summary>
        public static int DuplicateAmmo(WeaponData d, int carried) => d.UsesAmmo ? (carried >= 0 ? carried : Math.Max(1, d.magazineSize / 2)) : 0;

        /// <summary>Todas las balas que lleva el jugador (cargadores + reservas): para comprobar que nada se pierde.</summary>
        public static int TotalRounds(IList<WeaponState> weapons, AmmoReserve ammo)
        {
            int n = 0;
            foreach (var w in weapons) n += w.Magazine;
            foreach (AmmoType t in Enum.GetValues(typeof(AmmoType))) n += ammo.Count(t);
            return n;
        }

        public static bool HasAnyAmmo(IList<WeaponState> weapons, AmmoReserve ammo)
        {
            for (int i = 0; i < weapons.Count; i++) { var w = weapons[i]; if (w.Data.UsesAmmo && (w.Magazine > 0 || ammo.Count(w.Data.ammoType) > 0)) return true; }   // sin enumerador (se llama cada frame)
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Player
{
    /// <summary>
    /// Todo lo que lleva el jugador: armas (máximo 5), munición por tipo con reserva máxima, vendas y botiquines. Empieza con muy poco (arma inicial y
    /// pocas balas). Recoger algo que no cabe devuelve Full y NO consume el pickup.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public const int MaxWeapons = 5;

        public readonly List<WeaponState> Weapons = new List<WeaponState>();
        public AmmoReserve Ammo { get; private set; }
        public MedkitInventory Medkits { get; } = new MedkitInventory();
        public event Action Changed;
        public event Action<WeaponState> WeaponAdded;

        public void Initialize(LootLayoutData loot)
        {
            Ammo = new AmmoReserve(loot.ammoLimits);
            Weapons.Clear();
            bool firstFirearm = true;
            foreach (var id in loot.startWeapons)
            {
                var data = WeaponLibrary.Get(id);
                if (data == null) continue;
                int mag = data.IsMelee ? 0 : (firstFirearm ? Mathf.Min(loot.startMagazine, data.magazineSize) : data.magazineSize);
                if (!data.IsMelee) firstFirearm = false;
                Weapons.Add(new WeaponState(data, mag));
            }
            // reserva inicial: poca, del tipo del primer arma de fuego
            var first = Weapons.Find(w => !w.Data.IsMelee);
            if (first != null) Ammo.Add(first.Data.ammoType, loot.startReserve);
            Changed?.Invoke();
        }

        public int Reserve(WeaponState w) => w.Data.UsesAmmo ? Ammo.Count(w.Data.ammoType) : 0;

        /// <summary>Añade un arma. Si ya la tiene se convierte en munición (su cargador si se había soltado); si las 5 casillas están llenas devuelve Full
        /// (el pickup ofrece entonces cambiarla por la activa manteniendo E).</summary>
        public PickupResult TryAddWeapon(string id, out string message, int magazine = -1)
        {
            var data = WeaponLibrary.Get(id);
            message = "";
            if (data == null) return PickupResult.Full;
            switch (Loadout.Decide(Weapons, id, MaxWeapons))
            {
                case WeaponPickupKind.AmmoInstead:
                {
                    int ammo = Loadout.DuplicateAmmo(data, magazine);
                    if (ammo > 0 && Ammo.Space(data.ammoType) < ammo) { message = "Ya tienes " + data.displayName + " y no cabe su munición"; return PickupResult.Full; }
                    if (ammo > 0) Ammo.Add(data.ammoType, ammo);
                    message = "Ya tenías " + data.displayName + ": +" + ammo + " balas";
                    Changed?.Invoke();
                    return PickupResult.Picked;
                }
                case WeaponPickupKind.SwapOffer:
                    SevenDays.Vigia.VigiaVoice.Say("inventario_lleno");
                    message = "Armas llenas (máx. " + MaxWeapons + "): mantén E para cambiarla por la activa · G suelta un arma";
                    return PickupResult.Full;
            }
            var state = new WeaponState(data, Loadout.MagazineFor(data, magazine));
            Weapons.Add(state);
            message = data.displayName + " (" + WeaponCatalog.RarityName(data.rarity) + ")";
            WeaponAdded?.Invoke(state);
            Changed?.Invoke();
            return PickupResult.Picked;
        }

        /// <summary>Quita el arma de esa casilla (para soltarla). null si es la única.</summary>
        public WeaponState RemoveWeapon(int index)
        {
            var w = Loadout.Drop(Weapons, index);
            if (w != null) Changed?.Invoke();
            return w;
        }

        /// <summary>Cambia el arma de la casilla activa por otra. Devuelve la que se suelta.</summary>
        public WeaponState SwapWeapon(int active, string id, int magazine)
        {
            var data = WeaponLibrary.Get(id);
            if (data == null || active < 0 || active >= Weapons.Count) return null;
            var old = Loadout.Swap(Weapons, active, data, magazine);
            WeaponAdded?.Invoke(Weapons[active]);
            Changed?.Invoke();
            return old;
        }

        public bool HasAnyAmmo => Loadout.HasAnyAmmo(Weapons, Ammo);

        public PickupResult TryAddAmmo(AmmoType type, int amount, out string message)
        {
            int added = Ammo.Add(type, amount);
            if (added == 0) { message = "Munición llena (" + AmmoName(type) + ")"; return PickupResult.Full; }
            message = "+" + added + " balas de " + AmmoName(type) + (added < amount ? " (reserva llena)" : "");
            Changed?.Invoke();
            return PickupResult.Picked;
        }

        public PickupResult TryAddMedkit(MedkitKind kind, int amount, out string message)
        {
            int added = Medkits.Add(kind, amount);
            var d = MedkitData.For(kind);
            if (added == 0) { message = d.Name + ": ya llevas el máximo"; return PickupResult.Full; }
            message = d.Name + " +" + added;
            Changed?.Invoke();
            return PickupResult.Picked;
        }

        /// <summary>Recoge un objeto de botín cualquiera.</summary>
        public PickupResult TryPickup(LootItem item, out string message)
        {
            switch (item.kind)
            {
                case "weapon": return TryAddWeapon(item.id, out message, item.magazine);
                case "ammo": return TryAddAmmo(item.AmmoTypeValue, item.amount, out message);
                default: return TryAddMedkit(item.MedkitKindValue, item.amount, out message);
            }
        }

        public void NotifyChanged() => Changed?.Invoke();

        public static string AmmoName(AmmoType t) => t == AmmoType.Pistol ? "pistola" : (t == AmmoType.Shotgun ? "escopeta" : (t == AmmoType.Rifle ? "rifle" : "—"));
    }
}

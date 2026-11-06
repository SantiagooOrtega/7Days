using System;
using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Weapons;

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

        /// <summary>Añade un arma. Si ya la tiene se convierte en un poco de munición; si las 5 casillas están llenas devuelve Full.</summary>
        public PickupResult TryAddWeapon(string id, out string message)
        {
            var data = WeaponLibrary.Get(id);
            message = "";
            if (data == null) return PickupResult.Full;
            var existing = Weapons.Find(w => w.Data.id == id);
            if (existing != null)
            {
                int ammo = data.UsesAmmo ? Mathf.Max(1, data.magazineSize / 2) : 0;
                if (ammo > 0 && Ammo.IsFull(data.ammoType)) { message = "Ya tienes " + data.displayName + " y la munición está llena"; return PickupResult.Full; }
                int added = ammo > 0 ? Ammo.Add(data.ammoType, ammo) : 0;
                message = "Ya tenías " + data.displayName + ": +" + added + " balas";
                Changed?.Invoke();
                return PickupResult.Picked;
            }
            if (Weapons.Count >= MaxWeapons) { message = "Armas llenas (máx. " + MaxWeapons + ")"; return PickupResult.Full; }
            var state = new WeaponState(data, data.IsMelee ? 0 : Mathf.Max(1, data.magazineSize / 2));
            Weapons.Add(state);
            message = data.displayName + " (" + WeaponCatalog.RarityName(data.rarity) + ")";
            WeaponAdded?.Invoke(state);
            Changed?.Invoke();
            return PickupResult.Picked;
        }

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
                case "weapon": return TryAddWeapon(item.id, out message);
                case "ammo": return TryAddAmmo(item.AmmoTypeValue, item.amount, out message);
                default: return TryAddMedkit(item.MedkitKindValue, item.amount, out message);
            }
        }

        public void NotifyChanged() => Changed?.Invoke();

        public static string AmmoName(AmmoType t) => t == AmmoType.Pistol ? "pistola" : (t == AmmoType.Shotgun ? "escopeta" : (t == AmmoType.Rifle ? "rifle" : "—"));
    }
}

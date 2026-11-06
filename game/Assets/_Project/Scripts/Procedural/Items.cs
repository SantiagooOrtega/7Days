using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum AmmoType { None, Pistol, Shotgun, Rifle }
    public enum Rarity { Common, Uncommon, Rare, Epic }
    public enum WeaponKind { Pistol, Revolver, Smg, Shotgun, AssaultRifle, SniperRifle, Melee }
    public enum MedkitKind { Bandage, Small, Medium, Large }

    /// <summary>
    /// Datos de un arma (se guardan en un ScriptableObject por arma, y por JSON para el backend y Gemini offline): daño, cadencia, cargador,
    /// retroceso, dispersión, alcance, rareza y tipo de munición. Las armas cuerpo a cuerpo no usan munición.
    /// </summary>
    [Serializable]
    public class WeaponData
    {
        public string id;
        public string displayName;
        public WeaponKind kind;
        public Rarity rarity;
        public AmmoType ammoType;
        public float damage = 20f;          // por bala (por perdigón en la escopeta)
        public int pellets = 1;             // perdigones por disparo
        public float roundsPerMinute = 600f;
        public int magazineSize = 30;
        public float reloadTime = 2f;
        public float recoil = 0.8f;         // grados de retroceso de cámara por disparo
        public float spreadHip = 2f;        // grados sin apuntar
        public float spreadAim = 0.4f;      // grados apuntando
        public float range = 100f;          // metros (alcance del golpe en el cuerpo a cuerpo)
        public bool automatic;
        public float[] color = { 0.15f, 0.15f, 0.17f }; // aspecto del arma
        public float length = 0.7f, thickness = 0.09f;
        // --- daño diferenciado
        public float minDamageFraction = 1f;   // fracción del daño que queda a distancia máxima (1 = sin falloff)
        public float falloffStart = 0.4f;      // fracción del alcance donde empieza a caer el daño
        public float armorPenetration = 0f;    // 0..1: cuánta de la resistencia del enemigo ignora
        public float headshotMultiplier = 1f;  // multiplica el multiplicador de cabeza del zombie (propio de cada arma)
        public float knockback = 1f;           // empuje al enemigo: la escopeta empuja más que la pistola

        public bool IsMelee => kind == WeaponKind.Melee;
        public bool UsesAmmo => ammoType != AmmoType.None;
        public float SecondsBetweenShots => 60f / Math.Max(1f, roundsPerMinute);
        public float DamagePerShot => damage * Math.Max(1, pellets);
        /// <summary>Daño por segundo teórico (sin recargas), para equilibrar.</summary>
        public float Dps => DamagePerShot * roundsPerMinute / 60f;

        /// <summary>Corrige valores imposibles.</summary>
        public void Validate()
        {
            damage = Math.Max(1f, damage);
            pellets = Math.Max(1, pellets);
            roundsPerMinute = Math.Max(10f, roundsPerMinute);
            magazineSize = IsMelee ? 0 : Math.Max(1, magazineSize);
            reloadTime = Math.Max(0.2f, reloadTime);
            spreadHip = Math.Max(0f, spreadHip); spreadAim = Math.Max(0f, Math.Min(spreadAim, spreadHip));
            range = Math.Max(1f, range);
            minDamageFraction = Math.Max(0f, Math.Min(1f, minDamageFraction));
            falloffStart = Math.Max(0f, Math.Min(0.95f, falloffStart));
            armorPenetration = Math.Max(0f, Math.Min(1f, armorPenetration));
            headshotMultiplier = Math.Max(0.1f, headshotMultiplier);
            knockback = Math.Max(0f, knockback);
            if (IsMelee) ammoType = AmmoType.None;
            else if (ammoType == AmmoType.None) ammoType = AmmoType.Pistol;
        }
    }

    /// <summary>Las 7 armas por defecto. Sus ScriptableObjects se crean desde estos datos y el JSON puede reemplazarlos.</summary>
    public static class WeaponCatalog
    {
        public static List<WeaponData> Defaults() => new List<WeaponData>
        {
            new WeaponData { id = "pistol", displayName = "Pistola", kind = WeaponKind.Pistol, rarity = Rarity.Common, ammoType = AmmoType.Pistol,
                damage = 18f, roundsPerMinute = 330f, magazineSize = 12, reloadTime = 1.3f, recoil = 1.2f, spreadHip = 1.4f, spreadAim = 0.25f, range = 70f, automatic = false,
                color = new[] { 0.3f, 0.3f, 0.32f }, length = 0.35f, minDamageFraction = 0.5f, falloffStart = 0.4f, armorPenetration = 0.1f, headshotMultiplier = 1.2f, knockback = 0.8f },
            new WeaponData { id = "revolver", displayName = "Revólver", kind = WeaponKind.Revolver, rarity = Rarity.Uncommon, ammoType = AmmoType.Pistol,
                damage = 48f, roundsPerMinute = 130f, magazineSize = 6, reloadTime = 2.6f, recoil = 2.6f, spreadHip = 1.8f, spreadAim = 0.2f, range = 90f, automatic = false,
                color = new[] { 0.36f, 0.33f, 0.3f }, length = 0.4f, minDamageFraction = 0.6f, falloffStart = 0.45f, armorPenetration = 0.35f, headshotMultiplier = 1.3f, knockback = 1.4f },
            new WeaponData { id = "smg", displayName = "Subfusil", kind = WeaponKind.Smg, rarity = Rarity.Uncommon, ammoType = AmmoType.Pistol,
                damage = 12f, roundsPerMinute = 850f, magazineSize = 30, reloadTime = 2.0f, recoil = 0.5f, spreadHip = 3.2f, spreadAim = 1.2f, range = 55f, automatic = true,
                color = new[] { 0.2f, 0.21f, 0.23f }, length = 0.55f, minDamageFraction = 0.35f, falloffStart = 0.3f, armorPenetration = 0f, headshotMultiplier = 1.0f, knockback = 0.5f },
            new WeaponData { id = "shotgun", displayName = "Escopeta", kind = WeaponKind.Shotgun, rarity = Rarity.Uncommon, ammoType = AmmoType.Shotgun,
                damage = 12f, pellets = 8, roundsPerMinute = 70f, magazineSize = 6, reloadTime = 3.2f, recoil = 3.4f, spreadHip = 6f, spreadAim = 4.2f, range = 28f, automatic = false,
                color = new[] { 0.28f, 0.2f, 0.14f }, length = 0.85f, thickness = 0.11f, minDamageFraction = 0.12f, falloffStart = 0.15f, armorPenetration = 0.05f, headshotMultiplier = 1.0f, knockback = 2.4f },
            new WeaponData { id = "assault_rifle", displayName = "Fusil de asalto", kind = WeaponKind.AssaultRifle, rarity = Rarity.Rare, ammoType = AmmoType.Rifle,
                damage = 36f, roundsPerMinute = 520f, magazineSize = 30, reloadTime = 2.2f, recoil = 0.8f, spreadHip = 2.2f, spreadAim = 0.4f, range = 120f, automatic = true,
                color = new[] { 0.15f, 0.15f, 0.17f }, length = 0.8f, minDamageFraction = 0.55f, falloffStart = 0.35f, armorPenetration = 0.3f, headshotMultiplier = 1.0f, knockback = 1.0f },
            new WeaponData { id = "sniper", displayName = "Rifle de francotirador", kind = WeaponKind.SniperRifle, rarity = Rarity.Epic, ammoType = AmmoType.Rifle,
                damage = 90f, roundsPerMinute = 55f, magazineSize = 5, reloadTime = 3.0f, recoil = 3.8f, spreadHip = 3.5f, spreadAim = 0.03f, range = 260f, automatic = false,
                color = new[] { 0.12f, 0.16f, 0.13f }, length = 1.1f, thickness = 0.08f, minDamageFraction = 1f, falloffStart = 0.5f, armorPenetration = 0.8f, headshotMultiplier = 1.3f, knockback = 2.0f },
            new WeaponData { id = "bar", displayName = "Barra metálica", kind = WeaponKind.Melee, rarity = Rarity.Common, ammoType = AmmoType.None,
                damage = 38f, roundsPerMinute = 100f, magazineSize = 0, reloadTime = 0.2f, recoil = 1.0f, spreadHip = 0f, spreadAim = 0f, range = 2.1f, automatic = false,
                color = new[] { 0.42f, 0.4f, 0.38f }, length = 0.9f, thickness = 0.05f, minDamageFraction = 1f, armorPenetration = 0.2f, headshotMultiplier = 1.0f, knockback = 1.8f },
        };

        public static WeaponData Get(string id)
        {
            foreach (var w in Defaults()) if (w.id == id) return w;
            return null;
        }

        public static bool Exists(string id) => Get(id) != null;

        /// <summary>Color de rareza (común gris, poco común verde, rara azul, épica morada) para el HUD y el brillo de los drops.</summary>
        public static float[] RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return new[] { 0.35f, 0.85f, 0.4f };
                case Rarity.Rare: return new[] { 0.3f, 0.55f, 1f };
                case Rarity.Epic: return new[] { 0.75f, 0.35f, 1f };
                default: return new[] { 0.8f, 0.8f, 0.8f };
            }
        }

        public static string RarityName(Rarity r)
        {
            switch (r) { case Rarity.Uncommon: return "poco común"; case Rarity.Rare: return "rara"; case Rarity.Epic: return "épica"; default: return "común"; }
        }
    }

    /// <summary>Reserva máxima de munición por tipo (pistola, escopeta, rifle).</summary>
    [Serializable]
    public class AmmoLimits
    {
        public int pistol = 48, shotgun = 20, rifle = 90;
        public int For(AmmoType t) => t == AmmoType.Pistol ? pistol : (t == AmmoType.Shotgun ? shotgun : (t == AmmoType.Rifle ? rifle : 0));
    }

    /// <summary>Munición que lleva el jugador, separada por tipo y con tope por tipo. Recoger más de lo que cabe NO consume el pickup.</summary>
    public class AmmoReserve
    {
        private readonly AmmoLimits _limits;
        private readonly Dictionary<AmmoType, int> _count = new Dictionary<AmmoType, int>();

        public AmmoReserve(AmmoLimits limits) { _limits = limits ?? new AmmoLimits(); }

        public int Count(AmmoType t) => t == AmmoType.None ? 0 : (_count.TryGetValue(t, out int c) ? c : 0);
        public int Max(AmmoType t) => _limits.For(t);
        public bool IsFull(AmmoType t) => t != AmmoType.None && Count(t) >= Max(t);
        public int Space(AmmoType t) => Math.Max(0, Max(t) - Count(t));

        /// <summary>Añade hasta el tope. Devuelve cuántas balas entraron (0 si estaba llena: el pickup no se consume).</summary>
        public int Add(AmmoType t, int amount)
        {
            if (t == AmmoType.None || amount <= 0) return 0;
            int accepted = Math.Min(amount, Space(t));
            if (accepted > 0) _count[t] = Count(t) + accepted;
            return accepted;
        }

        /// <summary>Saca hasta "n" balas (al recargar). Devuelve las que salieron.</summary>
        public int Take(AmmoType t, int n)
        {
            int taken = Math.Min(Math.Max(0, n), Count(t));
            if (taken > 0) _count[t] = Count(t) - taken;
            return taken;
        }
    }

    public class MedkitData
    {
        public MedkitKind Kind;
        public string Name;
        public float Heal;         // puntos de vida que cura al terminar
        public float UseSeconds;   // tiempo de uso (barra de curación)
        public int MaxCarry;       // cuántos puede llevar

        public static MedkitData For(MedkitKind k)
        {
            switch (k)
            {
                case MedkitKind.Bandage: return new MedkitData { Kind = k, Name = "Venda", Heal = 15f, UseSeconds = 1.2f, MaxCarry = 5 };
                case MedkitKind.Small: return new MedkitData { Kind = k, Name = "Botiquín pequeño", Heal = 30f, UseSeconds = 2.5f, MaxCarry = 3 };
                case MedkitKind.Medium: return new MedkitData { Kind = k, Name = "Botiquín mediano", Heal = 60f, UseSeconds = 4f, MaxCarry = 2 };
                default: return new MedkitData { Kind = k, Name = "Botiquín grande", Heal = 100f, UseSeconds = 6f, MaxCarry = 1 };
            }
        }
    }

    /// <summary>Vendas y botiquines que lleva el jugador, con tope por tipo.</summary>
    public class MedkitInventory
    {
        private readonly Dictionary<MedkitKind, int> _count = new Dictionary<MedkitKind, int>();
        public int Count(MedkitKind k) => _count.TryGetValue(k, out int c) ? c : 0;
        public bool IsFull(MedkitKind k) => Count(k) >= MedkitData.For(k).MaxCarry;

        /// <summary>Añade hasta el tope; devuelve cuántos entraron (0 = lleno, no se consume el pickup).</summary>
        public int Add(MedkitKind k, int n)
        {
            int accepted = Math.Min(Math.Max(0, n), MedkitData.For(k).MaxCarry - Count(k));
            if (accepted > 0) _count[k] = Count(k) + accepted;
            return accepted;
        }

        public bool TryTake(MedkitKind k)
        {
            if (Count(k) <= 0) return false;
            _count[k] = Count(k) - 1;
            return true;
        }

        /// <summary>El más conveniente para la vida que falta: el más pequeño que cura lo que falta; si ninguno alcanza, el más grande que haya.</summary>
        public MedkitKind? Best(float missingHealth)
        {
            MedkitKind? biggest = null;
            foreach (MedkitKind k in new[] { MedkitKind.Bandage, MedkitKind.Small, MedkitKind.Medium, MedkitKind.Large })
            {
                if (Count(k) <= 0) continue;
                biggest = k;
                if (MedkitData.For(k).Heal >= missingHealth * 0.8f) return k;
            }
            return biggest;
        }
    }

    /// <summary>Sesión de curación con barra de progreso: la vida se recupera al terminar, y se cancela (sin gastar el botiquín) si se recibe daño.</summary>
    public class HealingSession
    {
        public MedkitData Item { get; private set; }
        public bool Active { get; private set; }
        public float Elapsed { get; private set; }
        public float Progress => Active && Item != null ? Math.Min(1f, Elapsed / Math.Max(0.01f, Item.UseSeconds)) : 0f;

        public static bool CanStart(float health, float maxHealth) => health > 0f && health < maxHealth - 0.01f;

        public bool Start(MedkitData item, float health, float maxHealth)
        {
            if (Active || item == null || !CanStart(health, maxHealth)) return false;
            Item = item; Elapsed = 0f; Active = true;
            return true;
        }

        /// <summary>Avanza el reloj. Devuelve los puntos de vida a curar (solo al terminar, sin pasar de la vida máxima); 0 mientras tanto.</summary>
        public float Tick(float dt, float health, float maxHealth)
        {
            if (!Active) return 0f;
            Elapsed += dt;
            if (Elapsed < Item.UseSeconds) return 0f;
            Active = false;
            return Math.Max(0f, Math.Min(Item.Heal, maxHealth - health));
        }

        /// <summary>Cancela (por daño recibido o a mano). El botiquín no se gasta.</summary>
        public void Cancel() { Active = false; Elapsed = 0f; }
    }
}

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

        // --- manejo (Bloques de cámara y combate: retroceso, dispersión, apuntado, sway). Ver docs/BALANCE.md.
        public int dataVersion;                // versión de los datos: un asset más viejo que CurrentDataVersion se actualiza solo (editor)
        public string[] fireModes = new string[0]; // auto | semi | burst | pump | bolt; el primero es el inicial (X cambia entre ellos)
        public int burstCount = 3;
        public float recoilHorizontal = 0.3f;  // desviación lateral máxima por disparo (grados, aleatoria y acotada)
        public float recoilRecovery = 9f;      // grados por segundo que se recuperan al soltar el gatillo
        public float recoilMaxPitch = 8f;      // retroceso vertical acumulado máximo (grados)
        public float recoilAimMultiplier = 0.6f, recoilCrouchMultiplier = 0.8f;
        public float spreadPerShot = 0.35f;    // grados que crece la dispersión por disparo (ráfaga)
        public float spreadMax = 6f;           // dispersión máxima total (grados)
        public float spreadRecovery = 6f;      // grados por segundo que se recuperan
        public float spreadMove = 1.2f;        // grados extra corriendo a toda velocidad (proporcional a la velocidad)
        public float spreadAir = 2f;           // grados extra en el aire
        public float spreadCrouchMultiplier = 0.75f;
        public float aimFov = 42f;             // campo de visión al apuntar
        public float aimDistance = 1.7f;       // distancia de la cámara al apuntar (tercera persona)
        public float aimSensitivity = 0.6f;    // multiplicador de sensibilidad al apuntar
        public float adsTime = 0.18f;          // segundos para entrar/salir del apuntado (ease in/out)
        public bool aimFirstPerson;            // al apuntar, la cámara pasa detrás de la mira (SightPoint)
        public float[] scopeFovs = new float[0]; // con mira telescópica: FOV de cada nivel de zoom (rueda del ratón)
        public float swayAmount = 1f, bobAmount = 1f;
        public float kickback = 0.06f;         // metros que el arma retrocede visualmente por disparo
        public float kickbackPitch = 4f;       // grados que se levanta visualmente
        public float fireShake = 0.05f;        // impulso de cámara por disparo
        public bool ejectsCasings = true;
        // --- agarre (puntos para las manos y la culata; ver WeaponGrip)
        public WeaponGrip grip;

        public const int CurrentDataVersion = 4;

        public bool IsMelee => kind == WeaponKind.Melee;
        public bool HasScope => scopeFovs != null && scopeFovs.Length > 0;
        /// <summary>Modo de disparo inicial (si no hay lista: "auto" o "semi" según "automatic").</summary>
        public string DefaultFireMode => fireModes != null && fireModes.Length > 0 ? fireModes[0] : (automatic ? "auto" : "semi");
        public bool UsesAmmo => ammoType != AmmoType.None;
        public float SecondsBetweenShots => 60f / Math.Max(1f, roundsPerMinute);
        public float DamagePerShot => damage * Math.Max(1, pellets);
        /// <summary>Daño por segundo teórico (sin recargas), para equilibrar.</summary>
        public float Dps => DamagePerShot * roundsPerMinute / 60f;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

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
            burstCount = Math.Max(2, Math.Min(6, burstCount));
            recoilHorizontal = Clamp(recoilHorizontal, 0f, 3f);
            recoilRecovery = Clamp(recoilRecovery, 0.5f, 60f);
            recoilMaxPitch = Clamp(recoilMaxPitch, 0.5f, 25f);
            recoilAimMultiplier = Clamp(recoilAimMultiplier, 0.1f, 1f);
            recoilCrouchMultiplier = Clamp(recoilCrouchMultiplier, 0.1f, 1f);
            spreadPerShot = Clamp(spreadPerShot, 0f, 5f);
            spreadMax = Math.Max(spreadHip, Clamp(spreadMax, 0f, 20f));
            spreadRecovery = Clamp(spreadRecovery, 0.1f, 60f);
            spreadMove = Clamp(spreadMove, 0f, 10f);
            spreadAir = Clamp(spreadAir, 0f, 10f);
            spreadCrouchMultiplier = Clamp(spreadCrouchMultiplier, 0.2f, 1f);
            aimFov = Clamp(aimFov, 5f, 70f);
            aimDistance = Clamp(aimDistance, 0.5f, 4f);
            aimSensitivity = Clamp(aimSensitivity, 0.05f, 1.5f);
            adsTime = Clamp(adsTime, 0.05f, 0.8f);
            swayAmount = Clamp(swayAmount, 0f, 3f); bobAmount = Clamp(bobAmount, 0f, 3f);
            kickback = Clamp(kickback, 0f, 0.3f); kickbackPitch = Clamp(kickbackPitch, 0f, 25f);
            fireShake = Clamp(fireShake, 0f, 1f);
            if (scopeFovs != null) for (int i = 0; i < scopeFovs.Length; i++) scopeFovs[i] = Clamp(scopeFovs[i], 2f, 60f);
            if (grip == null || grip.grip == null || grip.grip.Length < 6 || grip.foregrip == null || grip.foregrip.Length < 6 || grip.stock == null || grip.stock.Length < 3) grip = WeaponGrip.For(kind, length, thickness);
            if (fireModes != null) foreach (var m in fireModes) if (Array.IndexOf(FireModes.All, m) < 0) { fireModes = new string[0]; break; }
        }
    }

    public static class FireModes
    {
        public const string Auto = "auto", Semi = "semi", Burst = "burst", Pump = "pump", Bolt = "bolt";
        public static readonly string[] All = { Auto, Semi, Burst, Pump, Bolt };
        public static string Label(string m) => m == Auto ? "AUTO" : m == Burst ? "RÁFAGA" : m == Pump ? "BOMBEO" : m == Bolt ? "CERROJO" : "SEMI";
    }

    /// <summary>Las 7 armas por defecto. Sus ScriptableObjects se crean desde estos datos y el JSON puede reemplazarlos.</summary>
    public static class WeaponCatalog
    {
        public static List<WeaponData> Defaults() => WithHandling(new List<WeaponData>
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
        });

        /// <summary>
        /// Valores de manejo de cada arma (retroceso, dispersión, apuntado, sway, modos de disparo). Separados para que la tabla de daño de arriba siga legible.
        /// </summary>
        private static List<WeaponData> WithHandling(List<WeaponData> list)
        {
            foreach (var w in list)
            {
                w.dataVersion = WeaponData.CurrentDataVersion;
                w.grip = WeaponGrip.For(w.kind, w.length, w.thickness);
                switch (w.id)
                {
                    case "pistol":
                        Set(w, new[] { FireModes.Semi }, rh: 0.35f, rr: 10f, rmax: 6f, sps: 0.45f, smax: 5f, srec: 7f, smove: 1.0f, fov: 46f, dist: 1.8f, sens: 0.65f, ads: 0.15f, sway: 0.8f, bob: 0.8f, kb: 0.05f, kbp: 6f, shake: 0.04f); break;
                    case "revolver":
                        Set(w, new[] { FireModes.Semi }, rh: 0.6f, rr: 7f, rmax: 9f, sps: 0.9f, smax: 6f, srec: 5f, smove: 1.2f, fov: 42f, dist: 1.7f, sens: 0.6f, ads: 0.18f, sway: 1f, bob: 0.9f, kb: 0.08f, kbp: 12f, shake: 0.09f); break;
                    case "smg":
                        Set(w, new[] { FireModes.Auto, FireModes.Burst }, rh: 0.45f, rr: 12f, rmax: 6f, sps: 0.22f, smax: 7f, srec: 9f, smove: 0.8f, fov: 48f, dist: 1.9f, sens: 0.7f, ads: 0.14f, sway: 0.9f, bob: 0.9f, kb: 0.03f, kbp: 2.5f, shake: 0.025f); break;
                    case "shotgun":
                        Set(w, new[] { FireModes.Pump }, rh: 0.8f, rr: 6f, rmax: 10f, sps: 0.6f, smax: 8f, srec: 4f, smove: 1.5f, fov: 50f, dist: 1.9f, sens: 0.7f, ads: 0.2f, sway: 1.2f, bob: 1.1f, kb: 0.12f, kbp: 14f, shake: 0.14f); break;
                    case "assault_rifle":
                        Set(w, new[] { FireModes.Auto, FireModes.Burst, FireModes.Semi }, rh: 0.3f, rr: 10f, rmax: 7f, sps: 0.18f, smax: 5.5f, srec: 8f, smove: 1.4f, fov: 40f, dist: 1.6f, sens: 0.55f, ads: 0.2f, sway: 1.1f, bob: 1.1f, kb: 0.04f, kbp: 3.5f, shake: 0.035f); break;
                    case "sniper":
                        w.range = 400f; w.falloffStart = 0.8f; w.minDamageFraction = 1f;   // más alcance real y sin caída de daño
                        Set(w, new[] { FireModes.Bolt }, rh: 0.4f, rr: 5f, rmax: 10f, sps: 1.5f, smax: 8f, srec: 3f, smove: 3f, fov: 30f, dist: 1.4f, sens: 0.35f, ads: 0.28f, sway: 1.4f, bob: 1.3f, kb: 0.1f, kbp: 9f, shake: 0.12f);
                        w.aimFirstPerson = true;
                        w.scopeFovs = new[] { 20f, 12f, 7.5f, 5f };   // x3, x5, x8, x12 (rueda del ratón mientras apuntas)
                        w.spreadAir = 6f;
                        break;
                    case "bar":
                        Set(w, new[] { FireModes.Semi }, rh: 0f, rr: 20f, rmax: 2f, sps: 0f, smax: 0f, srec: 10f, smove: 0f, fov: 60f, dist: 3f, sens: 1f, ads: 0.15f, sway: 1.3f, bob: 1.2f, kb: 0f, kbp: 0f, shake: 0.06f);
                        w.ejectsCasings = false; w.spreadAir = 0f;
                        break;
                }
                w.spreadMax = Math.Max(w.spreadMax, w.spreadHip);
            }
            return list;
        }

        /// <summary>
        /// Actualiza los datos de un asset de una versión anterior SIN perder lo que se editó a mano: solo cambia lo que trae la versión nueva.
        /// v2 -> v3: francotirador con alcance 400 m, sin caída de daño y zoom x3/x5/x8/x12. Devuelve false si es demasiado viejo (v0-1: usar los valores por defecto).
        /// </summary>
        public static bool Migrate(WeaponData w)
        {
            if (w == null || w.dataVersion < 2) return false;
            if (w.dataVersion < 3 && w.id == "sniper")
            {
                w.range = Math.Max(w.range, 400f);
                w.falloffStart = Math.Max(w.falloffStart, 0.8f);
                w.minDamageFraction = 1f;
                w.scopeFovs = new[] { 20f, 12f, 7.5f, 5f };
            }
            // v3 -> v4: puntos de agarre (empuñadura, guardamano, culata) según el tipo de arma
            if (w.dataVersion < 4 && (w.grip == null || w.grip.grip == null || w.grip.grip.Length < 6)) w.grip = WeaponGrip.For(w.kind, w.length, w.thickness);
            w.dataVersion = WeaponData.CurrentDataVersion;
            return true;
        }

        private static void Set(WeaponData w, string[] modes, float rh, float rr, float rmax, float sps, float smax, float srec, float smove, float fov, float dist, float sens, float ads,
            float sway, float bob, float kb, float kbp, float shake)
        {
            w.fireModes = modes; w.recoilHorizontal = rh; w.recoilRecovery = rr; w.recoilMaxPitch = rmax;
            w.spreadPerShot = sps; w.spreadMax = smax; w.spreadRecovery = srec; w.spreadMove = smove;
            w.aimFov = fov; w.aimDistance = dist; w.aimSensitivity = sens; w.adsTime = ads;
            w.swayAmount = sway; w.bobAmount = bob; w.kickback = kb; w.kickbackPitch = kbp; w.fireShake = shake;
        }

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

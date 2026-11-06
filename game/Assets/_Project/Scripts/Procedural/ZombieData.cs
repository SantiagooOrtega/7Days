using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public static class ZombieIds
    {
        public const string Common = "common", Runner = "runner", Tank = "tank", Armored = "armored", Spitter = "spitter", Explosive = "explosive";
        public const string BossBrute = "boss_brute", BossLauncher = "boss_launcher", BossFinal = "boss_final";
        public static readonly string[] All = { Common, Runner, Tank, Armored, Spitter, Explosive, BossBrute, BossLauncher, BossFinal };
        public static readonly string[] Bosses = { BossBrute, BossLauncher, BossFinal };
        /// <summary>Tipos de las tablas de botín (LootRules.Zombies): a cuál pertenece cada zombie.</summary>
        public static readonly string[] DropKinds = { "common", "runner", "armored", "tank", "explosive", "brute", "spitter", "boss" };
    }

    /// <summary>Habilidad de un jefe: cuándo puede usarse, cuánto avisa antes (telegraph) y cuánto espera para repetirse.</summary>
    [Serializable]
    public class BossAbilityData
    {
        public string id;                 // charge | slam | throw | summon | shockwave | summon_wave
        public float cooldown = 8f;
        public float telegraph = 1.2f;    // segundos de aviso visible y audible antes de ejecutarla
        public float minRange = 0f, maxRange = 30f;
        public int minPhase = 0;          // 0 normal, 1 furia al 50%, 2 furia al 25%
        public float weight = 1f;
        public float a, b, c;             // parámetros propios (velocidad de la carga, radio, cantidad...)
    }

    [Serializable]
    public class BossData
    {
        public int day = 3;
        public float phase2At = 0.5f, phase3At = 0.25f;                 // furia al bajar del 50% y del 25%
        public float[] furySpeed = { 1f, 1.25f, 1.5f };                   // multiplicador de velocidad por fase
        public float[] furyAttackRate = { 1f, 1.3f, 1.6f };               // multiplicador de frecuencia de ataques por fase
        public float globalCooldown = 1.2f;                               // pausa mínima entre habilidades
        public BossAbilityData[] abilities = new BossAbilityData[0];
        public bool hasWeakPoint;                                         // puntos débiles que solo se exponen en ciertos momentos
        public float weakPointExposeSeconds = 4f, weakPointPeriod = 14f;
        public float stunnedExtraDamage = 1.5f;                           // ventana de daño extra tras chocar contra un muro
        public float wallStunSeconds = 2.5f;
        public string summonType = ZombieIds.Runner;
        public int summonCount = 3;
    }

    /// <summary>
    /// Un tipo de zombie en datos (ScriptableObject o JSON, validable con zod): vida, velocidad, daño, resistencias, multiplicadores por zona, aturdimiento,
    /// tabla de drops, comportamiento especial y, para los jefes, sus habilidades y fases.
    /// </summary>
    [Serializable]
    public class ZombieTypeData
    {
        public string id;
        public string displayName;
        public float maxHealth = 100f;
        public float speed = 2.2f;          // al perseguir
        public float patrolSpeed = 1.0f;
        public float attackDamage = 12f;
        public float attackRange = 1.5f;
        public float attackCooldown = 1.2f;
        public float aggroRange = 18f;
        public float resistance = 0f;        // 0..0.9 general (se aplica a todas las zonas)
        public float stunResistance = 0f;    // 0..1 (los jefes ~0.9)
        public ZoneMultipliers zones = new ZoneMultipliers();
        public float scale = 1f;
        public bool canEnterBuildings = true;   // false: usa un agente de NavMesh grande que no cabe por puertas y ventanas
        public float navRadius = 0f;            // radio del agente; 0 = automático (0.35 x escala). Los que no entran a edificios necesitan >= 0.7
        public float[] color = { 0.4f, 0.5f, 0.35f };
        public string dropKind = "common";   // tabla de drops (LootRules.Zombies)
        // explosivo
        public float explosionRadius = 0f, explosionDamage = 0f;
        public float fuseTriggerRadius = 3f;     // al acercarse tanto el jugador se detiene y arranca la mecha
        public float fuseCancelRadius = 5.5f;    // si el jugador se aleja más de esto antes de que acabe la mecha, se cancela
        public float fuseSeconds = 1.35f;
        public float explosionKnockback = 9f;    // velocidad de empuje en el centro (m/s)
        public float explosionBlockedFactor = 0.25f; // fracción del daño que pasa a través de un muro sólido
        public bool explosionBreaksCover = true; // rompe coberturas ligeras (Destructible de fuerza 0)
        // lanzador: "none" | "direct" (línea recta, la frena la cobertura) | "arc" (cae sobre el jugador, ignora la cobertura frontal) | "mixed" (directo, y en arco si el jugador se cubre)
        public string projectile = "none";
        public float flankAfterSeconds = 4f;   // segundos del jugador en cobertura tras los que el lanzador busca un ángulo lateral
        public string projectileKind = "debris"; // debris | rod | acid | grenade (solo cambia el aspecto)
        public float projectileDamage = 0f, projectileSpeed = 14f, projectileCooldown = 3.5f, preferredRange = 12f, areaRadius = 1.2f;
        public bool isBoss;
        public BossData boss = new BossData();

        public ZombieTypeData Clone()
        {
            var c = (ZombieTypeData)MemberwiseClone();
            c.zones = zones.Clone();
            return c;
        }
    }

    public static class ZombieCatalog
    {
        public const float CommonHealth = 100f;

        public static List<ZombieTypeData> Defaults()
        {
            var list = new List<ZombieTypeData>();
            var common = new ZombieTypeData { id = ZombieIds.Common, displayName = "Zombi", maxHealth = 100f, speed = 2.4f, attackDamage = 12f, color = new[] { 0.42f, 0.55f, 0.38f }, dropKind = "common" };
            list.Add(common);
            list.Add(new ZombieTypeData { id = ZombieIds.Runner, displayName = "Corredor", maxHealth = 55f, speed = 5.2f, patrolSpeed = 1.6f, attackDamage = 9f, attackCooldown = 0.9f, aggroRange = 22f, color = new[] { 0.62f, 0.5f, 0.3f }, scale = 0.95f, dropKind = "runner",
                zones = new ZoneMultipliers { head = 2.5f, torso = 1f, arms = 0.6f, legs = 0.5f } });
            list.Add(new ZombieTypeData { id = ZombieIds.Tank, displayName = "Tanque", maxHealth = 400f, speed = 1.5f, patrolSpeed = 0.7f, attackDamage = 32f, attackCooldown = 1.9f, attackRange = 1.9f, resistance = 0.35f, stunResistance = 0.7f, scale = 1.45f,
                color = new[] { 0.35f, 0.38f, 0.3f }, canEnterBuildings = false, navRadius = 0.8f, dropKind = "tank", zones = new ZoneMultipliers { head = 2.0f, torso = 0.85f, arms = 0.5f, legs = 0.6f } });
            list.Add(new ZombieTypeData { id = ZombieIds.Armored, displayName = "Blindado", maxHealth = 160f, speed = 1.9f, attackDamage = 18f, color = new[] { 0.45f, 0.45f, 0.5f }, dropKind = "armored", resistance = 0.1f, stunResistance = 0.3f,
                zones = new ZoneMultipliers { head = 2.5f, torso = 0.35f, arms = 0.5f, legs = 0.6f } }); // el casco no protege: la cabeza sigue vulnerable
            list.Add(new ZombieTypeData { id = ZombieIds.Spitter, displayName = "Lanzador", maxHealth = 80f, speed = 2.0f, attackDamage = 8f, attackRange = 1.4f, color = new[] { 0.5f, 0.6f, 0.25f }, dropKind = "spitter",
                projectile = "mixed", projectileDamage = 14f, projectileSpeed = 15f, projectileCooldown = 3.2f, preferredRange = 12f, areaRadius = 1.3f });
            list.Add(new ZombieTypeData { id = ZombieIds.Explosive, displayName = "Explosivo", maxHealth = 70f, speed = 3.0f, attackDamage = 10f, color = new[] { 0.7f, 0.35f, 0.15f }, dropKind = "explosive",
                explosionRadius = 3.6f, explosionDamage = 45f });

            list.Add(new ZombieTypeData
            {
                id = ZombieIds.BossBrute, displayName = "Bruto Titán", maxHealth = 1500f, speed = 3.0f, attackDamage = 40f, attackCooldown = 1.6f, attackRange = 2.4f, aggroRange = 60f,
                resistance = 0.2f, stunResistance = 0.9f, scale = 2.2f, color = new[] { 0.45f, 0.22f, 0.2f }, dropKind = "boss", isBoss = true,
                zones = new ZoneMultipliers { head = 1.8f, torso = 1f, arms = 0.5f, legs = 0.6f },
                boss = new BossData { day = 3, wallStunSeconds = 2.5f, stunnedExtraDamage = 1.5f, abilities = new[] {
                    new BossAbilityData { id = "charge", cooldown = 9f, telegraph = 1.3f, minRange = 6f, maxRange = 28f, weight = 2f, a = 15f, b = 1.1f },
                    new BossAbilityData { id = "slam", cooldown = 6f, telegraph = 0.9f, minRange = 0f, maxRange = 4f, weight = 1.5f, a = 3.2f, b = 28f },
                } },
            });
            list.Add(new ZombieTypeData
            {
                id = ZombieIds.BossLauncher, displayName = "Lanzador Colosal", maxHealth = 2400f, speed = 2.4f, attackDamage = 35f, attackCooldown = 1.8f, aggroRange = 70f, preferredRange = 16f,
                resistance = 0.25f, stunResistance = 0.9f, scale = 2.0f, color = new[] { 0.35f, 0.45f, 0.22f }, dropKind = "boss", isBoss = true,
                zones = new ZoneMultipliers { head = 1.8f, torso = 1f, arms = 0.5f, legs = 0.6f },
                boss = new BossData { day = 5, summonType = ZombieIds.Runner, summonCount = 3, abilities = new[] {
                    new BossAbilityData { id = "throw", cooldown = 7f, telegraph = 1.4f, minRange = 6f, maxRange = 38f, weight = 2.5f, a = 2.4f, b = 45f },
                    new BossAbilityData { id = "summon", cooldown = 18f, telegraph = 1.0f, minRange = 0f, maxRange = 60f, weight = 1f },
                    new BossAbilityData { id = "slam", cooldown = 7f, telegraph = 0.9f, minRange = 0f, maxRange = 4f, weight = 1f, a = 3f, b = 25f },
                } },
            });
            list.Add(new ZombieTypeData
            {
                id = ZombieIds.BossFinal, displayName = "El Devorador", maxHealth = 3000f, speed = 3.2f, attackDamage = 45f, attackCooldown = 1.4f, aggroRange = 80f,
                resistance = 0.3f, stunResistance = 0.95f, scale = 2.4f, color = new[] { 0.3f, 0.12f, 0.35f }, dropKind = "boss", isBoss = true,
                zones = new ZoneMultipliers { head = 1.6f, torso = 0.9f, arms = 0.5f, legs = 0.6f, weakPoint = 4f },
                boss = new BossData { day = 7, hasWeakPoint = true, weakPointExposeSeconds = 4f, weakPointPeriod = 14f, summonType = ZombieIds.Common, summonCount = 4, abilities = new[] {
                    new BossAbilityData { id = "shockwave", cooldown = 10f, telegraph = 1.6f, minRange = 0f, maxRange = 16f, weight = 2f, a = 11f, b = 38f },
                    new BossAbilityData { id = "summon_wave", cooldown = 20f, telegraph = 1.2f, minRange = 0f, maxRange = 80f, weight = 1f },
                    new BossAbilityData { id = "slam", cooldown = 6f, telegraph = 0.9f, minRange = 0f, maxRange = 4.5f, weight = 1.5f, a = 3.6f, b = 34f },
                } },
            });
            return list;
        }

        public static ZombieTypeData Get(string id)
        {
            foreach (var z in Defaults()) if (z.id == id) return z;
            return null;
        }

        /// <summary>Jefe del día (3, 5 o 7); null si ese día no hay jefe.</summary>
        public static ZombieTypeData BossForDay(int day)
        {
            foreach (var z in Defaults()) if (z.isBoss && z.boss.day == day) return z;
            return null;
        }
    }

    /// <summary>Archivo de tipos de zombie (Resources/Zombies/zombies.json y el esquema zod de docs/ZOMBIES.md).</summary>
    [Serializable]
    public class ZombieFile
    {
        public List<ZombieTypeData> types = new List<ZombieTypeData>();
    }

    public class ZombieValidation
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>Valida los tipos de zombie (JSON de fuera): rangos, multiplicadores, drops, habilidades de jefe y que los jefes tengan entre 10 y 30 veces la vida de un común.</summary>
    public static class ZombieValidator
    {
        private static readonly string[] AbilityIds = { "charge", "slam", "throw", "summon", "shockwave", "summon_wave" };
        /// <summary>Con 2 x 0.7 = 1.4 m no cabe por las puertas de la casa (1.1–1.2 m de ancho libre).</summary>
        public const float GiantMinRadius = 0.7f;
        private static readonly string[] ProjectileKinds = { "debris", "rod", "acid", "grenade" };
        private static readonly string[] Projectiles = { "none", "direct", "arc", "mixed" };

        public static ZombieValidation Validate(IList<ZombieTypeData> types)
        {
            var v = new ZombieValidation();
            if (types == null || types.Count == 0) { v.Errors.Add("no hay tipos de zombie"); return v; }
            var ids = new HashSet<string>();
            foreach (var z in types)
            {
                if (z == null || string.IsNullOrEmpty(z.id) || !ids.Add(z.id)) { v.Errors.Add("tipo sin id o repetido"); continue; }
                string n = z.id + ": ";
                if (z.maxHealth < 1f || z.maxHealth > 20000f) v.Errors.Add(n + "maxHealth fuera de 1..20000");
                if (z.speed < 0.1f || z.speed > 12f) v.Errors.Add(n + "speed fuera de 0.1..12");
                if (z.attackDamage < 0f || z.attackDamage > 200f) v.Errors.Add(n + "attackDamage fuera de 0..200");
                if (z.attackRange < 0.5f || z.attackRange > 8f) v.Errors.Add(n + "attackRange fuera de 0.5..8");
                if (z.attackCooldown < 0.2f || z.attackCooldown > 10f) v.Errors.Add(n + "attackCooldown fuera de 0.2..10");
                if (z.resistance < 0f || z.resistance > 0.9f) v.Errors.Add(n + "resistance fuera de 0..0.9");
                if (z.stunResistance < 0f || z.stunResistance > 1f) v.Errors.Add(n + "stunResistance fuera de 0..1");
                if (z.scale < 0.3f || z.scale > 4f) v.Errors.Add(n + "scale fuera de 0.3..4");
                if (z.zones == null || z.zones.head <= 0f || z.zones.torso <= 0f || z.zones.arms <= 0f || z.zones.legs <= 0f || z.zones.weakPoint <= 0f || z.zones.head > 6f || z.zones.torso > 3f || z.zones.weakPoint > 10f)
                    v.Errors.Add(n + "multiplicadores de zona inválidos");
                if (Array.IndexOf(ZombieIds.DropKinds, z.dropKind) < 0) v.Errors.Add(n + "dropKind desconocido '" + z.dropKind + "'");
                if (Array.IndexOf(Projectiles, z.projectile) < 0) v.Errors.Add(n + "projectile desconocido '" + z.projectile + "'");
                if (z.projectile != "none" && (z.projectileDamage < 0f || z.projectileCooldown < 0.5f || z.projectileSpeed < 2f || z.preferredRange < 3f || z.flankAfterSeconds < 0.5f)) v.Errors.Add(n + "proyectil con daño, velocidad, rango, flanqueo o cooldown inválido");
                if (Array.IndexOf(ProjectileKinds, z.projectileKind) < 0) v.Errors.Add(n + "projectileKind desconocido '" + z.projectileKind + "'");
                if (z.explosionRadius < 0f || z.explosionRadius > 12f || z.explosionDamage < 0f) v.Errors.Add(n + "explosión inválida");
                if (z.explosionRadius > 0f)
                {
                    if (z.fuseSeconds < 0.5f || z.fuseSeconds > 3f) v.Errors.Add(n + "fuseSeconds fuera de 0.5..3");
                    if (z.fuseTriggerRadius < 1f || z.fuseTriggerRadius > 8f) v.Errors.Add(n + "fuseTriggerRadius fuera de 1..8");
                    if (z.fuseCancelRadius < z.fuseTriggerRadius + 1f) v.Errors.Add(n + "fuseCancelRadius debe superar al de activación en al menos 1 m");
                    if (z.explosionBlockedFactor < 0f || z.explosionBlockedFactor > 1f) v.Errors.Add(n + "explosionBlockedFactor fuera de 0..1");
                    if (z.explosionKnockback < 0f || z.explosionKnockback > 30f) v.Errors.Add(n + "explosionKnockback fuera de 0..30");
                }
                if (z.navRadius < 0f || z.navRadius > 2f) v.Errors.Add(n + "navRadius fuera de 0..2");
                if (!z.canEnterBuildings && z.navRadius < GiantMinRadius) v.Errors.Add(n + "un zombi que no entra a edificios necesita navRadius >= " + GiantMinRadius + " (puertas de 1.1–1.2 m)");
                if (z.color == null || z.color.Length != 3) v.Errors.Add(n + "color debe tener 3 valores");
                if (z.isBoss) ValidateBoss(z, v, n);
            }
            return v;
        }

        private static void ValidateBoss(ZombieTypeData z, ZombieValidation v, string n)
        {
            var b = z.boss;
            if (b == null) { v.Errors.Add(n + "jefe sin datos de jefe"); return; }
            double ratio = z.maxHealth / ZombieCatalog.CommonHealth;
            if (ratio < 10 || ratio > 30) v.Errors.Add(n + "un jefe debe tener entre 10 y 30 veces la vida de un común (tiene " + ratio.ToString("0.0") + "x)");
            if (z.stunResistance < 0.8f) v.Errors.Add(n + "un jefe debe resistir el aturdimiento (>= 0.8)");
            if (b.day < 1 || b.day > 7) v.Errors.Add(n + "day fuera de 1..7");
            if (b.phase2At <= b.phase3At || b.phase2At >= 1f || b.phase3At <= 0f) v.Errors.Add(n + "fases de furia inválidas (phase2At > phase3At, ambas entre 0 y 1)");
            if (b.furySpeed == null || b.furySpeed.Length != 3 || b.furyAttackRate == null || b.furyAttackRate.Length != 3) v.Errors.Add(n + "furySpeed y furyAttackRate necesitan 3 valores");
            else for (int i = 1; i < 3; i++) if (b.furySpeed[i] < b.furySpeed[i - 1] || b.furyAttackRate[i] < b.furyAttackRate[i - 1]) v.Errors.Add(n + "la furia no puede bajar la velocidad ni los ataques");
            if (b.abilities == null || b.abilities.Length == 0) { v.Errors.Add(n + "sin habilidades"); return; }
            foreach (var a in b.abilities)
            {
                if (a == null || Array.IndexOf(AbilityIds, a.id) < 0) { v.Errors.Add(n + "habilidad desconocida '" + a?.id + "'"); continue; }
                if (a.cooldown < 1f) v.Errors.Add(n + a.id + ": cooldown < 1 s");
                if (a.telegraph < 0.5f) v.Errors.Add(n + a.id + ": telegraph < 0.5 s (injusto: el jugador necesita poder reaccionar)");
                if (a.minRange > a.maxRange) v.Errors.Add(n + a.id + ": minRange > maxRange");
                if (a.weight <= 0f) v.Errors.Add(n + a.id + ": weight <= 0");
            }
            if (Array.IndexOf(ZombieIds.All, b.summonType) < 0) v.Errors.Add(n + "summonType desconocido");
            if (z.dropKind != "boss") v.Errors.Add(n + "un jefe debe usar la tabla de drops 'boss' (suelta algo raro garantizado)");
        }
    }
}

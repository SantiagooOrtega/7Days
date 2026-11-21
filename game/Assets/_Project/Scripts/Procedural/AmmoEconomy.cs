using System;
using System.Collections.Generic;
using System.Linq;
using SevenDays.Player;

namespace SevenDays.Procedural
{
    /// <summary>Munición que entra en un día (en balas; un cartucho de escopeta cuenta como una bala).</summary>
    public struct DayAmmo
    {
        public int Day, Zombies, Budget;
        public double Refill;      // relleno de contenedores y puntos al empezar el día (valor esperado)
        public double TableDrops;  // drops normales de las tablas (valor esperado, con el tope diario)
        public double TopUp;       // lo que pone la economía para llegar al presupuesto
        public double Trickle;     // parte del TopUp que reaparece en tandas durante la oleada
        public double KillCredit;  // parte del TopUp que sueltan los zombis al morir (crédito por baja)
        public double Total => Refill + TableDrops + TopUp;
        public double Ratio => Zombies <= 0 ? 0 : Total / Zombies;
    }

    /// <summary>
    /// Economía de munición por día (pura): presupuesto = balas por zombi x zombis de la oleada, con 4.5 balas por zombi el día 1 bajando a 3 el día 7 (generosa
    /// tras probar: se puede pelear a tiros; botiquines y armas siguen escasos). Las fuentes "naturales" (relleno de contenedores y drops de las tablas) se cuentan primero y la
    /// economía completa el resto: 40 % en tandas durante la oleada y 60 % como drops de munición de los zombis. No cuenta el botín inicial del mapa ni la munición de salida.
    /// </summary>
    public static class AmmoEconomy
    {
        public const float RatioDay1 = 5.2f, RatioDay7 = 3.6f;   // 4.5 -> 3.0 antes: más suministro tras probar
        public const double TrickleShare = 0.4;
        public const int TrickleBatches = 4;
        public const float TrickleFirstSeconds = 20f, TrickleIntervalSeconds = 25f;
        /// <summary>Un punto donde reapareció botín no se vuelve a usar para las tandas hasta pasados estos segundos.</summary>
        public const float PointCooldownSeconds = 60f;
        /// <summary>Relleno entre días: más cantidad que antes (x1.3) y más reapariciones por punto (+3).</summary>
        public const float RefillScale = 1.3f;   // 1.5 antes; con los pickups x2.5 el relleno ya pasaba del objetivo
        public const int ExtraRespawns = 3;
        /// <summary>Sin munición en ninguna arma durante este tiempo, el director deja un drop cerca.</summary>
        public const float PitySeconds = 60f;

        public static float TargetRatio(int day)
        {
            day = Math.Max(1, Math.Min(DayPlans.Days, day));
            return RatioDay1 + (RatioDay7 - RatioDay1) * (day - 1) / (DayPlans.Days - 1f);
        }

        /// <summary>Balas de munición esperadas de una tirada de la tabla.</summary>
        public static double AmmoRounds(ScarcityReport r) => r.PistolAmmo + r.ShotgunAmmo + r.RifleAmmo;

        /// <summary>Balas esperadas de los drops normales de las tablas en un día (reparto de tipos del día, con el tope diario de drops).</summary>
        public static double ExpectedTableDropRounds(LootLayoutData l, int day, int zombies = -1)
        {
            var plan = DayPlans.For(day);
            if (zombies >= 0) plan.totalZombies = zombies;
            var tuning = DayPlans.ApplyUnlocks(DirectorTuning.ForDay(day), day);
            double drops = 0, rounds = 0;
            foreach (var id in tuning.typeIds)
            {
                var type = ZombieCatalog.Get(id);
                var rule = l.Drop(type != null ? type.dropKind : id);
                if (rule == null) continue;
                double n = tuning.Proportion(id) * plan.totalZombies * (rule.guaranteed ? 1.0 : rule.chance);
                drops += n;
                rounds += n * AmmoRounds(ScarcityReport.Expected(l, rule.table, (Rarity)rule.minRarity));
            }
            if (drops > l.maxDropsPerDay && drops > 0) rounds *= l.maxDropsPerDay / drops;
            return rounds;
        }

        /// <summary>Balas esperadas de unos puntos que reaparecen (sus tablas).</summary>
        public static double RefillRounds(LootLayoutData l, IEnumerable<LootPoint> points) => points.Sum(p => AmmoRounds(ScarcityReport.Expected(l, p.table)));

        /// <summary>Plan de un día con el relleno que de verdad hubo (refillRounds).</summary>
        public static DayAmmo Plan(LootLayoutData l, int day, double refillRounds)
        {
            var d = new DayAmmo { Day = day, Zombies = DayPlans.For(day).totalZombies };
            d.Budget = (int)Math.Round(TargetRatio(day) * d.Zombies);
            d.Refill = refillRounds;
            d.TableDrops = ExpectedTableDropRounds(l, day);
            d.TopUp = Math.Max(0, d.Budget - d.Refill - d.TableDrops);
            d.Trickle = d.TopUp * TrickleShare;
            d.KillCredit = d.TopUp - d.Trickle;
            return d;
        }

        /// <summary>
        /// Simula los 7 días con el estado real del botín, en el peor caso de relleno (el jugador vacía todos los puntos cada día): lo usa la prueba de la economía.
        /// </summary>
        public static List<DayAmmo> Simulate(LootLayoutData l, int seed = 7)
        {
            var state = new LootDirectorState(l) { RespawnScale = RefillScale, ExtraRespawns = ExtraRespawns };
            var rng = new Random(seed);
            var result = new List<DayAmmo>();
            for (int day = 1; day <= DayPlans.Days; day++)
            {
                double refill = 0;
                if (day > 1)
                {
                    foreach (var p in state.Points) p.Active = false;   // vació todo el día anterior
                    refill = RefillRounds(l, state.NewDay(day, 0, rng));
                }
                result.Add(Plan(l, day, refill));
            }
            return result;
        }

        /// <summary>Tipo de munición a dar: el de las armas que lleva el jugador con la reserva más vacía (proporcionalmente). Sin armas de fuego, pistola.</summary>
        public static AmmoType PickType(IList<AmmoType> owned, Func<AmmoType, int> count, Func<AmmoType, int> max)
        {
            AmmoType best = AmmoType.Pistol; float bestFill = float.MaxValue;
            foreach (var t in owned.Distinct())
            {
                if (t == AmmoType.None) continue;
                float fill = count(t) / (float)Math.Max(1, max(t));
                if (fill < bestFill) { bestFill = fill; best = t; }
            }
            return best;
        }

        /// <summary>Tamaño de un paquete de munición de ese tipo (lo que suelta un zombi o aparece en una tanda).</summary>
        public static int PackSize(AmmoType t) => t == AmmoType.Shotgun ? 6 : (t == AmmoType.Rifle ? 16 : 14);
    }

    /// <summary>"Pity timer": si el jugador pasa PitySeconds sin munición en ninguna arma, avisa una vez (y vuelve a contar).</summary>
    public class AmmoPity
    {
        public float Seconds => _t;
        private float _t;
        public float Limit = AmmoEconomy.PitySeconds;

        /// <summary>Devuelve true cuando toca dejar un drop cerca.</summary>
        public bool Tick(float dt, bool hasAnyAmmo)
        {
            if (hasAnyAmmo) { _t = 0f; return false; }
            _t += dt;
            if (_t < Limit) return false;
            _t = 0f;
            return true;
        }
    }

    /// <summary>
    /// Pity por tipo: cuenta por separado cada tipo de munición de las armas que lleva el jugador; si un tipo pasa PitySeconds sin balas (ni en el cargador ni en la
    /// reserva), devuelve ese tipo para que el director deje un paquete de ESE tipo cerca, y vuelve a contar. Tipos que ya no lleva se olvidan.
    /// </summary>
    public class AmmoTypePity
    {
        public float Limit = AmmoEconomy.PitySeconds;
        private readonly Dictionary<AmmoType, float> _t = new Dictionary<AmmoType, float>();
        private readonly List<AmmoType> _empty = new List<AmmoType>();

        public float Seconds(AmmoType t) => _t.TryGetValue(t, out float v) ? v : 0f;

        /// <summary>Tipo al que le toca un drop (o None).</summary>
        public AmmoType Tick(float dt, IList<WeaponState> weapons, AmmoReserve ammo)
        {
            _empty.Clear();
            foreach (var w in weapons)
            {
                if (!w.Data.UsesAmmo) continue;
                var t = w.Data.ammoType;
                bool none = ammo.Count(t) <= 0;
                if (none) foreach (var o in weapons) if (o.Data.UsesAmmo && o.Data.ammoType == t && o.Magazine > 0) { none = false; break; }
                if (none && !_empty.Contains(t)) _empty.Add(t);
            }
            var keys = new List<AmmoType>(_t.Keys);
            foreach (var k in keys) if (!_empty.Contains(k)) _t.Remove(k);
            AmmoType due = AmmoType.None;
            foreach (var t in _empty)
            {
                float v = Seconds(t) + dt;
                if (v >= Limit && due == AmmoType.None) { due = t; v = 0f; }
                _t[t] = v;
            }
            return due;
        }
    }
}

using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Lo que ve la IA director del combate de un día: precisión, headshots, tiempo para matar, daño recibido por tipo y tiempo en cobertura.</summary>
    [Serializable]
    public struct CombatSnapshot
    {
        public int shotsFired, shotsHit, headshots, kills, headshotKills;
        public float accuracy;            // aciertos / disparos (0..1)
        public float headshotRatio;       // headshots / aciertos (0..1)
        public float averageTimeToKill;   // segundos desde el primer impacto hasta la muerte
        public float timeInCover, timePlayed, coverRatio;
        public float damageTaken;
        public string[] damageSources;    // tipos de zombie (o "fall", "other") ...
        public float[] damageAmounts;     // ... y cuánto daño vino de cada uno
    }

    /// <summary>Recoge las métricas de combate. Pura: Unity solo le avisa de los eventos; el director lee Snapshot().</summary>
    public class CombatMetrics
    {
        private int _shots, _hits, _heads, _kills, _headKills, _ttkCount;
        private float _ttkSum, _coverTime, _time, _damage;
        private readonly Dictionary<string, float> _bySource = new Dictionary<string, float>();

        public void RecordShot() => _shots++;
        /// <summary>Un disparo que acertó (una ráfaga de escopeta cuenta como un solo acierto).</summary>
        public void RecordHit(bool headshot) { _hits++; if (headshot) _heads++; }
        public void RecordKill(bool headshot, float timeToKill)
        {
            _kills++; if (headshot) _headKills++;
            if (timeToKill >= 0f) { _ttkSum += timeToKill; _ttkCount++; }
        }
        public void RecordDamageTaken(string source, float amount)
        {
            if (amount <= 0f) return;
            if (string.IsNullOrEmpty(source)) source = "other";
            _damage += amount;
            _bySource[source] = (_bySource.TryGetValue(source, out float v) ? v : 0f) + amount;
        }
        public void Tick(float dt, bool inCover) { _time += dt; if (inCover) _coverTime += dt; }
        public void Reset() { _shots = _hits = _heads = _kills = _headKills = _ttkCount = 0; _ttkSum = _coverTime = _time = _damage = 0f; _bySource.Clear(); }

        public CombatSnapshot Snapshot()
        {
            var s = new CombatSnapshot
            {
                shotsFired = _shots, shotsHit = Math.Min(_hits, Math.Max(_shots, _hits)), headshots = _heads, kills = _kills, headshotKills = _headKills,
                accuracy = _shots == 0 ? 0f : Math.Min(1f, _hits / (float)_shots),
                headshotRatio = _hits == 0 ? 0f : _heads / (float)_hits,
                averageTimeToKill = _ttkCount == 0 ? 0f : _ttkSum / _ttkCount,
                timeInCover = _coverTime, timePlayed = _time, coverRatio = _time <= 0f ? 0f : _coverTime / _time,
                damageTaken = _damage,
            };
            var keys = new List<string>(_bySource.Keys); keys.Sort(StringComparer.Ordinal);
            s.damageSources = keys.ToArray();
            s.damageAmounts = new float[keys.Count];
            for (int i = 0; i < keys.Count; i++) s.damageAmounts[i] = _bySource[keys[i]];
            return s;
        }
    }

    /// <summary>
    /// Parámetros del día que fija la IA director: multiplicadores de vida, velocidad y daño de los zombis, proporción de cada tipo, agresividad de los jefes y frecuencia
    /// de los especiales (lanzador y explosivo). Unity los aplica al generar zombis; el director solo escribe números.
    /// </summary>
    [Serializable]
    public class DirectorTuning
    {
        public float healthMultiplier = 1f, speedMultiplier = 1f, damageMultiplier = 1f;
        public float bossAggressiveness = 1f;   // multiplica la frecuencia de ataques del jefe y acorta su pausa entre ataques
        public float specialFrequency = 1f;     // multiplica el peso de spitter y explosive
        public string[] typeIds = { ZombieIds.Common, ZombieIds.Runner, ZombieIds.Tank, ZombieIds.Armored, ZombieIds.Spitter, ZombieIds.Explosive };
        public float[] typeWeights = { 60f, 15f, 6f, 6f, 8f, 5f };

        public static readonly string[] Specials = { ZombieIds.Spitter, ZombieIds.Explosive };

        public DirectorTuning Clone() => new DirectorTuning
        {
            healthMultiplier = healthMultiplier, speedMultiplier = speedMultiplier, damageMultiplier = damageMultiplier,
            bossAggressiveness = bossAggressiveness, specialFrequency = specialFrequency, typeIds = (string[])typeIds.Clone(), typeWeights = (float[])typeWeights.Clone(),
        };

        /// <summary>Dificultad base creciente por día (el director parte de aquí y la ajusta según el rendimiento).</summary>
        public static DirectorTuning ForDay(int day)
        {
            day = Math.Max(1, Math.Min(7, day));
            float t = (day - 1) / 6f;
            var d = new DirectorTuning
            {
                healthMultiplier = 1f + 0.6f * t, speedMultiplier = 1f + 0.2f * t, damageMultiplier = 1f + 0.4f * t,
                bossAggressiveness = 1f + 0.5f * t, specialFrequency = 0.6f + 1.2f * t,
            };
            // más tipos peligrosos según avanzan los días; el día 1 casi solo comunes
            d.typeWeights = new[] { 90f - 45f * t, 6f + 14f * t, 0f + 10f * t, 0f + 9f * t, 2f + 10f * t, 1f + 7f * t };
            return d;
        }

        /// <summary>Peso efectivo de cada tipo (los especiales se multiplican por specialFrequency).</summary>
        public float EffectiveWeight(int i)
        {
            float w = Math.Max(0f, typeWeights[i]);
            if (Array.IndexOf(Specials, typeIds[i]) >= 0) w *= Math.Max(0f, specialFrequency);
            return w;
        }

        public float Proportion(string id)
        {
            float total = 0f, mine = 0f;
            for (int i = 0; i < typeIds.Length; i++) { float w = EffectiveWeight(i); total += w; if (typeIds[i] == id) mine += w; }
            return total <= 0f ? 0f : mine / total;
        }

        /// <summary>Elige un tipo según las proporciones. roll01 en [0,1).</summary>
        public string Pick(double roll01)
        {
            float total = 0f;
            for (int i = 0; i < typeIds.Length; i++) total += EffectiveWeight(i);
            if (total <= 0f) return ZombieIds.Common;
            double r = Math.Max(0.0, Math.Min(0.999999, roll01)) * total;
            for (int i = 0; i < typeIds.Length; i++) { r -= EffectiveWeight(i); if (r < 0) return typeIds[i]; }
            return typeIds[typeIds.Length - 1];
        }

        public List<string> Validate()
        {
            var e = new List<string>();
            if (healthMultiplier < 0.25f || healthMultiplier > 4f) e.Add("healthMultiplier fuera de 0.25..4");
            if (speedMultiplier < 0.5f || speedMultiplier > 2f) e.Add("speedMultiplier fuera de 0.5..2");
            if (damageMultiplier < 0.25f || damageMultiplier > 4f) e.Add("damageMultiplier fuera de 0.25..4");
            if (bossAggressiveness < 0.5f || bossAggressiveness > 3f) e.Add("bossAggressiveness fuera de 0.5..3");
            if (specialFrequency < 0f || specialFrequency > 4f) e.Add("specialFrequency fuera de 0..4");
            if (typeIds == null || typeWeights == null || typeIds.Length == 0 || typeIds.Length != typeWeights.Length) { e.Add("typeIds y typeWeights deben tener la misma longitud (> 0)"); return e; }
            float sum = 0f;
            for (int i = 0; i < typeIds.Length; i++)
            {
                if (Array.IndexOf(ZombieIds.All, typeIds[i]) < 0 || Array.IndexOf(ZombieIds.Bosses, typeIds[i]) >= 0) e.Add("tipo no válido para oleadas: " + typeIds[i]);
                if (typeWeights[i] < 0f) e.Add("peso negativo");
                sum += typeWeights[i];
            }
            if (sum <= 0f) e.Add("la suma de pesos debe ser > 0");
            return e;
        }
    }
}

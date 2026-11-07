using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Estadísticas de combate que se GUARDAN con el progreso (campo `stats` de PUT /api/progress): todo numérico y plano, para que JsonUtility lo convierta y el backend
    /// lo valide con zod. Incluye las métricas de exposición de la cobertura y la fatiga de la cámara de carrera. Los tiempos y contadores se acumulan día a día; la
    /// fatiga guarda el máximo.
    /// </summary>
    [Serializable]
    public class CombatStatsRecord
    {
        public int kills, shotsFired, shotsHit, deaths;
        public int headshots, headshotKills;
        public float averageTimeToKill;     // segundos (media ponderada por muertes)
        public float damageTaken;
        public float coverSeconds, poppedOutSeconds, blindFireSeconds;   // tiempo cubierto, asomado y disparando a ciegas
        public float sprintSeconds;         // tiempo corriendo
        public float maxFatigue;            // 0..1: lo más cansado que ha estado corriendo
        public int daysPlayed;

        public float Accuracy => shotsFired <= 0 ? 0f : Math.Min(1f, shotsHit / (float)shotsFired);
        public float CoverSecondsTotal => coverSeconds + poppedOutSeconds + blindFireSeconds;

        public static CombatStatsRecord FromSnapshot(CombatSnapshot s, int deaths = 0) => new CombatStatsRecord
        {
            kills = s.kills, shotsFired = s.shotsFired, shotsHit = s.shotsHit, deaths = deaths, headshots = s.headshots, headshotKills = s.headshotKills,
            averageTimeToKill = s.averageTimeToKill, damageTaken = s.damageTaken,
            coverSeconds = s.coverSeconds, poppedOutSeconds = s.poppedOutSeconds, blindFireSeconds = s.blindFireSeconds,
            sprintSeconds = s.sprintSeconds, maxFatigue = Math.Max(s.maxFatigue, s.fatigue), daysPlayed = 1,
        };

        /// <summary>Suma lo guardado y lo de un día. Nunca modifica los originales.</summary>
        public static CombatStatsRecord Merge(CombatStatsRecord stored, CombatStatsRecord day)
        {
            stored = stored ?? new CombatStatsRecord(); day = day ?? new CombatStatsRecord();
            int kills = stored.kills + day.kills;
            return new CombatStatsRecord
            {
                kills = kills, shotsFired = stored.shotsFired + day.shotsFired, shotsHit = stored.shotsHit + day.shotsHit, deaths = stored.deaths + day.deaths,
                headshots = stored.headshots + day.headshots, headshotKills = stored.headshotKills + day.headshotKills,
                averageTimeToKill = kills <= 0 ? 0f : (stored.averageTimeToKill * stored.kills + day.averageTimeToKill * day.kills) / kills,
                damageTaken = stored.damageTaken + day.damageTaken,
                coverSeconds = stored.coverSeconds + day.coverSeconds, poppedOutSeconds = stored.poppedOutSeconds + day.poppedOutSeconds, blindFireSeconds = stored.blindFireSeconds + day.blindFireSeconds,
                sprintSeconds = stored.sprintSeconds + day.sprintSeconds, maxFatigue = Math.Max(stored.maxFatigue, day.maxFatigue), daysPlayed = stored.daysPlayed + day.daysPlayed,
            };
        }

        /// <summary>Mismas reglas que el esquema zod del backend: contadores enteros >= 0, tiempos >= 0, fatiga en 0..1, disparos acertados <= disparados.</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            if (kills < 0 || shotsFired < 0 || shotsHit < 0 || deaths < 0 || headshots < 0 || headshotKills < 0 || daysPlayed < 0) e.Add("contadores negativos");
            if (shotsHit > shotsFired) e.Add("shotsHit > shotsFired");
            if (headshotKills > kills) e.Add("headshotKills > kills");
            if (averageTimeToKill < 0f || damageTaken < 0f || coverSeconds < 0f || poppedOutSeconds < 0f || blindFireSeconds < 0f || sprintSeconds < 0f) e.Add("tiempos o daño negativos");
            if (maxFatigue < 0f || maxFatigue > 1f) e.Add("maxFatigue fuera de 0..1");
            return e;
        }
    }

    /// <summary>
    /// Reglas del director que leen las estadísticas GUARDADAS (acumuladas más el día en curso): abuso de cobertura y fatiga al correr. Puras y explicables: cada ajuste
    /// devuelve su motivo para mostrarlo en el panel F4 y en el panel explicativo de la Fase 4.
    /// </summary>
    public static class DirectorRules
    {
        public const float AbuseSpecialBoost = 1.5f, TiredRunnerBoost = 1.3f;
        public const float TiredFatigue = 0.8f, TiredSprintSeconds = 30f;

        public static bool IsTiredRunner(CombatStatsRecord s) => s.maxFatigue >= TiredFatigue && s.sprintSeconds >= TiredSprintSeconds;

        /// <summary>
        /// Ajusta el tuning base del día según las estadísticas: si abusas de la cobertura suben los especiales (lanzadores y arcos) y los lanzadores flanquean antes
        /// (coverPressure); si corres siempre hasta agotarte suben los corredores. Devuelve una COPIA y las razones.
        /// </summary>
        public static DirectorTuning Adjust(DirectorTuning baseTuning, CombatStatsRecord stats, List<string> reasons = null)
        {
            var t = baseTuning.Clone();
            stats = stats ?? new CombatStatsRecord();
            if (CoverAbuse.IsAbusing(stats))
            {
                t.coverPressure = 1f;
                t.specialFrequency = Math.Min(4f, t.specialFrequency * AbuseSpecialBoost);
                reasons?.Add("abuso de cobertura (" + (int)(stats.coverSeconds / Math.Max(1f, stats.CoverSecondsTotal) * 100f) + " % del tiempo en cobertura escondido): más lanzadores y flanqueo");
            }
            if (IsTiredRunner(stats))
            {
                for (int i = 0; i < t.typeIds.Length; i++) if (t.typeIds[i] == ZombieIds.Runner) t.typeWeights[i] *= TiredRunnerBoost;
                reasons?.Add("corres hasta agotarte (fatiga máx. " + (int)(stats.maxFatigue * 100f) + " %): más corredores");
            }
            return t;
        }
    }
}

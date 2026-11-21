using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Estadísticas de un día terminado (JSON plano: se envían al backend en el Paso 3 dentro de MatchStats).</summary>
    [Serializable]
    public class DayStats
    {
        public int day;
        public float waveSeconds;          // de "¡OLEADA!" a "OLEADA SUPERADA"
        public int zombiesTotal;           // zombis de la oleada (sin jefe)
        public int kills, headshots, headshotKills, shotsFired, shotsHit;
        public int grenadeKills, grenadesThrown;
        public int bestStreak;                     // mejor racha (bajas a menos de 4 s entre sí)
        public string[] weapons = new string[0];   // armas con las que disparó ese día   // bajas por granada y granadas lanzadas ese día
        public float damageTaken, coverSeconds, timePlayed;
        public bool died, boss;
        public string[] killTypes = new string[0];
        public int[] killCounts = new int[0];
        public float rawScore, score;      // puntuación del día (0-100) y suavizada

        public float Accuracy => shotsFired <= 0 ? 0f : Math.Min(1f, shotsHit / (float)shotsFired);
        public float HeadshotKillRatio => kills <= 0 ? 0f : headshotKills / (float)kills;
        public float CoverRatio => timePlayed <= 0f ? 0f : Math.Min(1f, coverSeconds / timePlayed);

        public static DayStats From(int day, CombatSnapshot s, float waveSeconds, int zombiesTotal, bool died, bool boss, IDictionary<string, int> killsByType)
        {
            var d = new DayStats
            {
                day = day, waveSeconds = Math.Max(0f, waveSeconds), zombiesTotal = zombiesTotal, kills = s.kills, headshots = s.headshots, headshotKills = s.headshotKills,
                shotsFired = s.shotsFired, shotsHit = Math.Min(s.shotsHit, s.shotsFired), damageTaken = s.damageTaken, coverSeconds = s.timeInCover, timePlayed = s.timePlayed,
                died = died, boss = boss,
            };
            if (killsByType != null)
            {
                var keys = new List<string>(killsByType.Keys); keys.Sort(StringComparer.Ordinal);
                d.killTypes = keys.ToArray();
                d.killCounts = new int[keys.Count];
                for (int i = 0; i < keys.Count; i++) d.killCounts[i] = killsByType[keys[i]];
            }
            return d;
        }
    }

    /// <summary>Una partida: mapa, días y resultado. Lo que se guarda en local y se sube al terminar (Paso 3).</summary>
    [Serializable]
    public class MatchStats
    {
        public string mapId = "";
        public bool victory, finished;
        public List<DayStats> days = new List<DayStats>();

        public int DaysCompleted { get { int n = 0; foreach (var d in days) if (!d.died) n++; return n; } }
        public int Kills { get { int n = 0; foreach (var d in days) n += d.kills; return n; } }
        public int Headshots { get { int n = 0; foreach (var d in days) n += d.headshotKills; return n; } }
        public int GrenadeKills { get { int n = 0; foreach (var d in days) n += d.grenadeKills; return n; } }
        public float Seconds { get { float n = 0; foreach (var d in days) n += d.timePlayed; return n; } }
        public float Accuracy { get { int f = 0, h = 0; foreach (var d in days) { f += d.shotsFired; h += d.shotsHit; } return f <= 0 ? 0f : h / (float)f; } }
        public float DamageTaken { get { float n = 0; foreach (var d in days) n += d.damageTaken; return n; } }
        public float LastScore => days.Count == 0 ? PerformanceScore.Neutral : days[days.Count - 1].score;

        /// <summary>Puntos para el ranking: días superados, bajas, headshots, victoria y la puntuación media de rendimiento.</summary>
        public int RankingPoints()
        {
            float avg = 0f; foreach (var d in days) avg += d.score; avg = days.Count == 0 ? 0f : avg / days.Count;
            return DaysCompleted * 1000 + Kills * 10 + Headshots * 5 + (victory ? 5000 : 0) + (int)Math.Round(avg * 10f);
        }
    }

    /// <summary>
    /// Puntuación de rendimiento 0-100 de un día (pura y explicable). 50 = lo esperado. Suma de cinco partes:
    /// precisión (25), muertes a la cabeza (15), poco daño recibido por zombi (30), rapidez al limpiar la oleada (20) y uso equilibrado de la cobertura (10).
    /// Morir deja el día en 20 como mucho. Se suaviza con la de días anteriores para que un día raro no dispare la dificultad.
    /// </summary>
    public static class PerformanceScore
    {
        public const float Neutral = 50f, Smoothing = 0.4f, DeathCap = 20f;
        public const float GoodAccuracy = 0.6f, GoodHeadshotRatio = 0.4f, BadDamagePerZombie = 8f, SecondsPerZombie = 6f;

        private static float C01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static readonly string[] PartNames = { "Precisión", "Cabeza", "Daño evitado", "Rapidez", "Cobertura" };
        public static readonly float[] PartMax = { 25f, 15f, 30f, 20f, 10f };

        /// <summary>Las cinco partes de la puntuación (precisión, cabeza, daño, rapidez, cobertura), en sus puntos (máximos en PartMax).</summary>
        public static float[] Parts(DayStats d)
        {
            float acc = 25f * C01(d.Accuracy / GoodAccuracy);
            float head = 15f * C01(d.HeadshotKillRatio / GoodHeadshotRatio);
            float dmg = d.kills == 0 ? 0f : 30f * (1f - C01(d.damageTaken / Math.Max(1, d.kills) / BadDamagePerZombie));
            float expected = Math.Max(1, d.zombiesTotal) * SecondsPerZombie;
            float speed = d.waveSeconds <= 0f ? 10f : 20f * C01(expected / d.waveSeconds);
            float cr = d.CoverRatio;
            float cover = cr < 0.1f ? 10f * (0.5f + 5f * cr) : (cr <= 0.6f ? 10f : 10f * C01(1f - (cr - 0.6f) / 0.4f));
            return new[] { acc, head, dmg, speed, cover };
        }

        public static float Raw(DayStats d, List<string> parts = null)
        {
            float acc = 25f * C01(d.Accuracy / GoodAccuracy);
            float head = 15f * C01(d.HeadshotKillRatio / GoodHeadshotRatio);
            float dmg = 30f * (1f - C01(d.damageTaken / Math.Max(1, d.kills) / BadDamagePerZombie));
            if (d.kills == 0) dmg = 0f;
            float expected = Math.Max(1, d.zombiesTotal) * SecondsPerZombie;
            float speed = d.waveSeconds <= 0f ? 10f : 20f * C01(expected / d.waveSeconds);
            // cobertura: lo ideal es entre el 10 % y el 60 % del tiempo; nada o siempre escondido puntúa menos
            float cr = d.CoverRatio;
            float cover = cr < 0.1f ? 10f * (0.5f + 5f * cr) : (cr <= 0.6f ? 10f : 10f * C01(1f - (cr - 0.6f) / 0.4f));
            float total = acc + head + dmg + speed + cover;
            if (d.died) total = Math.Min(total, DeathCap);
            parts?.Add("precisión " + acc.ToString("0") + "/25 · cabeza " + head.ToString("0") + "/15 · daño " + dmg.ToString("0") + "/30 · rapidez " + speed.ToString("0") + "/20 · cobertura " + cover.ToString("0") + "/10" + (d.died ? " · murió (tope 20)" : ""));
            return Math.Max(0f, Math.Min(100f, total));
        }

        /// <summary>Media exponencial: 60 % lo anterior, 40 % lo de hoy.</summary>
        public static float Smooth(float previous, float raw) => previous + Smoothing * (raw - previous);
    }

    /// <summary>
    /// Ajuste del director por la puntuación suavizada (puro, con límites): 45-55 no cambia nada; por encima sube vida, velocidad, daño y especiales y acorta el
    /// retardo de la horda; por debajo, al revés. Ningún parámetro se mueve más de MaxChange (±25 %) respecto al día base, y el resultado siempre es válido.
    /// </summary>
    public static class DirectorAdapt
    {
        public const float DeadZone = 5f, MaxChange = 0.25f;
        public const float HealthK = 0.25f, SpeedK = 0.10f, DamageK = 0.20f, SpecialK = 0.30f, HordeK = 0.30f;

        /// <summary>-1..1: cuánto se aparta la puntuación de lo esperado (0 dentro de la zona muerta).</summary>
        public static float Delta(float score)
        {
            float d = score - PerformanceScore.Neutral;
            if (Math.Abs(d) <= DeadZone) return 0f;
            d = (d - Math.Sign(d) * DeadZone) / (PerformanceScore.Neutral - DeadZone);
            return Math.Max(-1f, Math.Min(1f, d));
        }

        private static float Lim(float k, float delta) => 1f + Math.Max(-MaxChange, Math.Min(MaxChange, k * delta));

        public static DirectorTuning Apply(DirectorTuning baseTuning, float score, List<string> reasons = null)
        {
            var t = baseTuning.Clone();
            float d = Delta(score);
            if (d == 0f) { reasons?.Add("rendimiento " + score.ToString("0") + "/100: lo esperado, sin cambios"); return t; }
            float h = Lim(HealthK, d), s = Lim(SpeedK, d), dm = Lim(DamageK, d), sp = Lim(SpecialK, d), hd = Lim(HordeK, -d);
            t.healthMultiplier = Clamp(t.healthMultiplier * h, 0.25f, 4f);
            t.speedMultiplier = Clamp(t.speedMultiplier * s, 0.5f, 2f);
            t.damageMultiplier = Clamp(t.damageMultiplier * dm, 0.25f, 4f);
            t.specialFrequency = Clamp(t.specialFrequency * sp, 0f, 4f);
            t.hordeDelayMax = Clamp(t.hordeDelayMax * hd, 0f, 5f);
            string P(float m) => ((m - 1f) * 100f).ToString("+0;-0") + " %";
            reasons?.Add("rendimiento " + score.ToString("0") + "/100 (" + (d > 0 ? "alto: más difícil" : "bajo: más fácil") + "): vida " + P(h) + ", velocidad " + P(s) + ", daño " + P(dm) + ", especiales " + P(sp) + ", retardo de la horda " + P(hd));
            return t;
        }

        private static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
    }
}

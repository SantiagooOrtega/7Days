using System;
using System.Collections.Generic;
using System.Text;

namespace SevenDays.Procedural
{
    public enum StatsScreenMode { Defeat, Victory, DaySummary }

    public enum Medal { Francotirador, SinCobertura, Racha, Granadero, Intocable, Superviviente }

    /// <summary>Una barra de rendimiento: nombre, 0-100 y la explicación del director en texto.</summary>
    public struct CategoryBar { public string Name; public float Value; public string Explain; }

    /// <summary>
    /// Datos de la pantalla de muerte, victoria y resumen del día (puro, probado en EditMode): medallas según las estadísticas, barras 0-100 por categoría con
    /// su explicación, formato de los números y la tabla por día. La pantalla (UI/StatsScreen) solo los pinta.
    /// </summary>
    public static class StatsReport
    {
        public const float SniperAccuracy = 0.55f, SniperHeadRatio = 0.4f, NoCoverRatio = 0.05f, LowDamagePerKill = 2f;
        public const int SniperMinKills = 8, NoCoverMinKills = 10, StreakMedal = 8, GrenadierKills = 5;

        public static string MedalName(Medal m)
        {
            switch (m)
            {
                case Medal.Francotirador: return "Francotirador";
                case Medal.SinCobertura: return "Sin cobertura";
                case Medal.Racha: return "Racha";
                case Medal.Granadero: return "Granadero";
                case Medal.Intocable: return "Intocable";
                default: return "Superviviente";
            }
        }

        public static string MedalHow(Medal m)
        {
            switch (m)
            {
                case Medal.Francotirador: return "Precisión ≥ 55 % y 40 % de bajas a la cabeza";
                case Medal.SinCobertura: return "10+ bajas casi sin cubrirse";
                case Medal.Racha: return "8 bajas seguidas";
                case Medal.Granadero: return "5 bajas con granada";
                case Medal.Intocable: return "Menos de 2 de daño por baja";
                default: return "Superó 5 días o más";
            }
        }

        /// <summary>Medallas de un conjunto de días (la partida entera o un día).</summary>
        public static List<Medal> Medals(IList<DayStats> days)
        {
            var list = new List<Medal>();
            int kills = 0, hk = 0, fired = 0, hit = 0, grenade = 0, best = 0, survived = 0; float cover = 0f, played = 0f, dmg = 0f;
            foreach (var d in days)
            {
                kills += d.kills; hk += d.headshotKills; fired += d.shotsFired; hit += Math.Min(d.shotsHit, d.shotsFired); grenade += d.grenadeKills;
                best = Math.Max(best, d.bestStreak); cover += d.coverSeconds; played += d.timePlayed; dmg += d.damageTaken; if (!d.died) survived++;
            }
            float acc = fired <= 0 ? 0f : hit / (float)fired, headRatio = kills <= 0 ? 0f : hk / (float)kills, coverRatio = played <= 0f ? 1f : cover / played;
            if (kills >= SniperMinKills && acc >= SniperAccuracy && headRatio >= SniperHeadRatio) list.Add(Medal.Francotirador);
            if (kills >= NoCoverMinKills && coverRatio < NoCoverRatio) list.Add(Medal.SinCobertura);
            if (best >= StreakMedal) list.Add(Medal.Racha);
            if (grenade >= GrenadierKills) list.Add(Medal.Granadero);
            if (kills >= 10 && dmg / kills < LowDamagePerKill) list.Add(Medal.Intocable);
            if (survived >= 5) list.Add(Medal.Superviviente);
            return list;
        }

        public static int BestStreak(IList<DayStats> days) { int b = 0; foreach (var d in days) b = Math.Max(b, d.bestStreak); return b; }

        /// <summary>Barras 0-100 por categoría del día con la explicación del director.</summary>
        public static CategoryBar[] Bars(DayStats d)
        {
            var parts = PerformanceScore.Parts(d);
            var bars = new CategoryBar[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                bars[i] = new CategoryBar { Name = PerformanceScore.PartNames[i], Value = Math.Max(0f, Math.Min(100f, parts[i] / PerformanceScore.PartMax[i] * 100f)), Explain = Explain(i, d) };
            return bars;
        }

        private static string Explain(int part, DayStats d)
        {
            switch (part)
            {
                case 0: return Percent(d.Accuracy) + " de aciertos (bien a partir del 60 %)";
                case 1: return Percent(d.HeadshotKillRatio) + " de bajas a la cabeza (bien a partir del 40 %)";
                case 2: return (d.kills <= 0 ? "sin bajas" : (d.damageTaken / d.kills).ToString("0.0") + " de daño recibido por baja") + " (mal desde 8)";
                case 3: return "oleada en " + Time(d.waveSeconds) + " (lo esperado: " + Time(Math.Max(1, d.zombiesTotal) * PerformanceScore.SecondsPerZombie) + ")";
                default: return Percent(d.CoverRatio) + " del tiempo a cubierto (lo ideal: 10-60 %)";
            }
        }

        // ---------------------------------------------------------------- formato
        public static string Time(float seconds)
        {
            int s = Math.Max(0, (int)Math.Round(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        public static string Percent(float ratio) => ((int)Math.Round(Math.Max(0f, Math.Min(1f, ratio)) * 100f)) + " %";

        public static string Damage(float dmg) => ((int)Math.Round(Math.Max(0f, dmg))).ToString();

        public static string Weapons(IList<DayStats> days)
        {
            var seen = new List<string>();
            foreach (var d in days) if (d.weapons != null) foreach (var w in d.weapons) if (!string.IsNullOrEmpty(w) && !seen.Contains(w)) seen.Add(w);
            return seen.Count == 0 ? "—" : string.Join(", ", seen);
        }

        /// <summary>Tabla por día, ordenada por día: Día | Bajas | Cabeza | Precisión | Daño | Tiempo | Puntos.</summary>
        public static List<string[]> DayTable(IList<DayStats> days)
        {
            var sorted = new List<DayStats>(days);
            sorted.Sort((a, b) => a.day.CompareTo(b.day));
            var rows = new List<string[]> { new[] { "Día", "Bajas", "Cabeza", "Precisión", "Daño", "Tiempo", "Puntos" } };
            foreach (var d in sorted)
                rows.Add(new[] { d.day + (d.died ? " †" : ""), d.kills.ToString(), d.headshotKills.ToString(), Percent(d.Accuracy), Damage(d.damageTaken), Time(d.waveSeconds), ((int)Math.Round(d.score)).ToString() });
            return rows;
        }

        /// <summary>Texto de la tabla con columnas alineadas (fuente proporcional: se usa con tabuladores fijos en la pantalla).</summary>
        public static string TableText(List<string[]> rows)
        {
            var sb = new StringBuilder();
            foreach (var r in rows) { for (int i = 0; i < r.Length; i++) { sb.Append(r[i].PadRight(i == 0 ? 6 : 11)); } sb.Append('\n'); }
            return sb.ToString();
        }

        /// <summary>Contador que sube durante la animación de entrada (0..1 con ease-out).</summary>
        public static int CountUp(int target, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            return (int)Math.Round(target * e);
        }
    }
}

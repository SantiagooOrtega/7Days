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

        public static readonly string[] StatNames = { "Bajas", "Disparos a la cabeza", "Precisión", "Daño recibido", "Tiempo de oleada", "Armas usadas", "Mejor racha" };
        private static int Clampi(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        /// <summary>Valores de la lista de estadísticas (texto final) y su explicación: lo prueban las pruebas EditMode a través de StatsReport.</summary>
        public static string[] StatValues(IList<DayStats> list, out int kills, out int heads, out int accPct, out int damage)
        {
            kills = heads = 0; int fired = 0, hit = 0; float dmg = 0f, wave = 0f;
            foreach (var d in list) { kills += d.kills; heads += d.headshotKills; fired += d.shotsFired; hit += Math.Min(d.shotsHit, d.shotsFired); dmg += d.damageTaken; wave += d.waveSeconds; }
            accPct = fired <= 0 ? 0 : (int)Math.Round(hit * 100f / fired); damage = (int)Math.Round(dmg);
            return new[] { kills.ToString(), heads.ToString(), accPct + " %", damage.ToString(), StatsReport.Time(wave), StatsReport.Weapons(list), StatsReport.BestStreak(list).ToString() };
        }

        /// <summary>Gráfica: barras de bajas y línea de precisión por día. Con un solo día (o sin bajas) dibuja al menos una barra mínima y un punto: nunca vacía.</summary>
        public static byte[] Chart(IList<DayStats> days, int w, int h)
        {
            var px = new byte[w * h * 4];
            void Set(int x, int y, byte r, byte g, byte b, byte a) { if (x < 0 || y < 0 || x >= w || y >= h) return; int o = (y * w + x) * 4; px[o] = r; px[o + 1] = g; px[o + 2] = b; px[o + 3] = a; }
            for (int x = 0; x < w; x++) Set(x, 0, 200, 190, 160, 160);
            int max = 1; foreach (var d in days) max = Math.Max(max, d.kills);
            float bw = w / (float)DayPlans.Days;
            int prevX = -1, prevY = -1;
            for (int i = 0; i < days.Count && i < DayPlans.Days; i++)
            {
                int bh = Math.Max(3, (int)Math.Round((h - 8) * days[i].kills / (float)max));
                for (int x = (int)Math.Round(i * bw + 3); x < (int)Math.Round((i + 1) * bw - 3); x++) for (int y = 1; y <= bh; y++) Set(x, y, days[i].died ? (byte)200 : (byte)190, days[i].died ? (byte)55 : (byte)120, 40, 230);
                int cx = (int)Math.Round((i + 0.5f) * bw), cy = Clampi((int)Math.Round(days[i].Accuracy * (h - 6)) + 3, 2, h - 3);
                for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) Set(cx + dx, cy + dy, 255, 210, 120, 255);   // punto del día
                if (prevX >= 0) for (int s = 0; s <= 30; s++) { int lx = (int)Math.Round(prevX + (cx - prevX) * s / 30f), ly = (int)Math.Round(prevY + (cy - prevY) * s / 30f); Set(lx, ly, 255, 210, 120, 255); Set(lx, ly + 1, 255, 210, 120, 255); }
                prevX = cx; prevY = cy;
            }
            return px;
        }

    }
}

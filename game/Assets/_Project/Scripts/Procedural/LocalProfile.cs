using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Una partida terminada, guardada en local (perfil y ranking sin servidor).</summary>
    [Serializable]
    public class MatchRecord
    {
        public string player = "", mapId = "", date = "";
        public int daysCompleted, dayReached, kills, headshots, points;
        public bool victory;
    }

    /// <summary>Historial local de partidas (JSON en persistentDataPath): perfil, ranking local y "Continuar" (último día alcanzado sin terminar).</summary>
    [Serializable]
    public class LocalProfile
    {
        public const int MaxRecords = 50;
        public List<MatchRecord> matches = new List<MatchRecord>();

        public void Add(MatchRecord r)
        {
            matches.Add(r);
            if (matches.Count > MaxRecords) matches.RemoveAt(0);
        }

        public static MatchRecord From(MatchStats m, string player, int dayReached, string date)
            => new MatchRecord { player = player ?? "", mapId = m.mapId, date = date, daysCompleted = m.DaysCompleted, dayReached = dayReached, kills = m.Kills, headshots = m.Headshots, points = m.RankingPoints(), victory = m.victory };

        /// <summary>Ranking local: mejores puntos primero (empate: más reciente después).</summary>
        public List<MatchRecord> Ranking(int top = 10)
        {
            var l = new List<MatchRecord>(matches);
            l.Sort((a, b) => b.points != a.points ? b.points.CompareTo(a.points) : string.CompareOrdinal(a.date, b.date));
            if (l.Count > top) l.RemoveRange(top, l.Count - top);
            return l;
        }

        public MatchRecord Last => matches.Count == 0 ? null : matches[matches.Count - 1];

        /// <summary>Día con el que "Continuar" empieza: el último alcanzado si la última partida no se ganó; si no hay, 0 (no se puede continuar).</summary>
        public int ContinueDay => Last == null || Last.victory ? 0 : Math.Max(1, Math.Min(DayPlans.Days, Last.dayReached));

        public int TotalKills { get { int n = 0; foreach (var m in matches) n += m.kills; return n; } }
        public int Victories { get { int n = 0; foreach (var m in matches) if (m.victory) n++; return n; } }
        public int BestPoints { get { int b = 0; foreach (var m in matches) b = Math.Max(b, m.points); return b; } }
    }
}

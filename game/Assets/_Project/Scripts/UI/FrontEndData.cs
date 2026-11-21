using System.IO;
using UnityEngine;
using SevenDays.Net;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>Perfil local (persistentDataPath/perfil.json) y envío al ranking del backend si hay sesión (sin bloquear: si falla, se queda en local).</summary>
    public static class FrontEndData
    {
        private static LocalProfile _profile;
        public static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "perfil.json");

        public static LocalProfile Profile
        {
            get
            {
                if (_profile != null) return _profile;
                try { _profile = File.Exists(PathFile) ? JsonUtility.FromJson<LocalProfile>(File.ReadAllText(PathFile)) : null; } catch { _profile = null; }
                return _profile ??= new LocalProfile();
            }
        }

        public static string PlayerName => Session.IsLoggedIn && Session.User != null ? Session.User.username : "Invitado";

        /// <summary>Partida terminada: se guarda en local y, con sesión, se sube al ranking.</summary>
        public static void Record(MatchStats m, int dayReached)
        {
            var r = LocalProfile.From(m, PlayerName, dayReached, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            Profile.Add(r);
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(Profile)); } catch (System.Exception e) { Debug.LogWarning("[7Days] No se pudo guardar el perfil local: " + e.Message); }
            if (Session.IsLoggedIn) _ = ApiClient.Post<RankingSubmitResponse>("/api/rankings", new RankingSubmit { score = r.points, daysSurvived = r.daysCompleted, kills = r.kills, victory = r.victory }, true);
        }

        [System.Serializable] public class RankingSubmit { public int score, daysSurvived, kills; public bool victory; }
        [System.Serializable] public class RankingSubmitResponse { }
        [System.Serializable] public class RankingRow { public int position, score, daysSurvived, kills; public string username; public bool victory; }
        [System.Serializable] public class RankingList { public RankingRow[] rankings; }
    }
}

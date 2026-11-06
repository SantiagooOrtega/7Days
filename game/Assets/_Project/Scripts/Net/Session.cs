using UnityEngine;

namespace SevenDays.Net
{
    /// <summary>
    /// Sesión del jugador. Los tokens viven SOLO en memoria: al cerrar el juego se pierden
    /// (nunca se guardan en PlayerPrefs ni en disco).
    /// </summary>
    public static class Session
    {
        public static string AccessToken { get; private set; }
        public static string RefreshToken { get; private set; }
        public static UserDto User { get; private set; }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

        public static void Set(AuthResponse response)
        {
            User = response.user;
            AccessToken = response.accessToken;
            RefreshToken = response.refreshToken;
        }

        public static void SetTokens(string access, string refresh)
        {
            AccessToken = access;
            RefreshToken = refresh;
        }

        public static void Clear()
        {
            User = null;
            AccessToken = null;
            RefreshToken = null;
        }

        // Si "Domain Reload" está desactivado en el editor, los estáticos sobrevivirían entre Plays.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}

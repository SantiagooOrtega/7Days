using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SevenDays.Net
{
    public class ApiResult<T> where T : class
    {
        public bool Ok;
        public long Status;
        public T Data;
        public string Error;
    }

    /// <summary>Cliente HTTP del backend. Si un endpoint protegido responde 401, intenta renovar el token una vez.</summary>
    public static class ApiClient
    {
        public static string BaseUrl = "http://localhost:3001";
        private const int TimeoutSeconds = 10;
        private static Task<bool> _refreshing;

        public static Task<ApiResult<T>> Get<T>(string path, bool auth = true) where T : class
            => Send<T>("GET", path, null, auth, true);

        public static Task<ApiResult<T>> Post<T>(string path, object body, bool auth = false) where T : class
            => Send<T>("POST", path, body, auth, true);

        public static Task<ApiResult<T>> Put<T>(string path, object body, bool auth = true) where T : class
            => Send<T>("PUT", path, body, auth, true);

        private static async Task<ApiResult<T>> Send<T>(string method, string path, object body, bool auth, bool allowRefresh)
            where T : class
        {
            using var req = new UnityWebRequest(BaseUrl + path, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = TimeoutSeconds;
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            if (auth && Session.IsLoggedIn)
                req.SetRequestHeader("Authorization", "Bearer " + Session.AccessToken);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            long status = req.responseCode;
            string text = req.downloadHandler != null ? req.downloadHandler.text : "";

            if (req.result == UnityWebRequest.Result.ConnectionError)
                return Fail<T>(0, "No se pudo conectar con el servidor (" + BaseUrl + "). ¿Está encendido el backend?");

            if (status == 401 && auth && allowRefresh && !string.IsNullOrEmpty(Session.RefreshToken))
            {
                if (await TryRefresh()) return await Send<T>(method, path, body, auth, false);
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                T data = string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<T>(text);
                return new ApiResult<T> { Ok = true, Status = status, Data = data };
            }
            return Fail<T>(status, ParseError(text, status));
        }

        private static Task<bool> TryRefresh()
        {
            // Si varias peticiones fallan a la vez, solo una renueva el token.
            return _refreshing ??= RefreshOnce();
        }

        private static async Task<bool> RefreshOnce()
        {
            try
            {
                var res = await Send<RefreshResponse>("POST", "/api/auth/refresh",
                    new RefreshRequest { refreshToken = Session.RefreshToken }, false, false);
                if (res.Ok && res.Data != null)
                {
                    Session.SetTokens(res.Data.accessToken, res.Data.refreshToken);
                    return true;
                }
                Session.Clear();
                return false;
            }
            finally
            {
                _refreshing = null;
            }
        }

        private static ApiResult<T> Fail<T>(long status, string error) where T : class
            => new ApiResult<T> { Ok = false, Status = status, Error = error };

        private static string ParseError(string text, long status)
        {
            try
            {
                var body = JsonUtility.FromJson<ErrorBody>(text);
                if (body?.error != null)
                {
                    if (body.error.details != null && body.error.details.Length > 0)
                        return body.error.details[0].path + ": " + body.error.details[0].message;
                    return body.error.message;
                }
            }
            catch { /* respuesta que no es JSON */ }
            return "Error del servidor (HTTP " + status + ")";
        }
    }
}

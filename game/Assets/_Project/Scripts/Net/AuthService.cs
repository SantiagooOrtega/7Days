using System.Threading.Tasks;

namespace SevenDays.Net
{
    public static class AuthService
    {
        public static async Task<ApiResult<AuthResponse>> Register(string email, string username, string password)
        {
            var res = await ApiClient.Post<AuthResponse>("/api/auth/register",
                new RegisterRequest { email = email, username = username, password = password });
            if (res.Ok) Session.Set(res.Data);
            return res;
        }

        public static async Task<ApiResult<AuthResponse>> Login(string email, string password)
        {
            var res = await ApiClient.Post<AuthResponse>("/api/auth/login",
                new LoginRequest { email = email, password = password });
            if (res.Ok) Session.Set(res.Data);
            return res;
        }

        public static async Task Logout()
        {
            if (Session.RefreshToken != null)
                await ApiClient.Post<EmptyResponse>("/api/auth/logout", new RefreshRequest { refreshToken = Session.RefreshToken });
            Session.Clear();
        }
    }
}

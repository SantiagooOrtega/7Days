using System;

namespace SevenDays.Net
{
    // Clases simples que JsonUtility convierte a/desde el JSON del backend.
    [Serializable] public class UserDto { public string id; public string email; public string username; }
    [Serializable] public class AuthResponse { public UserDto user; public string accessToken; public string refreshToken; }
    [Serializable] public class RefreshResponse { public string accessToken; public string refreshToken; }
    [Serializable] public class EmptyResponse { }

    [Serializable] public class RegisterRequest { public string email; public string username; public string password; }
    [Serializable] public class LoginRequest { public string email; public string password; }
    [Serializable] public class RefreshRequest { public string refreshToken; }

    [Serializable] public class ErrorDetail { public string path; public string message; }
    [Serializable] public class ErrorInfo { public string code; public string message; public ErrorDetail[] details; }
    [Serializable] public class ErrorBody { public ErrorInfo error; }
}

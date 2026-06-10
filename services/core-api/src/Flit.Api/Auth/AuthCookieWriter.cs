namespace Flit.Api.Auth;

public static class AuthCookieWriter
{
    public static void SetSessionCookies(
        HttpResponse response,
        IHostEnvironment env,
        string accessToken,
        DateTimeOffset accessExpires,
        string refreshToken,
        DateTimeOffset refreshExpires)
    {
        var secure = !env.IsDevelopment();

        response.Cookies.Append(AuthCookieNames.Access, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = accessExpires,
        });

        response.Cookies.Append(AuthCookieNames.Refresh, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Expires = refreshExpires,
        });
    }

    public static void ClearSessionCookies(HttpResponse response)
    {
        response.Cookies.Delete(AuthCookieNames.Access, new CookieOptions { Path = "/" });
        response.Cookies.Delete(AuthCookieNames.Refresh, new CookieOptions { Path = "/api/v1/auth" });
    }

    public static string? ReadRefreshFromRequest(HttpRequest request) =>
        request.Cookies[AuthCookieNames.Refresh]
        ?? TryReadBodyRefresh(request);

    public static string? ReadAccessFromRequest(HttpRequest request)
    {
        var bearer = request.Headers.Authorization.ToString();
        if (bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return bearer["Bearer ".Length..].Trim();

        return request.Cookies[AuthCookieNames.Access];
    }

    private static string? TryReadBodyRefresh(HttpRequest request)
    {
        // Body refresh se lee en el endpoint; no re-leer stream aquí.
        return null;
    }
}

using Microsoft.AspNetCore.Http;

namespace UserManagementAPI.Middleware;

// Protects /api/users: each request needs a valid "Authorization: Bearer <token>" header
public sealed class TokenAuthenticationMiddleware
{
    private const string ProtectedPath = "/api/users";
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly HashSet<string> _validTokens;

    public TokenAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<TokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        _validTokens = configuration.GetSection("Authentication:ApiTokens").Get<string[]>()
            ?.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        if (_validTokens.Count == 0)
        {
            _logger.LogWarning("No API tokens are configured. All requests to {Path} will be rejected.", ProtectedPath);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Requests outside /api/users are not checked
        if (!context.Request.Path.StartsWithSegments(ProtectedPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = ExtractBearerToken(context.Request.Headers.Authorization.ToString());

        if (token is null || !_validTokens.Contains(token))
        {
            // Log the request, never the token itself
            _logger.LogWarning("Rejected {Method} {Path}: missing or invalid token.",
                context.Request.Method, context.Request.Path);

            await WriteUnauthorizedAsync(context);
            return;
        }

        await _next(context);
    }

    private static string? ExtractBearerToken(string authorizationHeader)
    {
        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorizationHeader[BearerPrefix.Length..].Trim();

        return token.Length == 0 ? null : token;
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized. Provide a valid Bearer token."
        });
    }
}
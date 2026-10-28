using Microsoft.AspNetCore.Builder;

namespace UserManagementAPI.Middleware;

public static class MiddlewareExtensions
{
    public static WebApplication UseRequestLogging(this WebApplication app)
    {
        app.UseMiddleware<RequestLoggingMiddleware>();

        return app;
    }

    public static WebApplication UseErrorHandling(this WebApplication app)
    {
        app.UseMiddleware<ErrorHandlingMiddleware>();

        return app;
    }

    public static WebApplication UseTokenAuthentication(this WebApplication app)
    {
        app.UseMiddleware<TokenAuthenticationMiddleware>();

        return app;
    }
}
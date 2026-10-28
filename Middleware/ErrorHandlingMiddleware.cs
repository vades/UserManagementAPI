using Microsoft.AspNetCore.Http;

namespace UserManagementAPI.Middleware;

// Catches unhandled exceptions and returns a consistent JSON error response
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException)
        {
            // Missing or malformed JSON body
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest,
                "The request body is missing or contains invalid JSON.");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "Internal server error.");
            return;
        }

        // No endpoint matched the URL, so ASP.NET Core returned an empty 404.
        // Endpoints that return their own 404 are skipped, because they have an endpoint.
        if (context.GetEndpoint() is null && context.Response.StatusCode == StatusCodes.Status404NotFound)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound,
                "The requested resource was not found.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        // Once the response has started, the status code can no longer be changed
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
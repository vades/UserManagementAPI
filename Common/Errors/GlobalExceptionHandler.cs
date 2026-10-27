using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace UserManagementAPI.Common.Errors;

public static class GlobalExceptionHandler
{
    // Safety net: every unhandled error returns the same JSON shape
    public static WebApplication UseJsonExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

            if (exception is BadHttpRequestException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "The request body is missing or contains invalid JSON."
                });

                return;
            }

            app.Logger.LogError(exception, "Unhandled error while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "An unexpected error occurred. Please try again later."
            });
        }));

        return app;
    }
}
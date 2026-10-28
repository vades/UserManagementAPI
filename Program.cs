using UserManagementAPI.Features.Users.Data;
using UserManagementAPI.Features.Users.Endpoints;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

// Middleware pipeline. Each request passes through these top to bottom,
// and each response passes back up in reverse order.
//
// 1. Logging:          outermost, records every request and the final status code
// 2. Error handling:   turns exceptions and empty 404s into JSON responses
// 3. Routing:          matches the URL to an endpoint
// 4. Authentication:   rejects /api/users requests without a valid token
// 5. Endpoints:        the actual user operations
app.UseRequestLogging();
app.UseErrorHandling();
app.UseRouting();
app.UseTokenAuthentication();

app.MapUserEndpoints();

app.Run();
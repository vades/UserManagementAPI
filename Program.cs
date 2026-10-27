using UserManagementAPI.Features.Users.Data;
using UserManagementAPI.Features.Users.Endpoints;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

// Pipeline order: logging sees every request and the final status code,
// error handling turns exceptions into JSON responses
app.UseRequestLogging();
app.UseErrorHandling();

app.MapUserEndpoints();

app.Run();
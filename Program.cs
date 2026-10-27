using UserManagementAPI.Features.Users.Data;
using UserManagementAPI.Features.Users.Endpoints;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

// Pipeline order (step 5 will confirm it):
// logging -> error handling -> authentication -> endpoints
app.UseRequestLogging();
app.UseErrorHandling();
app.UseTokenAuthentication();

app.MapUserEndpoints();

app.Run();
using UserManagementAPI.Common.Errors;
using UserManagementAPI.Features.Users.Data;
using UserManagementAPI.Features.Users.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

app.UseJsonExceptionHandler();
app.MapUserEndpoints();

app.Run();
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using UserManagementAPI.Features.Users.Data;
using UserManagementAPI.Features.Users.Models;
using UserManagementAPI.Features.Users.Validation;
using static UserManagementAPI.Responses.ApiResponses;

namespace UserManagementAPI.Features.Users.Endpoints;

public static class UserEndpoints
{
    // Registers all /api/users routes
    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users");

        // GET: users with paging
        users.MapGet("", async (UserStore store, int page = 1, int pageSize = 10) =>
        {
            if (page < 1)
            {
                return Results.BadRequest(new { error = "Page must be 1 or greater." });
            }

            if (pageSize < 1 || pageSize > UserValidation.MaxPageSize)
            {
                return Results.BadRequest(new
                {
                    error = $"Page size must be between 1 and {UserValidation.MaxPageSize}."
                });
            }

            var (items, totalCount) = await store.GetPageAsync(page, pageSize);

            return Results.Ok(new
            {
                message = "Users retrieved successfully.",
                data = items,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }
            });
        });

        // GET: one user by ID
        users.MapGet("/{id:int}", async (int id, UserStore store) =>
        {
            var user = await store.GetByIdAsync(id);

            if (user is null)
            {
                return UserNotFound(id);
            }

            return Results.Ok(new { message = "User retrieved successfully.", data = user });
        });

        // POST: add a new user (both fields required)
        users.MapPost("", async (CreateUserRequest request, UserStore store) =>
        {
            var errors = UserValidation.Validate(request.Name, request.Email, requireAll: true);

            if (errors.Count > 0)
            {
                return ValidationFailed(errors);
            }

            var (result, user) = await store.AddAsync(request.Name, request.Email);

            if (result != UserChangeResult.Success || user is null)
            {
                return EmailTaken();
            }

            return Results.Created($"/api/users/{user.Id}",
                new { message = "User created successfully.", data = user });
        });

        // PUT: replace a user (both fields required)
        users.MapPut("/{id:int}", async (int id, UpdateUserRequest request, UserStore store) =>
        {
            var errors = UserValidation.Validate(request.Name, request.Email, requireAll: true);

            if (errors.Count > 0)
            {
                return ValidationFailed(errors);
            }

            return UpdateResponse(await store.UpdateAsync(id, request.Name, request.Email), id);
        });

        // PATCH: partial update (send only the fields you want to change)
        users.MapPatch("/{id:int}", async (int id, UpdateUserRequest request, UserStore store) =>
        {
            if (request.Name is null && request.Email is null)
            {
                return Results.BadRequest(new { error = "Provide at least one field to update: name or email." });
            }

            var errors = UserValidation.Validate(request.Name, request.Email, requireAll: false);

            if (errors.Count > 0)
            {
                return ValidationFailed(errors);
            }

            return UpdateResponse(await store.UpdateAsync(id, request.Name, request.Email), id);
        });

        // DELETE: remove a user
        users.MapDelete("/{id:int}", async (int id, UserStore store) =>
        {
            if (await store.DeleteAsync(id) == UserChangeResult.NotFound)
            {
                return UserNotFound(id);
            }

            return Results.Ok(new { message = "User deleted successfully." });
        });

        return app;
    }
}
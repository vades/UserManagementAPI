using Microsoft.AspNetCore.Http;
using UserManagementAPI.Features.Users.Models;

namespace UserManagementAPI.Common.Responses;

// Shared response helpers used by the endpoints
public static class ApiResponses
{
    public static IResult UserNotFound(int id) =>
        Results.NotFound(new { message = $"User with ID {id} was not found." });

    public static IResult EmailTaken() =>
        Results.Conflict(new { message = "A user with this email already exists." });

    public static IResult ValidationFailed(Dictionary<string, string> errors) =>
        Results.BadRequest(new { message = "Validation failed.", errors });

    public static IResult UpdateResponse((UserChangeResult Result, User? User) outcome, int id)
    {
        return outcome.Result switch
        {
            UserChangeResult.NotFound => UserNotFound(id),
            UserChangeResult.DuplicateEmail => EmailTaken(),
            _ => Results.Ok(new { message = "User updated successfully.", data = outcome.User })
        };
    }
}
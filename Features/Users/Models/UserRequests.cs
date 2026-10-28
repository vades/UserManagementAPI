namespace UserManagementAPI.Features.Users.Models;

public record CreateUserRequest(string Name, string Email);

// Used by PUT (both fields) and PATCH (only the fields that are sent)
public record UpdateUserRequest(string? Name, string? Email);
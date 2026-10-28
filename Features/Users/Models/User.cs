namespace UserManagementAPI.Features.Users.Models;

public record User(int Id, string Name, string Email, DateTime CreatedAt, DateTime UpdatedAt);

public enum UserChangeResult
{
    Success,
    NotFound,
    DuplicateEmail
}
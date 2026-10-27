using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

// Safety net: every unhandled error returns the same JSON shape
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

// Shared response helpers
static IResult UserNotFound(int id) =>
    Results.NotFound(new { message = $"User with ID {id} was not found." });

static IResult EmailTaken() =>
    Results.Conflict(new { message = "A user with this email already exists." });

static IResult ValidationFailed(Dictionary<string, string> errors) =>
    Results.BadRequest(new { message = "Validation failed.", errors });

static IResult UpdateResponse((UserChangeResult Result, User? User) outcome, int id)
{
    return outcome.Result switch
    {
        UserChangeResult.NotFound => UserNotFound(id),
        UserChangeResult.DuplicateEmail => EmailTaken(),
        _ => Results.Ok(new { message = "User updated successfully.", data = outcome.User })
    };
}

var users = app.MapGroup("/api/users");

// GET: users with paging
users.MapGet("", (UserStore store, int page = 1, int pageSize = 10) =>
{
    if (page < 1)
    {
        return Results.BadRequest(new { message = "Page must be 1 or greater." });
    }

    if (pageSize < 1 || pageSize > UserValidation.MaxPageSize)
    {
        return Results.BadRequest(new
        {
            message = $"Page size must be between 1 and {UserValidation.MaxPageSize}."
        });
    }

    var (items, totalCount) = store.GetPage(page, pageSize);

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
users.MapGet("/{id:int}", (int id, UserStore store) =>
{
    var user = store.GetById(id);

    if (user is null)
    {
        return UserNotFound(id);
    }

    return Results.Ok(new { message = "User retrieved successfully.", data = user });
});

// POST: add a new user (both fields required)
users.MapPost("", (CreateUserRequest request, UserStore store) =>
{
    var errors = UserValidation.Validate(request.Name, request.Email, requireAll: true);

    if (errors.Count > 0)
    {
        return ValidationFailed(errors);
    }

    var (result, user) = store.Add(request.Name, request.Email);

    if (result != UserChangeResult.Success || user is null)
    {
        return EmailTaken();
    }

    return Results.Created($"/api/users/{user.Id}",
        new { message = "User created successfully.", data = user });
});

// PUT: replace a user (both fields required)
users.MapPut("/{id:int}", (int id, UpdateUserRequest request, UserStore store) =>
{
    var errors = UserValidation.Validate(request.Name, request.Email, requireAll: true);

    if (errors.Count > 0)
    {
        return ValidationFailed(errors);
    }

    return UpdateResponse(store.Update(id, request.Name, request.Email), id);
});

// PATCH: partial update (send only the fields you want to change)
users.MapPatch("/{id:int}", (int id, UpdateUserRequest request, UserStore store) =>
{
    if (request.Name is null && request.Email is null)
    {
        return Results.BadRequest(new { message = "Provide at least one field to update: name or email." });
    }

    var errors = UserValidation.Validate(request.Name, request.Email, requireAll: false);

    if (errors.Count > 0)
    {
        return ValidationFailed(errors);
    }

    return UpdateResponse(store.Update(id, request.Name, request.Email), id);
});

// DELETE: remove a user
users.MapDelete("/{id:int}", (int id, UserStore store) =>
{
    if (store.Delete(id) == UserChangeResult.NotFound)
    {
        return UserNotFound(id);
    }

    return Results.Ok(new { message = "User deleted successfully." });
});

app.Run();

// Models
record User(int Id, string Name, string Email);
record CreateUserRequest(string Name, string Email);
record UpdateUserRequest(string? Name, string? Email);

enum UserChangeResult
{
    Success,
    NotFound,
    DuplicateEmail
}

// Thread-safe in-memory storage
sealed class UserStore(ILogger<UserStore> logger)
{
    private readonly object _lock = new();
    private readonly Dictionary<int, User> _users = new()
    {
        [1] = new User(1, "Alice", "alice@example.com"),
        [2] = new User(2, "Bob", "bob@example.com")
    };
    private int _nextId = 3;

    public (List<User> Items, int TotalCount) GetPage(int page, int pageSize)
    {
        lock (_lock)
        {
            var all = _users.Values.OrderBy(u => u.Id).ToList();
            var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);

            return (all.Skip(skip).Take(pageSize).ToList(), all.Count);
        }
    }

    public User? GetById(int id)
    {
        lock (_lock)
        {
            return _users.GetValueOrDefault(id);
        }
    }

    public (UserChangeResult Result, User? User) Add(string name, string email)
    {
        var normalizedEmail = NormalizeEmail(email);

        lock (_lock)
        {
            if (IsEmailTaken(normalizedEmail, null))
            {
                return (UserChangeResult.DuplicateEmail, null);
            }

            var user = new User(_nextId, name.Trim(), normalizedEmail);

            try
            {
                // Add throws on a duplicate key (the indexer would silently overwrite it)
                _users.Add(user.Id, user);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Failed to store user with ID {UserId}.", user.Id);
                throw; // The global handler turns this into a JSON 500
            }

            _nextId++;
            logger.LogInformation("User {UserId} created.", user.Id);

            return (UserChangeResult.Success, user);
        }
    }

    // Used by both PUT (all fields) and PATCH (null means "keep current value")
    public (UserChangeResult Result, User? User) Update(int id, string? name, string? email)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(id, out var existing))
            {
                return (UserChangeResult.NotFound, null);
            }

            var updated = existing with
            {
                Name = name?.Trim() ?? existing.Name,
                Email = email is null ? existing.Email : NormalizeEmail(email)
            };

            if (IsEmailTaken(updated.Email, id))
            {
                return (UserChangeResult.DuplicateEmail, null);
            }

            _users[id] = updated;
            logger.LogInformation("User {UserId} updated.", id);

            return (UserChangeResult.Success, updated);
        }
    }

    public UserChangeResult Delete(int id)
    {
        lock (_lock)
        {
            if (!_users.Remove(id))
            {
                return UserChangeResult.NotFound;
            }

            logger.LogInformation("User {UserId} deleted.", id);

            return UserChangeResult.Success;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // Must be called inside the lock
    private bool IsEmailTaken(string email, int? excludeId)
    {
        return _users.Values.Any(u =>
            u.Id != excludeId &&
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
    }
}

// Input validation rules
static partial class UserValidation
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 255;
    public const int MaxPageSize = 100;

    // requireAll: true for POST/PUT (null is an error)
    // requireAll: false for PATCH (null means "not sent, skip")
    public static Dictionary<string, string> Validate(string? name, string? email, bool requireAll)
    {
        var errors = new Dictionary<string, string>();

        if (requireAll || name is not null)
        {
            var nameError = ValidateName(name);

            if (nameError is not null)
            {
                errors["name"] = nameError;
            }
        }

        if (requireAll || email is not null)
        {
            var emailError = ValidateEmail(email);

            if (emailError is not null)
            {
                errors["email"] = emailError;
            }
        }

        return errors;
    }

    // Returns an error message, or null if the value is valid
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return $"Name must be {NameMaxLength} characters or fewer.";
        }

        return null;
    }

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Email is required.";
        }

        var trimmed = email.Trim();

        if (trimmed.Length > EmailMaxLength)
        {
            return $"Email must be {EmailMaxLength} characters or fewer.";
        }

        if (!EmailRegex().IsMatch(trimmed))
        {
            return "Email format is not valid.";
        }

        return null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
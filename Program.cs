using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;

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
users.MapGet("", async (UserStore store, int page = 1, int pageSize = 10) =>
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
        return Results.BadRequest(new { message = "Provide at least one field to update: name or email." });
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

app.Run();

// Models
record User(int Id, string Name, string Email, DateTime CreatedAt, DateTime UpdatedAt);
record CreateUserRequest(string Name, string Email);
record UpdateUserRequest(string? Name, string? Email);

enum UserChangeResult
{
    Success,
    NotFound,
    DuplicateEmail
}

// Data access with raw SQL and parameters (no ORM)
sealed class UserStore(IConfiguration configuration, ILogger<UserStore> logger)
{
    private const string SelectColumns = "Id, Name, Email, CreatedAt, UpdatedAt";

    private readonly string _connectionString =
        configuration.GetConnectionString("UsersDb")
        ?? throw new InvalidOperationException("Connection string 'UsersDb' is missing in appsettings.json.");

    public async Task<(List<User> Items, int TotalCount)> GetPageAsync(int page, int pageSize)
    {
        await using var connection = await OpenConnectionAsync();

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Users;";
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = connection.CreateCommand();
        pageCommand.CommandText = $"SELECT {SelectColumns} FROM Users ORDER BY Id LIMIT $take OFFSET $skip;";
        pageCommand.Parameters.AddWithValue("$take", pageSize);
        pageCommand.Parameters.AddWithValue("$skip", (long)(page - 1) * pageSize);

        var items = new List<User>();

        await using var reader = await pageCommand.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(ReadUser(reader));
        }

        return (items, totalCount);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    public async Task<(UserChangeResult Result, User? User)> AddAsync(string name, string email)
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO Users (Name, Email, CreatedAt, UpdatedAt)
            VALUES ($name, $email, $now, $now)
            RETURNING {SelectColumns};
            """;
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$email", NormalizeEmail(email));
        command.Parameters.AddWithValue("$now", UtcNowText());

        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            return (UserChangeResult.Success, ReadUser(reader));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint on Email
        {
            return (UserChangeResult.DuplicateEmail, null);
        }
        catch (SqliteException ex)
        {
            logger.LogError(ex, "Failed to insert user with email {Email}.", email);
            throw; // The global handler turns this into a JSON 500
        }
        finally
        {
            logger.LogInformation("Add user attempted.");
        }
    }

    // Used by both PUT (all fields) and PATCH (null means "keep current value")
    public async Task<(UserChangeResult Result, User? User)> UpdateAsync(int id, string? name, string? email)
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE Users
            SET Name = COALESCE($name, Name),
                Email = COALESCE($email, Email),
                UpdatedAt = $now
            WHERE Id = $id
            RETURNING {SelectColumns};
            """;
        command.Parameters.AddWithValue("$name", (object?)name?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$email", (object?)(email is null ? null : NormalizeEmail(email)) ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", UtcNowText());
        command.Parameters.AddWithValue("$id", id);

        try
        {
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return (UserChangeResult.NotFound, null);
            }

            var user = ReadUser(reader);
            logger.LogInformation("User {UserId} updated.", id);

            return (UserChangeResult.Success, user);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint on Email
        {
            return (UserChangeResult.DuplicateEmail, null);
        }
        catch (SqliteException ex)
        {
            logger.LogError(ex, "Failed to update user {UserId}.", id);
            throw;
        }
    }

    public async Task<UserChangeResult> DeleteAsync(int id)
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            return UserChangeResult.NotFound;
        }

        logger.LogInformation("User {UserId} deleted.", id);

        return UserChangeResult.Success;
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        return connection;
    }

    private static User ReadUser(SqliteDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetString(2),
        ParseUtc(reader.GetString(3)),
        ParseUtc(reader.GetString(4)));

    private static DateTime ParseUtc(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    // Matches the format used by the migration: 2026-10-27T08:06:07Z
    private static string UtcNowText() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
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
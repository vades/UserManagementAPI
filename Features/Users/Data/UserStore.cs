using System.Globalization;
using Microsoft.Data.Sqlite;
using UserManagementAPI.Features.Users.Models;

namespace UserManagementAPI.Features.Users.Data;

// Data access with raw SQL and parameters (no ORM)
public sealed class UserStore(IConfiguration configuration, ILogger<UserStore> logger)
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
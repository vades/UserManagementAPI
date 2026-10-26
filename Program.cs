var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// In-memory storage (data resets when the app stops)
var users = new List<User>
{
    new(1, "Alice", "alice@example.com"),
    new(2, "Bob", "bob@example.com")
};
var nextId = 3;

// GET: all users
app.MapGet("/api/users", () =>
{
    return Results.Ok(new
    {
        message = "Users retrieved successfully.",
        data = users
    });
});

// GET: one user by ID
app.MapGet("/api/users/{id:int}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);

    if (user is null)
    {
        return Results.NotFound(new { message = $"User with ID {id} was not found." });
    }

    return Results.Ok(new { message = "User retrieved successfully.", data = user });
});

// POST: add a new user
app.MapPost("/api/users", (CreateUserRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { message = "Name and Email are required." });
    }

    var user = new User(nextId++, request.Name, request.Email);
    users.Add(user);

    return Results.Created($"/api/users/{user.Id}",
        new { message = "User created successfully.", data = user });
});

// PUT: update an existing user
app.MapPut("/api/users/{id:int}", (int id, UpdateUserRequest request) =>
{
    var index = users.FindIndex(u => u.Id == id);

    if (index == -1)
    {
        return Results.NotFound(new { message = $"User with ID {id} was not found." });
    }

    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { message = "Name and Email are required." });
    }

    users[index] = new User(id, request.Name, request.Email);

    return Results.Ok(new { message = "User updated successfully.", data = users[index] });
});

// DELETE: remove a user
app.MapDelete("/api/users/{id:int}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);

    if (user is null)
    {
        return Results.NotFound(new { message = $"User with ID {id} was not found." });
    }

    users.Remove(user);

    return Results.Ok(new { message = "User deleted successfully." });
});

app.Run();

// Models
record User(int Id, string Name, string Email);
record CreateUserRequest(string Name, string Email);
record UpdateUserRequest(string Name, string Email);
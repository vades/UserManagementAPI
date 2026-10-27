# Users API

A simple REST API for managing users, built with **.NET 10**, **C# 14**, **Minimal API**, and **SQLite**. It uses raw SQL with `Microsoft.Data.Sqlite` (no ORM), organizes code by feature, and uses custom middleware for logging, error handling, and token authentication.

This project is a learning exercise. It is built step by step, and each step is reviewed before the next one starts.

---

## 1. What is this API about?

The API manages users with the four CRUD operations, plus PATCH for partial updates. Users are stored in a local SQLite database file at `data/users.db`. Every request under `/api/users` must include a valid Bearer token.

| Method | Endpoint | Purpose | Success | Errors |
|--------|----------|---------|---------|--------|
| GET | `/api/users?page=1&pageSize=10` | List users (paged) | 200 | 400, 401 |
| GET | `/api/users/{id}` | Get one user | 200 | 401, 404 |
| POST | `/api/users` | Add a user | 201 | 400, 401, 409 |
| PUT | `/api/users/{id}` | Replace a user (name and email required) | 200 | 400, 401, 404, 409 |
| PATCH | `/api/users/{id}` | Update only the fields you send | 200 | 400, 401, 404, 409 |
| DELETE | `/api/users/{id}` | Delete a user | 200 | 401, 404 |

### Response format

The API uses two response shapes:

- **Success:** `message`, plus `data` when the response returns something. Paged lists include a `pagination` object.
- **Error:** `error`, a short human-readable message. Validation errors also include an `errors` object that lists each failing field.

Examples:

```json
{ "message": "User created successfully.", "data": { "id": 3, "name": "Charlie", "email": "charlie@example.com", "createdAt": "2026-10-27T08:10:00Z", "updatedAt": "2026-10-27T08:10:00Z" } }
```

```json
{ "error": "Validation failed.", "errors": { "email": "Email format is not valid." } }
```

```json
{ "error": "Unauthorized. Provide a valid Bearer token." }
```

### Validation rules

- `name` is required, trimmed, and at most 100 characters.
- `email` is required, trimmed, at most 255 characters, and must have a valid format. It is stored in lowercase.
- Emails are unique and case-insensitive. A duplicate returns `409 Conflict`.
- `pageSize` must be between 1 and 100, and `page` must be 1 or greater.

---

## 2. Project structure

```text
UserManagementAPI/
├── Program.cs                          # Startup and middleware pipeline
├── appsettings.json                    # Connection string, logging, API tokens
├── UserManagementAPI.csproj
├── Middleware/
│   ├── RequestLoggingMiddleware.cs     # Logs method, path, status code, duration
│   ├── ErrorHandlingMiddleware.cs      # Turns exceptions and empty 404s into JSON
│   ├── TokenAuthenticationMiddleware.cs# Checks the Bearer token for /api/users
│   └── MiddlewareExtensions.cs         # UseRequestLogging, UseErrorHandling, UseTokenAuthentication
├── Responses/
│   └── ApiResponses.cs                 # Shared error helpers: NotFound, Conflict, ValidationFailed
├── Features/
│   └── Users/
│       ├── Endpoints/
│       │   └── UserEndpoints.cs        # All /api/users routes
│       ├── Models/
│       │   ├── User.cs                 # User record and UserChangeResult enum
│       │   └── UserRequests.cs         # CreateUserRequest and UpdateUserRequest
│       ├── Data/
│       │   └── UserStore.cs            # Raw SQL data access
│       └── Validation/
│           └── UserValidation.cs       # Input rules and email regex
├── Migrations/
│   └── 001_create_users.sql            # Creates the Users table and sample data
├── Properties/
│   └── launchSettings.json             # Port and launch profile
└── data/
    └── users.db                        # SQLite database (not in source control)
```

### What each folder is for

- **`Program.cs`** registers services, defines the middleware order, and maps the endpoints. It contains no business logic.
- **`Middleware/`** holds code that runs on every request, regardless of the feature.
- **`Responses/`** holds shared response helpers used by all features. It sits at the root, not under a `Common/` folder, because shared code belongs next to the features, not inside an arbitrary bucket.
- **`Features/Users/`** contains everything about users: routes, models, SQL, and validation. A new feature, such as `Features/Orders/`, follows the same pattern.
- **`Migrations/`** contains SQL scripts that create and change the database.

---

## 3. Middleware pipeline

Each request passes through the middleware from top to bottom. Each response passes back up in reverse order.

```
Request
  -> 1. Request logging       (outermost: records every response, including 401 and 500)
  -> 2. Error handling        (turns exceptions and empty 404s into JSON)
  -> 3. Routing               (matches the URL to an endpoint)
  -> 4. Token authentication  (rejects /api/users requests without a valid token)
  -> 5. Endpoints             (user operations)
Response
```

The order is set in `Program.cs`:

```csharp
app.UseRequestLogging();
app.UseErrorHandling();
app.UseRouting();
app.UseTokenAuthentication();

app.MapUserEndpoints();
```

Why this order:

- **Logging is first** so that rejected requests (`401`) and failed requests (`500`) are still logged.
- **Error handling is before authentication** so that exceptions in any later step still return JSON.
- **Routing is before authentication** so the middleware knows the matched route. The error handler also uses it to tell a real `404` from an unknown URL.
- **Authentication is before the endpoints** so no user code runs without a valid token.

### Request logging

Logs one line per request with the method, path, status code, and duration:

```text
info: UserManagementAPI.Middleware.RequestLoggingMiddleware[0]
      HTTP GET /api/users responded 200 in 12 ms
```

Request and response bodies are not logged, because they contain personal data such as emails.

### Error handling

- Malformed or missing JSON returns `400` with a message.
- Any unhandled exception is logged with its stack trace and returns `500` with `"Internal server error."`. The stack trace never reaches the client.
- An unknown URL returns a JSON `404` instead of an empty body.
- If the response has already started, the middleware doesn't try to change the status code.

### Token authentication

- Clients send the token in the `Authorization` header: `Authorization: Bearer <token>`.
- Only paths starting with `/api/users` are checked.
- A missing or invalid token returns `401` and the `WWW-Authenticate: Bearer` header.
- Tokens are never written to the logs.

---

## 4. Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "UsersDb": "Data Source=data/users.db"
  },
  "Authentication": {
    "ApiTokens": [
      "dev-token-123",
      "dev-token-456"
    ]
  }
}
```

- **`ConnectionStrings:UsersDb`** points to the SQLite file. The path is relative, so run the API from the project folder.
- **`Authentication:ApiTokens`** lists the accepted tokens. These values are for learning only. Replace them before using the API anywhere else.
- **`Logging:LogLevel`** sets how much is logged. Use `Debug` while troubleshooting and `Information` in normal use.

### launchSettings.json

Rider and `dotnet run` read the port from `Properties/launchSettings.json`. The examples in this README use `http://localhost:5062`. Use the URL printed in your console, because it may differ.

### .gitignore

The database files should never be committed:

```gitignore
# SQLite database (local data, not source code)
data/*.db
data/*.db-shm
data/*.db-wal
```

---

## 5. Setup guide

### Requirements

- .NET 10 SDK
- Rider or Visual Studio (optional)
- SQLite command-line tool (optional, for inspecting the database)

### Install the SQLite command-line tool (Windows)

Option A, with winget (recommended):

```powershell
winget install SQLite.SQLite
```

Close PowerShell, open a new window, and check the installation:

```powershell
sqlite3 --version
```

If `sqlite3` is not found, locate the executable and add its folder to your user PATH:

```powershell
Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter sqlite3.exe -ErrorAction SilentlyContinue | Select-Object FullName
```

```powershell
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";<folder-containing-sqlite3.exe>", "User")
```

Use the folder, not the file. Then restart PowerShell.

Option B: download `sqlite-tools-win-x64-*.zip` from https://www.sqlite.org/download.html, extract it, and add the folder with `sqlite3.exe` to your PATH.

### Install the NuGet package

From the project folder:

```powershell
dotnet add package Microsoft.Data.Sqlite
```

### Create the database

```powershell
mkdir data
sqlite3 data\users.db ".read Migrations\001_create_users.sql"
```

The migration is safe to run more than once. Check the table:

```powershell
sqlite3 data\users.db "SELECT * FROM Users;"
```

### Build and run the API

```powershell
dotnet build
dotnet run
```

Wait for the line `Now listening on: http://localhost:5062`. Leave this window open, because the request logs appear here.

---

## 6. How to test the API

Use `curl.exe` in PowerShell. In PowerShell, `curl` is an alias for `Invoke-WebRequest` and doesn't accept curl options, so always type `curl.exe`.

Use `-i` to show the status code and headers. Without it, `curl.exe` prints only the body.

The examples below use the valid token `dev-token-123`. Replace the port with the one in your console.

### Authentication

```powershell
# Missing token (401)
curl.exe -i http://localhost:5062/api/users

# Wrong token (401)
curl.exe -i -H "Authorization: Bearer wrong-token" http://localhost:5062/api/users

# Valid token (200)
curl.exe -i -H "Authorization: Bearer dev-token-123" http://localhost:5062/api/users
```

### CRUD operations

```powershell
# GET all users (paged)
curl.exe -i -H "Authorization: Bearer dev-token-123" "http://localhost:5062/api/users?page=1&pageSize=10"

# GET one user
curl.exe -i -H "Authorization: Bearer dev-token-123" http://localhost:5062/api/users/1

# POST a new user (201)
curl.exe -i -X POST http://localhost:5062/api/users -H "Authorization: Bearer dev-token-123" -H "Content-Type: application/json" -d "{\"name\":\"Charlie\",\"email\":\"charlie@example.com\"}"

# PUT: replace both fields (200)
curl.exe -i -X PUT http://localhost:5062/api/users/1 -H "Authorization: Bearer dev-token-123" -H "Content-Type: application/json" -d "{\"name\":\"Alice Miller\",\"email\":\"alice.miller@example.com\"}"

# PATCH: change only the name (200). createdAt stays the same, updatedAt changes
curl.exe -i -X PATCH http://localhost:5062/api/users/1 -H "Authorization: Bearer dev-token-123" -H "Content-Type: application/json" -d "{\"name\":\"Alice Smith\"}"

# DELETE a user (200)
curl.exe -i -X DELETE -H "Authorization: Bearer dev-token-123" http://localhost:5062/api/users/2
```

### Error cases

```powershell
# Duplicate email (409)
curl.exe -i -X POST http://localhost:5062/api/users -H "Authorization: Bearer dev-token-123" -H "Content-Type: application/json" -d "{\"name\":\"Alice 2\",\"email\":\"ALICE@example.com\"}"

# Invalid email (400)
curl.exe -i -X POST http://localhost:5062/api/users -H "Authorization: Bearer dev-token-123" -H "Content-Type: application/json" -d "{\"name\":\"Dan\",\"email\":\"not-an-email\"}"

# Invalid JSON (400)
curl.exe -i -X POST http://localhost:5062/api/users -H "Authorization: Bearer dev-token-123" -H "Content-Type: application/json" -d "{bad json"

# Unknown user (404)
curl.exe -i -H "Authorization: Bearer dev-token-123" http://localhost:5062/api/users/999

# Unknown URL (404, JSON body)
curl.exe -i http://localhost:5062/api/nothing

# Invalid page size (400)
curl.exe -i -H "Authorization: Bearer dev-token-123" "http://localhost:5062/api/users?pageSize=500"
```

### Check the logs

Every request should produce a line in the server window. For example:

```text
warn: UserManagementAPI.Middleware.TokenAuthenticationMiddleware[0]
      Rejected GET /api/users: missing or invalid token.
info: UserManagementAPI.Middleware.RequestLoggingMiddleware[0]
      HTTP GET /api/users responded 401 in 73 ms
```

### Check the database

After each change, compare the API result with the table:

```powershell
sqlite3 data\users.db "SELECT * FROM Users;"
```

---

## 7. Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| `sqlite3` is not recognized | The PATH was not updated, or PowerShell wasn't restarted | Restart PowerShell, or add the folder to your user PATH (see section 5) |
| `Invoke-WebRequest : Der Parameter "Headers" kann nicht gebunden werden` | PowerShell's `curl` alias is used instead of curl.exe | Type `curl.exe`, not `curl` |
| `MSB3026 ... UserManagementAPI.exe ... being used by another process` | The API is still running and locks the file | Stop the API (Ctrl + C in the run window, or `Stop-Process -Id <pid> -Force`), then build again |
| Request returns nothing in PowerShell | `curl.exe` prints only the body by default | Add `-i` to see the status code and headers |
| No log lines appear | You're looking at the wrong window | Log lines appear in the window running `dotnet run` or Rider's Run window, not where you type curl |
| `unable to open database file` | The working directory is not the project folder | Run the API from the project folder, so `data/users.db` resolves |
| `401` on every request | Missing or wrong token | Send `Authorization: Bearer dev-token-123` and check the spelling in `appsettings.json` |
| Changes to code have no effect | The old build is still running | Stop the API, then run `dotnet build` and `dotnet run` again |

---

## 8. What we did in each activity

### Activity 1: CRUD endpoints

- Created a minimal API project with all four CRUD operations.
- Returned consistent JSON responses with the correct HTTP status codes.
- Validated the request fields on POST and PUT.
- Used an in-memory list to store the users.

### Activity 2: Database layer

- **Bug fixes from the review:** input validation, a global error handler that returns JSON, paging for GET all, PATCH support, and logging.
- **Database setup:** the `Microsoft.Data.Sqlite` package, a `data/` folder, and a connection string in `appsettings.json`.
- **Migration:** `Migrations/001_create_users.sql` creates the `Users` table and inserts two sample users.
- **Raw SQL data access:** the `UserStore` class runs parameterized commands. It uses `RETURNING` to get the saved row in the same statement and a `UNIQUE` constraint on `Email` to prevent duplicates, even under concurrent requests.
- **Async endpoints:** all endpoints and store methods are async.
- **Feature-based structure:** code is organized into `Features/Users/`, with shared code at the root.

Database schema:

```sql
CREATE TABLE IF NOT EXISTS Users (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    Name       TEXT    NOT NULL,
    Email      TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    CreatedAt  TEXT    NOT NULL,
    UpdatedAt  TEXT    NOT NULL
);
```

Timestamps are stored as UTC text in ISO 8601 format, for example `2026-10-27T08:06:07Z`.

### Activity 3: Middleware

- **Request logging:** logs method, path, status code, and duration for every request.
- **Error handling:** returns consistent JSON for exceptions (`500`), malformed JSON (`400`), and unknown URLs (`404`). Logs the exception with its stack trace.
- **Token authentication:** protects `/api/users` with Bearer tokens and returns `401` for missing or invalid tokens.
- **Pipeline:** the middleware order is defined once in `Program.cs` and checked with requests for each layer.
- **Testing:** every layer was tested with valid and invalid requests, plus a temporary test route that threw an exception. That route was removed afterward.

---

## 9. How the coding assistant helped

- Explained what each part of the code does and why it is written that way, so the design choices are clear.
- Wrote complete code for each step, including the project setup, migration, store, middleware, and tests.
- Reviewed the first version and found issues: missing validation, inconsistent errors, thread-safety risks, and linear lookups. Then fixed them.
- Pointed out where the requested middleware order would hide rejected requests from the logs, and proposed an order that keeps the audit trail complete.
- Suggested the feature-based folder structure and moved shared code out of a `Common/` folder when you asked.
- Helped with the setup: the SQLite installation, PATH problems, the PowerShell `curl` alias, and locked build files.
- Gave `curl.exe` commands with expected responses for every test case.
- Adjusted the code to your preferences, such as braces on every `if` block and the connection string in `appsettings.json`.
- Kept each step small so you could review and approve it before the next one.

---

## 10. Tips and best practices for building a simple API with .NET

### Project design

- **Organize by feature.** Keep each feature's routes, models, data access, and validation together. Put shared code in its own folder at the root, not in a generic `Common/` folder.
- **Keep `Program.cs` short.** It should only configure services, the pipeline, and the endpoint mappings.
- **Use records for DTOs and models.** They are short, immutable, and compare by value.
- **Separate request and response shapes from the database model** once the two start to differ, for example when hiding fields or adding computed values.

### Endpoints

- **Use the right HTTP status code.** `201 Created` for new resources, `204` or `200` for successful deletes, `400` for bad input, `401` for missing or invalid authentication, `404` for unknown resources, and `409` for conflicts.
- **Use route constraints** such as `{id:int}` so bad IDs never reach your code.
- **Use `MapGroup`** to avoid repeating the route prefix.
- **Choose PUT or PATCH deliberately.** PUT replaces the whole resource; PATCH changes only the fields that are sent.
- **Limit paging.** Always cap `pageSize` so one request can't load the whole table.

### Validation

- **Validate every input** at the boundary, before it reaches the database.
- **Trim and normalize** strings such as emails, so `Alice@Example.com` and `alice@example.com` are treated as the same.
- **Enforce uniqueness in the database** with a `UNIQUE` constraint. A check in C# alone can fail under concurrent requests.
- **Return all field errors at once** rather than stopping at the first one, so the client can fix everything in one round trip.

### Database and SQL

- **Use parameters, never string concatenation,** to avoid SQL injection. Use `$name` placeholders with `AddWithValue`, as in `UserStore`.
- **Store timestamps in UTC** and use a consistent text format such as ISO 8601 in SQLite.
- **Keep SQL in one place.** Only the `Data/` folder should know about SQL.
- **Use `RETURNING`** (SQLite 3.35+) to read back the inserted or updated row in one statement.
- **Use `await using`** for connections, readers, and commands so resources are released even when an exception occurs.
- **Write migrations as numbered files** (`001_`, `002_`, and so on) and make them repeatable with `IF NOT EXISTS`.
- **Keep the database file out of source control.**

### Error handling

- **Use specific exception types** in `catch`. Avoid catching `Exception` except in a global handler.
- **Handle expected failures as results,** such as a duplicate email, instead of exceptions.
- **Log the exception, then rethrow** with `throw;` so the global handler still runs and the stack trace is kept.
- **Never send stack traces or internal details to the client.** Log them on the server.
- **Return the same error shape everywhere** so clients only need to handle one format.

### Middleware

- **Order matters.** Logging should be outermost so every response is recorded. Error handling should wrap everything else.
- **Keep middleware small and focused** on one concern: logging, errors, or authentication.
- **Never log secrets.** Log that a token was rejected, not the token itself.
- **Don't log personal data** such as request bodies or emails unless you have a clear reason.
- **Use `context.Response.HasStarted`** before changing the status code, because it can't be changed after the response starts.

### Security

- **Don't store real secrets in `appsettings.json`.** Use User Secrets during development (`dotnet user-secrets`), environment variables, or a secret store in production.
- **Use a standard authentication method for real projects.** Static tokens are fine for learning. For production, use JWT bearer authentication or an identity provider.
- **Use HTTPS in production,** and add `app.UseHttpsRedirection()` once the API is deployed.
- **Use `AllowedHosts` carefully.** Replace `*` with the real host names in production.
- **Return `401` for missing or invalid credentials** and `403` for valid credentials without permission.

### Concurrency

- **Shared state needs a lock or a database.** A plain `List` or `Dictionary` used across requests is not safe. The SQLite database handles concurrency for us in this project.
- **Keep locked code short.** Never await network or disk work while holding a lock.

### Logging

- **Use `ILogger<T>`** and structured message templates such as `"User {UserId} created."`. Do not build messages with string interpolation.
- **Choose the right level:** `Information` for normal events, `Warning` for rejected requests, `Error` for failures.
- **Log at the boundary,** such as in middleware or endpoints, and keep the data layer's logs focused on database failures.
- **Set `Microsoft.AspNetCore` to `Warning`** to reduce framework noise.
- **Add file logging with Serilog** when you need logs that outlive the console session.

### Testing

- **Test with `curl.exe -i`** to see status codes and headers, and compare each response with the expected result.
- **Check the database after each write** to confirm the data matches the API response.
- **Test the failure paths too:** missing token, bad JSON, duplicate email, unknown IDs, and forced exceptions.
- **Write automated tests next.** Use an xUnit project with `WebApplicationFactory<Program>` to run the same checks on every change.

### Development workflow

- **Stop the app before you build.** A running `.exe` is locked by Windows and causes `MSB3026` errors.
- **Make small changes and test after each one.** When something breaks, the cause is easy to find.
- **Use the Run window for logs.** Logs appear where the app runs, not where you type requests.
- **Keep one source of truth.** Update this README when the structure or behavior changes.
- **Review code before you commit it.** Look for missing validation, unhandled cases, and secrets.

---

## Next steps

- Add automated tests with xUnit and `WebApplicationFactory<Program>`.
- Replace static tokens with JWT bearer authentication.
- Add Serilog for file logging.
- Move from SQLite to SQL Server or PostgreSQL when you need multiple servers or heavier load.
- Add a second feature (for example, `Orders`) to practise the feature-based structure.

# Users API

A simple REST API for managing users, built with **.NET 10**, **C# 14**, **Minimal API**, and **SQLite**. It uses raw SQL with `Microsoft.Data.Sqlite`, with no ORM.

This project is a learning exercise. It is built step by step, and each activity is reviewed before the next one starts.

---

## 1. What is this API about?

The API manages users with the four CRUD operations, plus PATCH for partial updates. Users are stored in a local SQLite database file at `data/users.db`.

| Method | Endpoint | Purpose | Success | Errors |
|--------|----------|---------|---------|--------|
| GET | `/api/users?page=1&pageSize=10` | List users (paged) | 200 | 400 |
| GET | `/api/users/{id}` | Get one user | 200 | 404 |
| POST | `/api/users` | Add a user | 201 | 400, 409 |
| PUT | `/api/users/{id}` | Replace a user (name and email required) | 200 | 400, 404, 409 |
| PATCH | `/api/users/{id}` | Update only the fields you send | 200 | 400, 404, 409 |
| DELETE | `/api/users/{id}` | Delete a user | 200 | 404 |

### Response format

Every response is JSON with a `message`. Successful responses that return data also include a `data` field. Paged lists include a `pagination` object. Validation errors include an `errors` object that lists each failing field.

### Validation rules

- `name` is required, trimmed, and at most 100 characters.
- `email` is required, trimmed, at most 255 characters, and must have a valid format. It is stored in lowercase.
- Emails are unique and case-insensitive. A duplicate returns `409 Conflict`.
- `pageSize` must be between 1 and 100, and `page` must be 1 or greater.

---

## 2. Project structure

The code is organized by feature. Shared code lives in `Common/`, and everything about users lives in `Features/Users/`.

```text
UserManagementAPI/
├── Program.cs                          # Startup: services, error handler, endpoint mapping
├── appsettings.json                    # Connection string and logging config
├── UserManagementAPI.csproj
├── Common/
│   ├── Errors/
│   │   └── GlobalExceptionHandler.cs   # JSON error responses for unhandled exceptions
│   └── Responses/
│       └── ApiResponses.cs             # Shared helpers: NotFound, Conflict, ValidationFailed
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
└── data/
    └── users.db                        # SQLite database (not in source control)
```

### What each folder is for

- **`Program.cs`** registers services, the global error handler, and the user endpoints. It contains no business logic.
- **`Common/`** holds code that any feature can use. Future features, such as orders, will reuse these helpers.
- **`Features/Users/Endpoints/`** reads requests, calls the store or validator, and returns responses.
- **`Features/Users/Models/`** defines the data shapes for users and requests.
- **`Features/Users/Data/`** contains all SQL. If the database changes, only this folder changes.
- **`Features/Users/Validation/`** contains the input rules, which can be tested without running the API.

---

## 3. What we did in each activity

### Activity 1: CRUD endpoints

- Created a minimal API project with all four CRUD operations.
- Returned consistent JSON responses with the correct HTTP status codes.
- Validated the request fields on POST and PUT.
- Used an in-memory list to store the users.

### Activity 2: Database layer

Activity 2 replaced the in-memory storage with a real SQLite database and included the following work:

- **Bug fixes from the review:** input validation, a global exception handler that always returns JSON, paging for GET all, PATCH support, and logging. The `try/catch` pattern is in the data access layer, where database errors can occur.
- **Database setup:** the `Microsoft.Data.Sqlite` NuGet package, a `data/` folder, and a connection string in `appsettings.json`.
- **Migration:** `Migrations/001_create_users.sql` creates the `Users` table with `Id`, `Name`, `Email`, `CreatedAt`, and `UpdatedAt`. It also inserts two sample users.
- **Raw SQL data access:** the `UserStore` class runs parameterized SQL commands. It uses `RETURNING` to get the saved row in the same statement and `UNIQUE` on `Email` to prevent duplicates, even under concurrent requests.
- **Async endpoints:** all endpoints and store methods are async.
- **Feature-based structure:** the code was split into `Common/` and `Features/Users/` folders, with one namespace per folder.
- **Git ignore:** `data/*.db` files are excluded from source control.

### Database schema

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

---

## 4. Setup guide

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

Create the data folder, then run the migration:

```powershell
mkdir data
sqlite3 data\users.db ".read Migrations\001_create_users.sql"
```

The migration is safe to run more than once. Check the table:

```powershell
sqlite3 data\users.db "SELECT * FROM Users;"
```

### Run the API

```powershell
dotnet run
```

Note the URL shown in the console, for example `http://localhost:5000`. Use that URL in the examples below.

---

## 5. How to test the API

The examples use `curl`. Replace the port with the one shown in your console.

### GET all users (paged)

```bash
curl "http://localhost:5000/api/users?page=1&pageSize=10"
```

### GET one user

```bash
curl http://localhost:5000/api/users/1
```

### POST a new user

```bash
curl -X POST http://localhost:5000/api/users -H "Content-Type: application/json" -d "{\"name\":\"Charlie\",\"email\":\"charlie@example.com\"}"
```

Response (`201 Created`):

```json
{
  "message": "User created successfully.",
  "data": {
    "id": 3,
    "name": "Charlie",
    "email": "charlie@example.com",
    "createdAt": "2026-10-27T08:10:00Z",
    "updatedAt": "2026-10-27T08:10:00Z"
  }
}
```

### PUT (replace both fields)

```bash
curl -X PUT http://localhost:5000/api/users/1 -H "Content-Type: application/json" -d "{\"name\":\"Alice Miller\",\"email\":\"alice.miller@example.com\"}"
```

### PATCH (change only the name)

```bash
curl -X PATCH http://localhost:5000/api/users/1 -H "Content-Type: application/json" -d "{\"name\":\"Alice Smith\"}"
```

`createdAt` stays the same, and `updatedAt` changes.

### DELETE a user

```bash
curl -X DELETE http://localhost:5000/api/users/2
```

### Error cases to try

```bash
# Duplicate email (409)
curl -X POST http://localhost:5000/api/users -H "Content-Type: application/json" -d "{\"name\":\"Alice 2\",\"email\":\"ALICE@example.com\"}"

# Invalid email (400)
curl -X POST http://localhost:5000/api/users -H "Content-Type: application/json" -d "{\"name\":\"Dan\",\"email\":\"not-an-email\"}"

# Invalid JSON (400)
curl -X POST http://localhost:5000/api/users -H "Content-Type: application/json" -d "{bad json"

# Unknown user (404)
curl http://localhost:5000/api/users/999

# Invalid page size (400)
curl "http://localhost:5000/api/users?pageSize=500"
```

### Check the database directly

After each change, compare the API result with the table:

```powershell
sqlite3 data\users.db "SELECT * FROM Users;"
```

---

## 6. How the coding assistant helped

- Explained what each part of the code does and why it is written that way, so the design choices are clear.
- Wrote complete code for each step, including the project setup, migration, and store rewrite.
- Reviewed the first version and found issues: missing validation, inconsistent errors, thread-safety risks, and linear lookups. Then fixed them.
- Simplified the code without changing the approved behavior.
- Suggested the feature-based folder structure and wrote the complete code for it.
- Helped with the setup, including the SQLite installation and PATH problems on Windows.
- Gave `curl` commands and sample requests and responses for testing.
- Adjusted the code to your preferences, such as always using braces for `if` blocks and putting the connection string in `appsettings.json`.
- Kept each step small so you could review and approve it before the next one.

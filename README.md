# Users API

## 1. What is this API about?

This is a simple REST API built with **.NET 10** and **C# 14** for managing users. It uses **Minimal API** and stores data in memory, so it needs no database and is a good starting point for learning how CRUD operations work over HTTP.

The API supports these operations:

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/users` | Get all users |
| GET | `/api/users/{id}` | Get one user by ID |
| POST | `/api/users` | Add a new user |
| PUT | `/api/users/{id}` | Update an existing user |
| DELETE | `/api/users/{id}` | Delete a user by ID |

## 2. What we did in Activity 1

Activity 1 focused on writing and enhancing the API code:

- Created a new minimal API project with `dotnet new web`.
- Implemented all four CRUD operations for users.
- Used an in-memory `List<User>` with two seed users.
- Returned consistent JSON responses, each with a `message` and, when relevant, a `data` field.
- Used the correct HTTP status codes: `200 OK`, `201 Created`, `400 Bad Request`, and `404 Not Found`.
- Validated that `Name` and `Email` are not empty on POST and PUT.
- Applied a consistent code style with braces on every `if` block.

## 3. How the coding assistant helped

- Explained the purpose of each part of the code and the reasoning behind it.
- Wrote the complete code for each step so it was easy to run and test.
- Provided `curl` commands and sample JSON requests and responses for testing.
- Adjusted the code style to match your preferences, such as always using braces for `if` blocks.
- Pointed out limitations of the current approach, such as the in-memory list not being thread-safe, and noted that it will be improved in later activities.
- Kept each step small so you could review, test, and approve it before moving on.

## Running the project

```bash
dotnet run
```

Then open the URL shown in the console and test the endpoints using `curl`, Postman, or any HTTP client.
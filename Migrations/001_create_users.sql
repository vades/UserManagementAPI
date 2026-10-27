-- 001_create_users.sql
-- Creates the Users table.
-- Timestamps are stored as UTC text in ISO 8601 format (for example 2026-10-27T10:15:30Z).

CREATE TABLE IF NOT EXISTS Users (
                                     Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                                     Name       TEXT    NOT NULL,
                                     Email      TEXT    NOT NULL UNIQUE COLLATE NOCASE,
                                     CreatedAt  TEXT    NOT NULL,
                                     UpdatedAt  TEXT    NOT NULL
);

-- Seed data matching the previous in-memory users
INSERT OR IGNORE INTO Users (Id, Name, Email, CreatedAt, UpdatedAt)
VALUES
    (1, 'Alice', 'alice@example.com', strftime('%Y-%m-%dT%H:%M:%SZ', 'now'), strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
    (2, 'Bob',   'bob@example.com',   strftime('%Y-%m-%dT%H:%M:%SZ', 'now'), strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));
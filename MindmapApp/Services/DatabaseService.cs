using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace MindmapApp.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Đường dẫn cơ sở dữ liệu không hợp lệ", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString();

        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                DisplayName TEXT,
                CreatedAt TEXT NOT NULL,
                LastLoginAt TEXT
            );

            CREATE TABLE IF NOT EXISTS OtpRequests (
                Id TEXT PRIMARY KEY,
                Email TEXT NOT NULL,
                Code TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MindmapDocuments (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_MindmapDocuments_UserId ON MindmapDocuments(UserId);";
        command.ExecuteNonQuery();
    }

    public SqliteConnection GetConnection() => new(_connectionString);
}

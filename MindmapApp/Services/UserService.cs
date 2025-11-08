using System;
using System.Globalization;
using Microsoft.Data.Sqlite;
using MindmapApp.Models;

namespace MindmapApp.Services;

public class UserService
{
    private readonly DatabaseService _databaseService;
    private readonly PasswordHasher _passwordHasher;

    public UserService(DatabaseService databaseService, PasswordHasher passwordHasher)
    {
        _databaseService = databaseService;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> RegisterAsync(string email, string password, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = $Email";
            checkCommand.Parameters.AddWithValue("$Email", email.Trim().ToLowerInvariant());
            var exists = (long?)await checkCommand.ExecuteScalarAsync() ?? 0;
            if (exists > 0)
            {
                return false;
            }
        }

        var (hash, salt) = _passwordHasher.HashPassword(password);

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"INSERT INTO Users (Id, Email, PasswordHash, PasswordSalt, DisplayName, CreatedAt)
                                      VALUES ($Id, $Email, $PasswordHash, $PasswordSalt, $DisplayName, $CreatedAt)";
        insertCommand.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
        insertCommand.Parameters.AddWithValue("$Email", email.Trim().ToLowerInvariant());
        insertCommand.Parameters.AddWithValue("$PasswordHash", hash);
        insertCommand.Parameters.AddWithValue("$PasswordSalt", salt);
        insertCommand.Parameters.AddWithValue("$DisplayName", (object?)displayName ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("$CreatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return await insertCommand.ExecuteNonQueryAsync() == 1;
    }

    public async Task<UserAccount?> AuthenticateAsync(string email, string password)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Email, PasswordHash, PasswordSalt, DisplayName, CreatedAt, LastLoginAt FROM Users WHERE Email = $Email";
        command.Parameters.AddWithValue("$Email", email.Trim().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var hash = reader.GetString(2);
            var salt = reader.GetString(3);
            if (_passwordHasher.Verify(password, hash, salt))
            {
                var account = new UserAccount
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Email = reader.GetString(1),
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    DisplayName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind),
                    LastLoginAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind)
                };

                await UpdateLastLoginAsync(connection, account.Id);
                return account;
            }
        }

        return null;
    }

    private static async Task UpdateLastLoginAsync(SqliteConnection connection, Guid userId)
    {
        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = "UPDATE Users SET LastLoginAt = $LastLoginAt WHERE Id = $Id";
        updateCommand.Parameters.AddWithValue("$LastLoginAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        updateCommand.Parameters.AddWithValue("$Id", userId.ToString());
        await updateCommand.ExecuteNonQueryAsync();
    }

    public async Task<string?> CreateOtpAsync(string email, TimeSpan lifetime)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using (var checkUserCommand = connection.CreateCommand())
        {
            checkUserCommand.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = $Email";
            checkUserCommand.Parameters.AddWithValue("$Email", normalizedEmail);
            var exists = (long?)await checkUserCommand.ExecuteScalarAsync() ?? 0;
            if (exists == 0)
            {
                return null;
            }
        }

        var code = Random.Shared.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = "DELETE FROM OtpRequests WHERE Email = $Email";
            deleteCommand.Parameters.AddWithValue("$Email", normalizedEmail);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"INSERT INTO OtpRequests (Id, Email, Code, ExpiresAt, CreatedAt)
                                      VALUES ($Id, $Email, $Code, $ExpiresAt, $CreatedAt)";
        insertCommand.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
        insertCommand.Parameters.AddWithValue("$Email", normalizedEmail);
        insertCommand.Parameters.AddWithValue("$Code", code);
        insertCommand.Parameters.AddWithValue("$ExpiresAt", DateTime.UtcNow.Add(lifetime).ToString("O", CultureInfo.InvariantCulture));
        insertCommand.Parameters.AddWithValue("$CreatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        var result = await insertCommand.ExecuteNonQueryAsync();
        return result == 1 ? code : null;
    }

    public async Task<bool> ValidateOtpAsync(string email, string code)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, ExpiresAt FROM OtpRequests WHERE Email = $Email AND Code = $Code";
        command.Parameters.AddWithValue("$Email", email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("$Code", code.Trim());

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var expiresAt = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);
            if (expiresAt > DateTime.UtcNow)
            {
                var id = reader.GetString(0);
                await using var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM OtpRequests WHERE Id = $Id";
                deleteCommand.Parameters.AddWithValue("$Id", id);
                await deleteCommand.ExecuteNonQueryAsync();
                return true;
            }
        }

        return false;
    }

    public async Task<bool> UpdatePasswordAsync(string email, string newPassword)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var (hash, salt) = _passwordHasher.HashPassword(newPassword);

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET PasswordHash = $Hash, PasswordSalt = $Salt WHERE Email = $Email";
        command.Parameters.AddWithValue("$Hash", hash);
        command.Parameters.AddWithValue("$Salt", salt);
        command.Parameters.AddWithValue("$Email", normalizedEmail);
        return await command.ExecuteNonQueryAsync() == 1;
    }
}

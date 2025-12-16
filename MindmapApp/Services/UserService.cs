using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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

        // 1. Kiểm tra Email đã tồn tại chưa
        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
            checkCommand.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

            var exists = (long?)await checkCommand.ExecuteScalarAsync() ?? 0;
            if (exists > 0)
            {
                return false;
            }
        }

        // Tạo hash và salt
        var (hash, salt) = _passwordHasher.HashPassword(password);

        // 2. Thực hiện INSERT
        var insertSql = @"
            INSERT INTO Users (Id, Email, PasswordHash, PasswordSalt, DisplayName, CreatedAt) 
            VALUES (@Id, @Email, @PasswordHash, @PasswordSalt, @DisplayName, @CreatedAt)";

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = insertSql;

        insertCommand.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
        insertCommand.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
        insertCommand.Parameters.AddWithValue("@PasswordHash", hash);
        insertCommand.Parameters.AddWithValue("@PasswordSalt", salt);
        insertCommand.Parameters.AddWithValue("@DisplayName", (object?)displayName ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));

        return await insertCommand.ExecuteNonQueryAsync() == 1;
    }

    public async Task<UserAccount?> AuthenticateAsync(string email, string password)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        var sql = "SELECT Id, Email, PasswordHash, PasswordSalt, DisplayName, CreatedAt, LastLoginAt FROM Users WHERE Email = @Email";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var idStr = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var emailStr = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var storedHash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var storedSalt = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var displayName = reader.IsDBNull(4) ? null : reader.GetString(4);
            var createdAtStr = reader.IsDBNull(5) ? null : reader.GetString(5);
            var lastLoginStr = reader.IsDBNull(6) ? null : reader.GetString(6);

            // Xác minh mật khẩu
            bool passwordOk = _passwordHasher.Verify(password, storedHash, storedSalt);

            if (passwordOk)
            {
                var account = new UserAccount
                {
                    Id = Guid.TryParse(idStr, out var parsedId) ? parsedId : Guid.Empty,
                    Email = emailStr,
                    PasswordHash = storedHash,
                    PasswordSalt = storedSalt,
                    DisplayName = displayName,
                    CreatedAt = DateTime.TryParse(createdAtStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created) ? created : DateTime.MinValue,
                    LastLoginAt = DateTime.TryParse(lastLoginStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var lastLogin) ? lastLogin : null
                };

                await reader.DisposeAsync();

                // Update last login
                await UpdateLastLoginAsync(account.Id);
                return account;
            }
        }

        return null;
    }

    private async Task UpdateLastLoginAsync(Guid userId)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = "UPDATE Users SET LastLoginAt = @LastLoginAt WHERE Id = @Id";
        updateCommand.Parameters.AddWithValue("@LastLoginAt", DateTime.UtcNow.ToString("O"));
        updateCommand.Parameters.AddWithValue("@Id", userId.ToString());
        await updateCommand.ExecuteNonQueryAsync();
    }

    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using var checkUserCommand = connection.CreateCommand();
        checkUserCommand.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
        checkUserCommand.Parameters.AddWithValue("@Email", normalizedEmail);

        var exists = (long?)await checkUserCommand.ExecuteScalarAsync() ?? 0;
        return exists > 0;
    }

    public async Task<bool> UpdatePasswordAsync(string email, string newPassword)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var (hash, salt) = _passwordHasher.HashPassword(newPassword);

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET PasswordHash = @Hash, PasswordSalt = @Salt WHERE Email = @Email";
        command.Parameters.AddWithValue("@Hash", hash);
        command.Parameters.AddWithValue("@Salt", salt);
        command.Parameters.AddWithValue("@Email", normalizedEmail);
        return await command.ExecuteNonQueryAsync() == 1;
    }

    // --- MỚI: CÁC HÀM HỖ TRỢ ĐỔI MẬT KHẨU TỪ PROFILE ---

    /// <summary>
    /// Cập nhật thông tin User (DisplayName, Password) xuống DB theo ID
    /// </summary>
    public async Task<bool> UpdateUserAsync(UserAccount user)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Users 
            SET DisplayName = @DisplayName, 
                PasswordHash = @PasswordHash, 
                PasswordSalt = @PasswordSalt 
            WHERE Id = @Id";

        command.Parameters.AddWithValue("@DisplayName", (object?)user.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@PasswordSalt", user.PasswordSalt);
        command.Parameters.AddWithValue("@Id", user.Id.ToString());

        return await command.ExecuteNonQueryAsync() > 0;
    }

    // Hỗ trợ kiểm tra mật khẩu cũ mà không cần lộ class PasswordHasher
    public bool VerifyUserPassword(string rawPassword, string hash, string salt)
    {
        return _passwordHasher.Verify(rawPassword, hash, salt);
    }

    // Hỗ trợ tạo hash mới
    public (string Hash, string Salt) ComputeHash(string rawPassword)
    {
        return _passwordHasher.HashPassword(rawPassword);
    }
}
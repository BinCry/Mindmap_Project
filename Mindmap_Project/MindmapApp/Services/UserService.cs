using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MindmapApp.Models;

namespace MindmapApp.Services;

public class UserService
{
    #region Dependencies
    private readonly DatabaseService _databaseService;
    private readonly PasswordHasher _passwordHasher;
    private readonly HttpClient _httpClient;
    #endregion

    #region Constructor
    public UserService(DatabaseService databaseService, PasswordHasher passwordHasher)
    {
        _databaseService = databaseService;
        _passwordHasher = passwordHasher;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            // 🔥 Hostname máy chủ của bạn
            BaseAddress = new Uri("http://DESKTOP-57KS433:5076/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }
    #endregion

    #region Authentication
    public async Task<UserAccount?> AuthenticateAsync(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        UserAccount? cloudAccount = null;

        // 1. Login Online
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/login", new { Email = normalizedEmail, Password = password });
            if (response.IsSuccessStatusCode)
            {
                cloudAccount = await response.Content.ReadFromJsonAsync<UserAccount>();
                if (cloudAccount != null)
                {
                    var (hash, salt) = _passwordHasher.HashPassword(password);
                    cloudAccount.PasswordHash = hash;
                    cloudAccount.PasswordSalt = salt;
                    // Đồng bộ xuống Local
                    await SyncUserToLocalAsync(cloudAccount, password);
                    return cloudAccount;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cloud Login Failed: {ex.Message}");
        }

        // 2. Login Offline
        return await AuthenticateLocalAsync(normalizedEmail, password);
    }
    #endregion

    #region Password Reset
    // 🔥 HÀM QUAN TRỌNG: Đổi mật khẩu đồng bộ Server -> Local
    public async Task<bool> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // BƯỚC 1: Gọi lên Server để update DB chính
        bool serverUpdated = false;
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/reset-password", new
            {
                Email = normalizedEmail,
                Otp = otp,
                NewPassword = newPassword
            });

            if (response.IsSuccessStatusCode)
            {
                serverUpdated = true;
            }
            else
            {
                // Nếu Server báo lỗi (VD: Sai OTP, Email ko tồn tại) -> Dừng luôn
                return false;
            }
        }
        catch (Exception)
        {
            // Mất mạng -> Không cho đổi pass (để đảm bảo tính đồng nhất)
            return false;
        }

        // BƯỚC 2: Nếu Server OK -> Update SQLite Local
        if (serverUpdated)
        {
            await UpdateLocalPasswordAsync(normalizedEmail, newPassword);
            return true;
        }

        return false;
    }
    #endregion

    #region Local Sync
    private async Task<(string Hash, string Salt)?> UpdateLocalPasswordAsync(string email, string newPassword)
    {
        var (hash, salt) = _passwordHasher.HashPassword(newPassword);
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        // Check bảng tồn tại
        var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users'";
        if (await checkCmd.ExecuteScalarAsync() == null) return null;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET PasswordHash = @Hash, PasswordSalt = @Salt WHERE Email = @Email";
        cmd.Parameters.AddWithValue("@Hash", hash);
        cmd.Parameters.AddWithValue("@Salt", salt);
        cmd.Parameters.AddWithValue("@Email", email);
        await cmd.ExecuteNonQueryAsync();
        return (hash, salt);
    }

    private async Task SyncUserToLocalAsync(UserAccount cloudUser, string rawPassword)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        // Cast to SqliteTransaction for command bindings.
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        await EnsureUsersSchemaAsync(connection, transaction);

        var localUserId = await GetLocalUserIdByEmailAsync(connection, transaction, cloudUser.Email);

        if (localUserId.HasValue && localUserId.Value != cloudUser.Id)
        {
            await MigrateMindmapsToCloudUserAsync(connection, transaction, localUserId.Value, cloudUser.Id);
        }

        var (hash, salt) = _passwordHasher.HashPassword(rawPassword);

        var sql = @"
            INSERT OR REPLACE INTO Users (Id, Email, PasswordHash, PasswordSalt, DisplayName, CreatedAt, LastLoginAt, IsPro, UserColor)
            VALUES (@Id, @Email, @Hash, @Salt, @Name, @Created, @Last, @IsPro, @Color)";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;
        cmd.Parameters.AddWithValue("@Id", cloudUser.Id.ToString());
        cmd.Parameters.AddWithValue("@Email", cloudUser.Email);
        cmd.Parameters.AddWithValue("@Hash", hash);
        cmd.Parameters.AddWithValue("@Salt", salt);
        cmd.Parameters.AddWithValue("@Name", (object?)cloudUser.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Created", cloudUser.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@Last", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@IsPro", cloudUser.IsPro ? 1 : 0);
        cmd.Parameters.AddWithValue("@Color", (object?)cloudUser.UserColor ?? "#888888");

        await cmd.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
    #endregion

    #region Schema Helpers
    private static async Task EnsureUsersSchemaAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        var createSql = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY, Email TEXT UNIQUE, PasswordHash TEXT, PasswordSalt TEXT,
                DisplayName TEXT, CreatedAt TEXT, LastLoginAt TEXT, IsPro INTEGER, UserColor TEXT
            );";
        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = createSql;
            createCmd.Transaction = transaction;
            await createCmd.ExecuteNonQueryAsync();
        }

        await TryAddColumnAsync(connection, transaction, "Users", "IsPro", "INTEGER DEFAULT 0");
        await TryAddColumnAsync(connection, transaction, "Users", "UserColor", "TEXT");
    }

    private static async Task<Guid?> GetLocalUserIdByEmailAsync(SqliteConnection connection, SqliteTransaction transaction, string email)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Users WHERE Email = @Email LIMIT 1";
        cmd.Transaction = transaction;
        cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
        var result = await cmd.ExecuteScalarAsync();
        return result is string idText && Guid.TryParse(idText, out var id) ? id : null;
    }

    private static async Task MigrateMindmapsToCloudUserAsync(SqliteConnection connection, SqliteTransaction transaction, Guid localUserId, Guid cloudUserId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE MindmapDocuments SET UserId = @NewId WHERE UserId = @OldId";
        cmd.Transaction = transaction;
        cmd.Parameters.AddWithValue("@NewId", cloudUserId.ToString());
        cmd.Parameters.AddWithValue("@OldId", localUserId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task TryAddColumnAsync(SqliteConnection connection, SqliteTransaction transaction, string tableName, string columnName, string columnType)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
        cmd.Transaction = transaction;

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore if column already exists.
        }
    }
    #endregion

    #region Password Change
    public async Task<(bool Success, string? Hash, string? Salt)> ChangePasswordAsync(string email, string currentPassword, string newPassword)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/change-password", new
            {
                Email = normalizedEmail,
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            });

            if (!response.IsSuccessStatusCode) return (false, null, null);
        }
        catch (Exception)
        {
            return (false, null, null);
        }

        var result = await UpdateLocalPasswordAsync(normalizedEmail, newPassword);
        return result.HasValue ? (true, result.Value.Hash, result.Value.Salt) : (true, null, null);
    }
    #endregion

    #region Local Auth
    private async Task<UserAccount?> AuthenticateLocalAsync(string email, string password)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        var checkCmd = connection.CreateCommand(); checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users'";
        if (await checkCmd.ExecuteScalarAsync() == null) return null;

        var sql = "SELECT Id, Email, PasswordHash, PasswordSalt, DisplayName, CreatedAt, LastLoginAt, IsPro, UserColor FROM Users WHERE Email = @Email";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Email", email);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var storedHash = reader.GetString(2);
            var storedSalt = reader.GetString(3);

            if (_passwordHasher.Verify(password, storedHash, storedSalt))
            {
                var u = new UserAccount
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Email = reader.GetString(1),
                    PasswordHash = storedHash,
                    PasswordSalt = storedSalt,
                    DisplayName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    LastLoginAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                    IsPro = !reader.IsDBNull(7) && reader.GetInt32(7) == 1
                };
                if (!reader.IsDBNull(8)) u.UserColor = reader.GetString(8);
                else u.UserColor = "#4299E1";
                return u;
            }
        }
        return null;
    }
    #endregion

    #region Registration
    public async Task<bool> RegisterAsync(string email, string password, string? displayName)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/register", new { Email = normalizedEmail, Password = password, DisplayName = displayName });
            if (!response.IsSuccessStatusCode) return false;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Cloud Register Failed: {ex.Message}"); }

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        // Tạo bảng nếu chưa có
        var createTableSql = @"CREATE TABLE IF NOT EXISTS Users (Id TEXT PRIMARY KEY, Email TEXT UNIQUE, PasswordHash TEXT, PasswordSalt TEXT, DisplayName TEXT, CreatedAt TEXT, LastLoginAt TEXT, IsPro INTEGER, UserColor TEXT);";
        using (var c = connection.CreateCommand()) { c.CommandText = createTableSql; c.ExecuteNonQuery(); }

        if (await CheckEmailExistsAsync(normalizedEmail)) return false;

        var (hash, salt) = _passwordHasher.HashPassword(password);
        var insertSql = @"
            INSERT INTO Users (Id, Email, PasswordHash, PasswordSalt, DisplayName, CreatedAt, IsPro, UserColor) 
            VALUES (@Id, @Email, @Hash, @Salt, @Name, @Created, 0, @Color)";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = insertSql;
        cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@Email", normalizedEmail);
        cmd.Parameters.AddWithValue("@Hash", hash);
        cmd.Parameters.AddWithValue("@Salt", salt);

        // Tham số chuẩn
        cmd.Parameters.AddWithValue("@Name", (object?)displayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Created", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@Color", "#4299E1");

        return await cmd.ExecuteNonQueryAsync() == 1;
    }
    #endregion

    #region Profile & Helpers
    // Các hàm phụ trợ
    public async Task<bool> UpgradeToProAsync(Guid userId)
    {
        try { await _httpClient.PostAsync($"api/Auth/upgrade-pro/{userId}", null); } catch { }
        await using var connection = _databaseService.GetConnection(); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET IsPro = 1 WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", userId.ToString());
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        await using var connection = _databaseService.GetConnection(); await connection.OpenAsync();
        var checkTable = connection.CreateCommand(); checkTable.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users';";
        if (await checkTable.ExecuteScalarAsync() == null) return false;
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
        cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
        return ((long?)await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    public async Task<bool> UpdateUserAsync(UserAccount user)
    {
        await using var connection = _databaseService.GetConnection(); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET DisplayName = @Name WHERE Id = @Id";
        command.Parameters.AddWithValue("@Name", (object?)user.DisplayName ?? DBNull.Value); command.Parameters.AddWithValue("@Id", user.Id.ToString());
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public bool VerifyUserPassword(string raw, string hash, string salt) => _passwordHasher.Verify(raw, hash, salt);
    public (string Hash, string Salt) ComputeHash(string raw) => _passwordHasher.HashPassword(raw);
    #endregion
}

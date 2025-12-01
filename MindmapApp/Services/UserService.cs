using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
// Thêm thư viện SQL Server
using Microsoft.Data.SqlClient;
using MindmapApp.Models;

namespace MindmapApp.Services;

public class UserService
{
    private readonly DatabaseService _databaseService;
    private readonly PasswordHasher _passwordHasher;

    // Cache schema info for Users table
    private HashSet<string>? _userColumns;
    private bool _columnsLoaded = false;

    public UserService(DatabaseService databaseService, PasswordHasher passwordHasher)
    {
        _databaseService = databaseService;
        _passwordHasher = passwordHasher;
    }

    private async Task EnsureUserColumnsLoadedAsync(SqlConnection connection)
    {
        if (_columnsLoaded && _userColumns != null)
            return;

        // Expect an open connection
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users'";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cols.Add(reader.GetString(0));
        }

        _userColumns = cols;
        _columnsLoaded = true;
    }

    private bool HasColumn(string name) => _userColumns != null && _userColumns.Contains(name);

    public async Task<bool> RegisterAsync(string email, string password, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await EnsureUserColumnsLoadedAsync(connection);

        // 1. Kiểm tra Email đã tồn tại chưa
        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
            checkCommand.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

            var exists = (int?)await checkCommand.ExecuteScalarAsync() ?? 0;
            if (exists > 0)
            {
                return false;
            }
        }

        // Tạo hash và salt
        var (hash, salt) = _passwordHasher.HashPassword(password);

        // Build INSERT based on available columns
        if (HasColumn("PasswordHash") && HasColumn("PasswordSalt"))
        {
            var cols = new List<string> { "Id", "Email", "PasswordHash", "PasswordSalt", "CreatedAt" };
            var vals = new List<string> { "@Id", "@Email", "@PasswordHash", "@PasswordSalt", "@CreatedAt" };
            if (HasColumn("DisplayName"))
            {
                cols.Insert(cols.Count - 1, "DisplayName");
                vals.Insert(vals.Count - 1, "@DisplayName");
            }

            var insertSql = $"INSERT INTO Users ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = insertSql;
            insertCommand.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            insertCommand.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
            insertCommand.Parameters.AddWithValue("@PasswordHash", hash);
            insertCommand.Parameters.AddWithValue("@PasswordSalt", salt);
            if (HasColumn("DisplayName"))
                insertCommand.Parameters.AddWithValue("@DisplayName", (object?)displayName ?? string.Empty);
            insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            return await insertCommand.ExecuteNonQueryAsync() == 1;
        }
        else if (HasColumn("Password"))
        {
            // Store combined salt|hash in single Password column for backward compatibility
            var combined = $"{salt}|{hash}";
            var cols = new List<string> { "Id", "Email", "Password", "CreatedAt" };
            var vals = new List<string> { "@Id", "@Email", "@Password", "@CreatedAt" };
            if (HasColumn("DisplayName"))
            {
                cols.Insert(cols.Count - 1, "DisplayName");
                vals.Insert(vals.Count - 1, "@DisplayName");
            }

            var insertSql = $"INSERT INTO Users ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = insertSql;
            insertCommand.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            insertCommand.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
            insertCommand.Parameters.AddWithValue("@Password", combined);
            if (HasColumn("DisplayName"))
                insertCommand.Parameters.AddWithValue("@DisplayName", (object?)displayName ?? string.Empty);
            insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            return await insertCommand.ExecuteNonQueryAsync() == 1;
        }
        else
        {
            // Unexpected schema: try to insert minimal columns (Id, Email, CreatedAt) if possible
            var cols = new List<string> { "Id", "Email", "CreatedAt" };
            var vals = new List<string> { "@Id", "@Email", "@CreatedAt" };
            var insertSql = $"INSERT INTO Users ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = insertSql;
            insertCommand.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            insertCommand.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
            insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            return await insertCommand.ExecuteNonQueryAsync() == 1;
        }
    }

    public async Task<UserAccount?> AuthenticateAsync(string email, string password)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await EnsureUserColumnsLoadedAsync(connection);

        // Build SELECT list dynamically
        var selectCols = new List<string> { "Id", "Email" };
        bool hasHashSalt = HasColumn("PasswordHash") && HasColumn("PasswordSalt");
        bool hasSinglePassword = HasColumn("Password");
        if (hasHashSalt)
        {
            selectCols.Add("PasswordHash");
            selectCols.Add("PasswordSalt");
        }
        else if (hasSinglePassword)
        {
            selectCols.Add("Password");
        }

        if (HasColumn("DisplayName")) selectCols.Add("DisplayName");
        if (HasColumn("CreatedAt")) selectCols.Add("CreatedAt");
        if (HasColumn("LastLoginAt")) selectCols.Add("LastLoginAt");

        var sql = $"SELECT {string.Join(", ", selectCols)} FROM Users WHERE Email = @Email";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var idx = 0;
            var idStr = reader.IsDBNull(idx) ? string.Empty : reader.GetString(idx); idx++;
            var emailStr = reader.IsDBNull(idx) ? string.Empty : reader.GetString(idx); idx++;

            string storedHash = string.Empty;
            string storedSalt = string.Empty;
            string storedSinglePassword = string.Empty;

            if (hasHashSalt)
            {
                storedHash = reader.IsDBNull(idx) ? string.Empty : reader.GetString(idx); idx++;
                storedSalt = reader.IsDBNull(idx) ? string.Empty : reader.GetString(idx); idx++;
            }
            else if (hasSinglePassword)
            {
                storedSinglePassword = reader.IsDBNull(idx) ? string.Empty : reader.GetString(idx); idx++;
            }

            string? displayName = null;
            DateTime createdAt = DateTime.MinValue;
            DateTime? lastLogin = null;

            if (HasColumn("DisplayName"))
            {
                displayName = reader.IsDBNull(idx) ? null : reader.GetString(idx);
                idx++;
            }
            if (HasColumn("CreatedAt"))
            {
                createdAt = reader.IsDBNull(idx) ? DateTime.MinValue : reader.GetDateTime(idx);
                idx++;
            }
            if (HasColumn("LastLoginAt"))
            {
                lastLogin = reader.IsDBNull(idx) ? null : (DateTime?)reader.GetDateTime(idx);
                idx++;
            }

            bool passwordOk = false;
            if (hasHashSalt)
            {
                passwordOk = _passwordHasher.Verify(password, storedHash, storedSalt);
            }
            else if (hasSinglePassword)
            {
                if (!string.IsNullOrEmpty(storedSinglePassword) && storedSinglePassword.Contains('|'))
                {
                    var parts = storedSinglePassword.Split('|', 2);
                    var salt = parts[0];
                    var hash = parts.Length > 1 ? parts[1] : string.Empty;
                    passwordOk = _passwordHasher.Verify(password, hash, salt);
                }
                else
                {
                    // Unsupported stored format
                    passwordOk = false;
                }
            }

            if (passwordOk)
            {
                var account = new UserAccount
                {
                    Id = string.IsNullOrEmpty(idStr) ? Guid.Empty : Guid.TryParse(idStr, out var parsedId) ? parsedId : Guid.Empty,
                    Email = emailStr,
                    PasswordHash = hasHashSalt ? storedHash : storedSinglePassword,
                    PasswordSalt = hasHashSalt ? storedSalt : string.Empty,
                    DisplayName = displayName,
                    CreatedAt = createdAt,
                    LastLoginAt = lastLogin
                };

                // Đóng reader trước khi cập nhật thời gian đăng nhập
                await reader.DisposeAsync();

                // Update last login using a new, separate connection to avoid reader conflicts
                await UpdateLastLoginAsync(account.Id);
                return account;
            }
        }

        return null;
    }

    // Hàm cập nhật thời gian đăng nhập lần cuối
    private async Task UpdateLastLoginAsync(Guid userId)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        // Check whether LastLoginAt column exists to avoid SQL errors
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LastLoginAt'";
            var exists = (int?)await checkCmd.ExecuteScalarAsync() ?? 0;
            if (exists == 0)
            {
                return; // column missing, nothing to update
            }
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = "UPDATE Users SET LastLoginAt = @LastLoginAt WHERE Id = @Id";

        updateCommand.Parameters.AddWithValue("@LastLoginAt", DateTime.UtcNow);
        updateCommand.Parameters.AddWithValue("@Id", userId.ToString());
        await updateCommand.ExecuteNonQueryAsync();
    }

    // CHỨC NĂNG MỚI: CHỈ KIỂM TRA EMAIL CÓ TỒN TẠI TRONG BẢNG USERS HAY KHÔNG
    // Dùng để xác nhận trước khi gửi OTP (In-memory)
    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await EnsureUserColumnsLoadedAsync(connection);

        await using var checkUserCommand = connection.CreateCommand();
        checkUserCommand.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
        checkUserCommand.Parameters.AddWithValue("@Email", normalizedEmail);

        var exists = (int?)await checkUserCommand.ExecuteScalarAsync() ?? 0;
        return exists > 0;
    }

    // CHỨC NĂNG CÒN LẠI: Cập nhật mật khẩu sau khi OTP đã được xác minh (in-memory)
    public async Task<bool> UpdatePasswordAsync(string email, string newPassword)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var (hash, salt) = _passwordHasher.HashPassword(newPassword);

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();

        await EnsureUserColumnsLoadedAsync(connection);

        if (HasColumn("PasswordHash") && HasColumn("PasswordSalt"))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET PasswordHash = @Hash, PasswordSalt = @Salt WHERE Email = @Email";
            command.Parameters.AddWithValue("@Hash", hash);
            command.Parameters.AddWithValue("@Salt", salt);
            command.Parameters.AddWithValue("@Email", normalizedEmail);
            return await command.ExecuteNonQueryAsync() == 1;
        }
        else if (HasColumn("Password"))
        {
            var combined = $"{salt}|{hash}";
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET Password = @Password WHERE Email = @Email";
            command.Parameters.AddWithValue("@Password", combined);
            command.Parameters.AddWithValue("@Email", normalizedEmail);
            return await command.ExecuteNonQueryAsync() == 1;
        }

        return false;
    }
}
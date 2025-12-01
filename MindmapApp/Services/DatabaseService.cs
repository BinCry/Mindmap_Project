using System;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;
// Đảm bảo bạn đã cài đặt gói NuGet này
using System.Windows;

namespace MindmapApp.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    // Constructor nhận Chuỗi Kết nối Server
    public DatabaseService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Invalid connection string", nameof(connectionString));
        }

        _connectionString = connectionString;

        // Khởi tạo: kiểm tra kết nối và thực hiện các sửa schema an toàn nếu cần (migrate nhẹ)
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqlConnection(_connectionString);
        try
        {
            connection.Open();

            // Kiểm tra xem bảng Users có tồn tại không
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users' AND TABLE_SCHEMA = 'dbo'
";
                var exists = (int)cmd.ExecuteScalar() > 0;
                if (!exists)
                {
                    // Nếu không có table Users, không làm gì (bạn đã nói DB đã tạo sẵn)
                    return;
                }
            }

            // Lấy danh sách cột hiện có
            var columns = new System.Collections.Generic.Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND TABLE_SCHEMA = 'dbo'";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    int? maxLen = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                    columns[name] = maxLen;
                }
            }

            // Nếu có cột Password và kích thước của nó nhỏ (ví dụ varchar(50)), mở rộng lên NVARCHAR(MAX)
            if (columns.ContainsKey("Password"))
            {
                var len = columns["Password"];
                // CHARACTER_MAXIMUM_LENGTH = -1 nghĩa là MAX, null hoặc nhỏ hơn 256 có thể gây truncate
                if (len == null || (len.HasValue && len > 0 && len < 256))
                {
                    try
                    {
                        using var alterCmd = connection.CreateCommand();
                        alterCmd.CommandText = "ALTER TABLE dbo.Users ALTER COLUMN Password NVARCHAR(MAX) NULL";
                        alterCmd.ExecuteNonQuery();
                        // update local cache
                        columns["Password"] = -1;
                    }
                    catch (SqlException)
                    {
                        // nếu không thể alter, bỏ qua nhưng log thông báo
                        // không throw để app vẫn khởi động; hiển thị thông báo nhẹ
                        MessageBox.Show("Không thể mở rộng cột 'Password'. Vui lòng kiểm tra quyền của tài khoản DB.", "Database Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            // Nếu thiếu PasswordHash hoặc PasswordSalt, thêm cột NVARCHAR(MAX) NULL
            var addedHashSalt = false;
            if (!columns.ContainsKey("PasswordHash"))
            {
                using var addHash = connection.CreateCommand();
                addHash.CommandText = "ALTER TABLE dbo.Users ADD PasswordHash NVARCHAR(MAX) NULL";
                addHash.ExecuteNonQuery();
                columns["PasswordHash"] = -1;
                addedHashSalt = true;
            }

            if (!columns.ContainsKey("PasswordSalt"))
            {
                using var addSalt = connection.CreateCommand();
                addSalt.CommandText = "ALTER TABLE dbo.Users ADD PasswordSalt NVARCHAR(MAX) NULL";
                addSalt.ExecuteNonQuery();
                columns["PasswordSalt"] = -1;
                addedHashSalt = true;
            }

            // Nếu vừa thêm PasswordHash/PasswordSalt và tồn tại dữ liệu cũ trong Password, migrate các giá trị dạng 'salt|hash' vào hai cột mới
            if (addedHashSalt && columns.ContainsKey("Password"))
            {
                try
                {
                    using var migrateCmd = connection.CreateCommand();
                    migrateCmd.CommandText = @"
UPDATE dbo.Users
SET PasswordSalt = CASE WHEN CHARINDEX('|', Password) > 0 THEN LEFT(Password, CHARINDEX('|',Password)-1) ELSE '' END,
    PasswordHash = CASE WHEN CHARINDEX('|', Password) > 0 THEN SUBSTRING(Password, CHARINDEX('|',Password)+1, LEN(Password)) ELSE Password END
WHERE Password IS NOT NULL
";
                    migrateCmd.ExecuteNonQuery();
                }
                catch (SqlException)
                {
                    // nếu migration thất bại thì chỉ cảnh báo
                    MessageBox.Show("Không thể migrate dữ liệu từ cột 'Password' sang 'PasswordHash'/'PasswordSalt'. Vui lòng kiểm tra thủ công.", "Database Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Optionally, you could set PasswordHash/PasswordSalt to NOT NULL with defaults, but avoid automatic destructive changes here.
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"SQL Server Connection Error: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    // Trả về đối tượng SqlConnection
    public SqlConnection GetConnection() => new(_connectionString);
}
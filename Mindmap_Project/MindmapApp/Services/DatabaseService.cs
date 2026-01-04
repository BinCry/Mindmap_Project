using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Windows;

namespace MindmapApp.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _databasePath; // Đường dẫn đến file .db

    // Constructor nhận Chuỗi Kết nối SQLite
    public DatabaseService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Invalid connection string", nameof(connectionString));
        }

        _connectionString = connectionString;

        // Trích xuất đường dẫn file từ Connection String
        _databasePath = connectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();

        // Khởi tạo: kiểm tra file và tạo database/schema nếu cần
        Initialize();
    }

    private void Initialize()
    {
        // 1. Kiểm tra xem file database đã tồn tại chưa
        if (!File.Exists(_databasePath))
        {
            MessageBox.Show("Khởi tạo database mới: mindmap.db. Quá trình này chỉ xảy ra một lần.", "Database Setup", MessageBoxButton.OK, MessageBoxImage.Information);

            try
            {
                // Mở kết nối và tạo schema (file .db sẽ được tạo)
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // 2. Định nghĩa SQL Schema (CREATE TABLE) cho SQLite
                // ✨ CẬP NHẬT: Thêm cột IsPro (INTEGER) mặc định là 0 (False)
                var createUsersTable = @"
                    CREATE TABLE Users (
                        Id TEXT PRIMARY KEY NOT NULL,
                        Email TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        PasswordSalt TEXT NOT NULL,
                        DisplayName TEXT,
                        CreatedAt TEXT NOT NULL,
                        LastLoginAt TEXT,
                        IsPro INTEGER DEFAULT 0
                    );";

                var createMindmapsTable = @"
                    CREATE TABLE MindmapDocuments (
                        Id TEXT PRIMARY KEY NOT NULL,
                        UserId TEXT NOT NULL,
                        Title TEXT,
                        Content TEXT, -- Lưu dữ liệu MindmapDocument (Nodes/Connections) dưới dạng JSON
                        UpdatedAt TEXT NOT NULL,
                        FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                    );
                    -- Index giúp truy vấn nhanh các Mindmap theo User
                    CREATE INDEX idx_MindmapDocuments_UserId_UpdatedAt ON MindmapDocuments (UserId, UpdatedAt DESC);
                ";

                // 3. Thực thi các lệnh tạo bảng
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = createUsersTable;
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = createMindmapsTable;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Xử lý lỗi nếu không thể tạo database (Ví dụ: thiếu quyền ghi)
                MessageBox.Show($"Lỗi khởi tạo database SQLite: {ex.Message}. Ứng dụng sẽ đóng.", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
        else
        {
            // ✨ 4. MIGRATION: Nếu DB đã tồn tại, tự động thêm cột IsPro nếu thiếu
            // Giúp bạn không cần xoá file .db cũ
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE Users ADD COLUMN IsPro INTEGER DEFAULT 0;";
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    // Nếu cột đã tồn tại (lỗi duplicate column), ta bỏ qua
                }
            }
            catch (Exception)
            {
                // Bỏ qua các lỗi kết nối khác để không làm phiền user
            }
        }
    }

    // Trả về đối tượng SqliteConnection
    public SqliteConnection GetConnection() => new(_connectionString);
}
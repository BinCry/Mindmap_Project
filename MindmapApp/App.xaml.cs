using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using MindmapApp.Services;

namespace MindmapApp;

public partial class App : Application
{
    // KHÔNG SỬ DỤNG CÁC BIẾN CỤC BỘ DỰA TRÊN FILE APPDATA NỮA, 
    // CHỈ SỬ DỤNG CHO SETTINGS FILE (settings.json)
    private static readonly string AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindmapApp");
    private static readonly string SettingsFile = Path.Combine(AppDataDirectory, "settings.json");

    // Thay đổi Chuỗi Kết nối SQL Server tại đây
    // CHÚ Ý: ĐÂY LÀ CHUỖI KẾT NỐI MẪU DÙNG SQL SERVER LOCALDB.
    // Nếu bạn dùng máy chủ thực, hãy thay thế bằng thông tin Server/User/Pass của bạn.

    private const string SqlServerConnectionString = "Server=LAPTOP-Q5S38J6S\\SQLEXPRESS;Database=MindMapApp;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;MultipleActiveResultSets=True;";


    public static DatabaseService DatabaseService { get; private set; } = null!;
    public static UserService UserService { get; private set; } = null!;
    public static EmailService EmailService { get; private set; } = null!;
    public static EmailSettings EmailSettings { get; private set; } = new();
    public static MindmapExportService ExportService { get; private set; } = null!;
    public static MindmapSearchService SearchService { get; private set; } = null!;
    public static MindmapAiService AiService { get; private set; } = null!;
    public static MindmapStorageService MindmapStorageService { get; private set; } = null!;
    public static string GoogleAiApiKey { get; private set; } = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Vẫn tạo thư mục AppData cho file settings.json
        if (!Directory.Exists(AppDataDirectory))
        {
            Directory.CreateDirectory(AppDataDirectory);
        }

        // KHÔNG CẦN DÒNG NÀY NỮA: var databasePath = Path.Combine(AppDataDirectory, "mindmap.db");

        // 1. Sử dụng Connection String của SQL Server để khởi tạo DatabaseService
        DatabaseService = new DatabaseService(SqlServerConnectionString);

        var passwordHasher = new PasswordHasher();
        UserService = new UserService(DatabaseService, passwordHasher);

        EmailSettings = LoadEmailSettings();
        EmailService = new EmailService(EmailSettings);
        ExportService = new MindmapExportService();
        SearchService = new MindmapSearchService();
        AiService = new MindmapAiService(new HttpClient());
        MindmapStorageService = new MindmapStorageService(DatabaseService);
        GoogleAiApiKey = LoadGoogleApiKey();
    }

    private static EmailSettings LoadEmailSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    GoogleAiApiKey = settings.GoogleAiApiKey ?? string.Empty;
                    if (settings.Email != null && !string.IsNullOrWhiteSpace(settings.Email.SmtpHost) && !string.IsNullOrWhiteSpace(settings.Email.SenderEmail))
                    {
                        return settings.Email;
                    }
                }
            }
        }
        catch
        {
            // ignore invalid settings file
        }

        // Fallback to environment variables if settings.json missing or incomplete
        var smtpHost = Environment.GetEnvironmentVariable("MINDMAP_SMTP_HOST");
        var senderEmail = Environment.GetEnvironmentVariable("MINDMAP_SENDER_EMAIL");
        if (!string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(senderEmail))
        {
            var smtpPortStr = Environment.GetEnvironmentVariable("MINDMAP_SMTP_PORT");
            int smtpPort = 587;
            if (!string.IsNullOrWhiteSpace(smtpPortStr) && int.TryParse(smtpPortStr, out var p)) smtpPort = p;

            var useSslStr = Environment.GetEnvironmentVariable("MINDMAP_USE_SSL");
            bool useSsl = true;
            if (!string.IsNullOrWhiteSpace(useSslStr) && bool.TryParse(useSslStr, out var b)) useSsl = b;

            var senderName = Environment.GetEnvironmentVariable("MINDMAP_SENDER_NAME") ?? "MindmapApp";
            var senderPassword = Environment.GetEnvironmentVariable("MINDMAP_SENDER_PASSWORD") ?? string.Empty;

            return new EmailSettings
            {
                SmtpHost = smtpHost,
                SmtpPort = smtpPort,
                UseSsl = useSsl,
                SenderEmail = senderEmail,
                SenderName = senderName,
                SenderPassword = senderPassword
            };
        }

        return new EmailSettings();
    }

    private static string LoadGoogleApiKey()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    return settings.GoogleAiApiKey ?? string.Empty;
                }
            }
        }
        catch
        {
            // ignore invalid settings
        }

        // fallback to environment variable
        var key = Environment.GetEnvironmentVariable("MINDMAP_GOOGLE_API_KEY");
        return key ?? string.Empty;
    }

    private class AppSettings
    {
        public EmailSettings? Email { get; set; }
        public string? GoogleAiApiKey { get; set; }
    }
}
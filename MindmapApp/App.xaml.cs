
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using MindmapApp.Services;

namespace MindmapApp;

public partial class App : Application
{
    private static readonly string AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindmapApp");
    private static readonly string SettingsFile = Path.Combine(AppDataDirectory, "settings.json");

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

        if (!Directory.Exists(AppDataDirectory))
        {
            Directory.CreateDirectory(AppDataDirectory);
        }

        var databasePath = Path.Combine(AppDataDirectory, "mindmap.db");
        DatabaseService = new DatabaseService(databasePath);
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
                if (settings != null && settings.Email != null)
                {
                    GoogleAiApiKey = settings.GoogleAiApiKey ?? string.Empty;
                    return settings.Email;
                }
            }
        }
        catch
        {
            // ignore invalid settings file
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

        return string.Empty;
    }

    private class AppSettings
    {
        public EmailSettings? Email { get; set; }
        public string? GoogleAiApiKey { get; set; }
    }
}

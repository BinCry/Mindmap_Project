using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MindmapApp.Services;
using MindMapApp.Properties; // Namespace này phải khớp với file Settings của bạn

namespace MindmapApp
{
    public partial class App : Application
    {
        private static readonly string AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindmapApp");
        private static readonly string SettingsFile = Path.Combine(AppDataDirectory, "settings.json");
        private static readonly string DatabaseFile = Path.Combine(AppDataDirectory, "mindmap.db");
        private static readonly string SqliteConnectionString = "Data Source=" + DatabaseFile;

        public static DatabaseService DatabaseService { get; private set; } = null!;
        public static UserService UserService { get; private set; } = null!;
        public static EmailService EmailService { get; private set; } = null!;
        public static EmailSettings EmailSettings { get; private set; } = new();
        public static MindmapExportService ExportService { get; private set; } = null!;
        public static MindmapSearchService SearchService { get; private set; } = null!;
        public static MindmapAiService AiService { get; private set; } = null!;
        public static MindmapStorageService MindmapStorageService { get; private set; } = null!;
        public static string GoogleAiApiKey { get; private set; } = string.Empty;

        // --- KHỞI ĐỘNG ---
        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Áp dụng Theme ĐÃ LƯU ngay lập tức để tránh bị nháy giao diện
            ApplyTheme(Settings.Default.IsDarkMode);

            base.OnStartup(e);

            // 2. Khởi tạo các thư mục và Service
            if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
            DatabaseService = new DatabaseService(SqliteConnectionString);
            UserService = new UserService(DatabaseService, new PasswordHasher());
            EmailSettings = LoadEmailSettings();
            EmailService = new EmailService(EmailSettings);
            ExportService = new MindmapExportService();
            SearchService = new MindmapSearchService();
            AiService = new MindmapAiService(new HttpClient());
            MindmapStorageService = new MindmapStorageService(DatabaseService);
            GoogleAiApiKey = LoadGoogleApiKey();
        }

        // --- LOGIC ĐỔI THEME ---
        public void ToggleTheme()
        {
            bool newMode = !Settings.Default.IsDarkMode;

            // Lưu trạng thái mới
            Settings.Default.IsDarkMode = newMode;
            Settings.Default.Save();

            // Áp dụng ngay lập tức
            ApplyTheme(newMode);
        }

        private void ApplyTheme(bool isDark)
        {
            var themeDict = GetThemeDictionary();
            if (themeDict != null)
            {
                if (isDark)
                {
                    // Dark Mode
                    UpdateBrush(themeDict, "BackgroundBrush", "#101720");
                    UpdateBrush(themeDict, "SurfaceBrush", "#182433");
                    UpdateBrush(themeDict, "TextBrush", "#FFEAF3FF");
                    UpdateBrush(themeDict, "PrimaryBrush", "#FF4E89AE");
                    UpdateGridBrush(themeDict, "GridBackgroundBrush", "#141414", "#282828");
                }
                else
                {
                    // Light Mode
                    UpdateBrush(themeDict, "BackgroundBrush", "#FFE2E6F0");
                    UpdateBrush(themeDict, "SurfaceBrush", "#FFF7F8FD");
                    UpdateBrush(themeDict, "TextBrush", "#FF273C4E");
                    UpdateBrush(themeDict, "PrimaryBrush", "#FF4E89AE");
                    UpdateGridBrush(themeDict, "GridBackgroundBrush", "#FFDDE2F0", "#FFBEC6DB");
                }
            }
        }

        private ResourceDictionary? GetThemeDictionary()
        {
            var dict = Current.Resources.MergedDictionaries.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

            if (dict == null && Current.Resources.MergedDictionaries.Count > 0)
            {
                if (Current.Resources.MergedDictionaries[0].MergedDictionaries.Count > 0)
                    return Current.Resources.MergedDictionaries[0].MergedDictionaries[0];
                return Current.Resources.MergedDictionaries[0];
            }
            return dict;
        }

        private static void UpdateBrush(ResourceDictionary dict, string key, string colorHex)
        {
            if (dict.Contains(key)) dict[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private static void UpdateGridBrush(ResourceDictionary dict, string key, string backColorHex, string lineColorHex)
        {
            if (dict.Contains(key)) dict[key] = CreateGridBrush(backColorHex, lineColorHex);
        }

        private static DrawingBrush CreateGridBrush(string backColorHex, string lineColorHex)
        {
            var brush = new DrawingBrush
            {
                Viewport = new Rect(0, 0, 25, 25),
                ViewportUnits = BrushMappingMode.Absolute,
                TileMode = TileMode.Tile
            };
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing
            {
                Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backColorHex)),
                Geometry = new RectangleGeometry(new Rect(0, 0, 25, 25))
            });
            var lineGeo = new GeometryGroup();
            lineGeo.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 25)));
            lineGeo.Children.Add(new LineGeometry(new Point(0, 0), new Point(25, 0)));
            group.Children.Add(new GeometryDrawing
            {
                Pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(lineColorHex)), 0.5),
                Geometry = lineGeo
            });
            brush.Drawing = group;
            return brush;
        }

        private static EmailSettings LoadEmailSettings()
        {
            try { if (File.Exists(SettingsFile)) { var json = File.ReadAllText(SettingsFile); var settings = JsonSerializer.Deserialize<AppSettings>(json); if (settings?.Email != null) return settings.Email; } } catch { }
            return new EmailSettings();
        }

        private static string LoadGoogleApiKey()
        {
            try { if (File.Exists(SettingsFile)) { var json = File.ReadAllText(SettingsFile); var settings = JsonSerializer.Deserialize<AppSettings>(json); return settings?.GoogleAiApiKey ?? string.Empty; } } catch { }
            return string.Empty;
        }

        private class AppSettings { public EmailSettings? Email { get; set; } public string? GoogleAiApiKey { get; set; } }
    }
}
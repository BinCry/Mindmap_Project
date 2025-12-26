using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using MindmapApp.Models;
using Microsoft.Data.Sqlite; // Sử dụng thư viện SQLite

namespace MindmapApp.Services;

public class MindmapStorageService
{
    private readonly DatabaseService _databaseService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MindmapStorageService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<MindmapDocument> LoadOrCreateAsync(Guid userId, string defaultTitle)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        // SQLite: Sử dụng LIMIT 1 thay cho TOP(1)
        command.CommandText = "SELECT Id, Title, Content FROM MindmapDocuments WHERE UserId = @userId ORDER BY UpdatedAt DESC LIMIT 1";
        command.Parameters.AddWithValue("@userId", userId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            // Logic đọc dữ liệu
            var documentId = Guid.Parse(reader.GetString(0));
            var title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var content = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

            // Đảm bảo các lớp StoredDocument/StoredNode/StoredConnection đã được thêm
            var stored = JsonSerializer.Deserialize<StoredDocument>(content, _jsonOptions) ?? new StoredDocument();
            return ToMindmapDocument(stored, documentId, userId, title);
        }

        // Logic tạo Mindmap mới nếu không có
        var document = new MindmapDocument
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = defaultTitle
        };
        await SaveDocumentAsync(document);
        return document;
    }

    public async Task SaveDocumentAsync(MindmapDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (document.OwnerId == Guid.Empty)
        {
            throw new InvalidOperationException("MindmapDocument cần có OwnerId để lưu trữ");
        }

        var stored = FromMindmapDocument(document);
        var json = JsonSerializer.Serialize(stored, _jsonOptions);

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        // SQLite: Sử dụng INSERT OR REPLACE cho cơ chế Upsert
        command.CommandText = @"
            INSERT OR REPLACE INTO MindmapDocuments (Id, UserId, Title, Content, UpdatedAt)
            VALUES (@id, @userId, @title, @content, @updatedAt);";

        command.Parameters.AddWithValue("@id", document.Id.ToString());
        command.Parameters.AddWithValue("@userId", document.OwnerId.ToString());
        command.Parameters.AddWithValue("@title", document.Title);
        command.Parameters.AddWithValue("@content", json);
        // Lưu DateTime dưới dạng ISO 8601 TEXT 
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static MindmapDocument ToMindmapDocument(StoredDocument stored, Guid id, Guid ownerId, string title)
    {
        var document = new MindmapDocument
        {
            Id = id,
            OwnerId = ownerId,
            Title = title,
            CanvasBackgroundColor = FromHex(stored.CanvasBackgroundColor),
            CanvasGridStyle = stored.CanvasGridStyle ?? "Light"
        };

        foreach (var node in stored.Nodes)
        {
            document.Nodes.Add(new NodeModel
            {
                Id = node.Id,
                Title = node.Title,
                Description = node.Description,
                ContentXaml = node.ContentXaml ?? string.Empty,
                X = node.X,
                Y = node.Y,
                Width = node.Width,
                Height = node.Height,
                Shape = node.Shape,
                BackgroundColor = FromHex(node.BackgroundColor),
                BorderColor = FromHex(node.BorderColor),
                TextColor = FromHex(node.TextColor),
                BackgroundGridStyle = node.BackgroundGridStyle ?? "None",
                FontSize = node.FontSize,
                FontFamily = node.FontFamily,
                Tags = new ObservableCollection<string>(node.Tags ?? new List<string>())

            });
        }

        foreach (var connection in stored.Connections)
        {
            document.Connections.Add(new ConnectionModel
            {
                Id = connection.Id,
                SourceId = connection.SourceId,
                TargetId = connection.TargetId,
                StrokeColor = FromHex(connection.StrokeColor),
                Thickness = connection.Thickness,
                IsCurved = connection.IsCurved,
                DashOffset = connection.DashOffset,
                DashArray = connection.DashArray != null ? new DoubleCollection(connection.DashArray) : null,
                ArrowStyle = connection.ArrowStyle ?? "None"
            });
        }

        return document;
    }

    private static StoredDocument FromMindmapDocument(MindmapDocument document)
    {
        var stored = new StoredDocument
        {
            Id = document.Id,
            CanvasBackgroundColor = ToHex(document.CanvasBackgroundColor),
            CanvasGridStyle = document.CanvasGridStyle,
            Nodes = document.Nodes.Select(n => new StoredNode
            {
                Id = n.Id,
                Title = n.Title,
                Description = n.Description,
                ContentXaml = n.ContentXaml,
                X = n.X,
                Y = n.Y,
                Width = n.Width,
                Height = n.Height,
                Shape = n.Shape,
                BackgroundColor = ToHex(n.BackgroundColor),
                BorderColor = ToHex(n.BorderColor),
                TextColor = ToHex(n.TextColor),
                BackgroundGridStyle = n.BackgroundGridStyle,
                FontSize = n.FontSize,
                FontFamily = n.FontFamily,
                Tags = n.Tags.ToList()
            }).ToList(),
            Connections = document.Connections.Select(c => new StoredConnection
            {
                Id = c.Id,
                SourceId = c.SourceId,
                TargetId = c.TargetId,
                StrokeColor = ToHex(c.StrokeColor),
                Thickness = c.Thickness,
                IsCurved = c.IsCurved,
                DashOffset = c.DashOffset,
                DashArray = c.DashArray?.ToArray(),
                ArrowStyle = c.ArrowStyle
            }).ToList()
        };

        return stored;
    }

    private static string ToHex(Color color) => color.ToString();

    private static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Colors.Transparent;
        }

        // Đảm bảo xử lý đúng cách, nếu ColorConverter không tìm thấy
        if (ColorConverter.ConvertFromString(hex) is Color color)
        {
            return color;
        }
        return Colors.Transparent; // Giá trị mặc định nếu chuyển đổi thất bại
    }

    // ✨ CÁC LỚP NỘI BỘ QUAN TRỌNG (Khắc phục lỗi biên dịch) ✨
    private class StoredDocument
    {
        public Guid Id { get; set; }
        public string CanvasBackgroundColor { get; set; } = ToHex(Colors.Transparent);
        public string? CanvasGridStyle { get; set; } = "Light";
        public List<StoredNode> Nodes { get; set; } = new();
        public List<StoredConnection> Connections { get; set; } = new();
    }

    private class StoredNode
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ContentXaml { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Shape { get; set; } = "RoundedRectangle";
        public string BackgroundColor { get; set; } = ToHex(Colors.White);
        public string BorderColor { get; set; } = ToHex(Colors.Black);
        public string TextColor { get; set; } = ToHex(Colors.Black);
        public string BackgroundGridStyle { get; set; } = "None";
        public double FontSize { get; set; }
        public string FontFamily { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
    }

    private class StoredConnection
    {
        public Guid Id { get; set; }
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public string StrokeColor { get; set; } = ToHex(Colors.Black);
        public double Thickness { get; set; }
        public bool IsCurved { get; set; }
        public double DashOffset { get; set; }
        public double[]? DashArray { get; set; }
        public string ArrowStyle { get; set; } = "None";
    }
}
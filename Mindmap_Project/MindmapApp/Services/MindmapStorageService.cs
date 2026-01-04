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

    public async Task<List<MindmapDocument>> GetAllMapsHeaderAsync(Guid userId)
    {
        var result = new List<MindmapDocument>();
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, Title, UpdatedAt, Content FROM MindmapDocuments WHERE UserId = @userId ORDER BY UpdatedAt DESC";
        command.Parameters.AddWithValue("@userId", userId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader.GetString(0));
            var title = reader.IsDBNull(1) ? "Không tên" : reader.GetString(1);
            var updatedAtStr = reader.IsDBNull(2) ? null : reader.GetString(2);
            var content = reader.IsDBNull(3) ? "{}" : reader.GetString(3);

            DateTime updatedAt = DateTime.UtcNow;
            if (DateTime.TryParse(updatedAtStr, out var d)) updatedAt = d;

            try
            {
                var stored = JsonSerializer.Deserialize<StoredDocument>(content, _jsonOptions);
                if (stored != null)
                {
                    result.Add(ToMindmapDocument(stored, id, userId, title, updatedAt));
                    continue;
                }
            }
            catch
            {
            }

            result.Add(new MindmapDocument
            {
                Id = id,
                OwnerId = userId,
                Title = title,
                UpdatedAt = updatedAt
            });
        }
        return result;
    }

    public async Task<MindmapDocument?> GetMapAsync(Guid docId)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, UserId, Title, Content, UpdatedAt FROM MindmapDocuments WHERE Id = @id";
        command.Parameters.AddWithValue("@id", docId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader.GetString(0));
            var userId = Guid.Parse(reader.GetString(1));
            var title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var content = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var updatedAtStr = reader.IsDBNull(4) ? null : reader.GetString(4);

            DateTime updatedAt = DateTime.UtcNow;
            if (DateTime.TryParse(updatedAtStr, out var d)) updatedAt = d;

            var stored = JsonSerializer.Deserialize<StoredDocument>(content, _jsonOptions) ?? new StoredDocument();
            return ToMindmapDocument(stored, id, userId, title, updatedAt);
        }
        return null;
    }

    public async Task<MindmapDocument> LoadOrCreateAsync(Guid userId, string defaultTitle)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, Title, Content, UpdatedAt FROM MindmapDocuments WHERE UserId = @userId ORDER BY UpdatedAt DESC LIMIT 1";
        command.Parameters.AddWithValue("@userId", userId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var documentId = Guid.Parse(reader.GetString(0));
            var title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var content = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var updatedAtStr = reader.IsDBNull(3) ? null : reader.GetString(3);

            DateTime updatedAt = DateTime.UtcNow;
            if (DateTime.TryParse(updatedAtStr, out var d)) updatedAt = d;

            var stored = JsonSerializer.Deserialize<StoredDocument>(content, _jsonOptions) ?? new StoredDocument();
            return ToMindmapDocument(stored, documentId, userId, title, updatedAt);
        }

        var document = new MindmapDocument
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = defaultTitle,
            UpdatedAt = DateTime.UtcNow
        };
        await SaveDocumentAsync(document);
        return document;
    }

    public async Task SaveDocumentAsync(MindmapDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (document.OwnerId == Guid.Empty) throw new InvalidOperationException("MindmapDocument cần có OwnerId để lưu trữ");

        document.UpdatedAt = DateTime.UtcNow; // Update timestamp
        var stored = FromMindmapDocument(document);
        var json = JsonSerializer.Serialize(stored, _jsonOptions);

        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            INSERT OR REPLACE INTO MindmapDocuments (Id, UserId, Title, Content, UpdatedAt)
            VALUES (@id, @userId, @title, @content, @updatedAt);";

        command.Parameters.AddWithValue("@id", document.Id.ToString());
        command.Parameters.AddWithValue("@userId", document.OwnerId.ToString());
        command.Parameters.AddWithValue("@title", document.Title);
        command.Parameters.AddWithValue("@content", json);
        command.Parameters.AddWithValue("@updatedAt", document.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static MindmapDocument ToMindmapDocument(StoredDocument stored, Guid id, Guid ownerId, string title, DateTime updatedAt)
    {
        var document = new MindmapDocument
        {
            Id = id,
            OwnerId = ownerId,
            Title = title,
            UpdatedAt = updatedAt,
            ShareCode = stored.ShareCode ?? string.Empty,
            CanvasBackgroundColor = FromHex(stored.CanvasBackgroundColor),
            CanvasGridStyle = stored.CanvasGridStyle ?? "Light"
        };
        // ... rest of method same as before just new property assigned above
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
            ShareCode = document.ShareCode,
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

    public string SerializeDocument(MindmapDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var stored = FromMindmapDocument(document);
        return JsonSerializer.Serialize(stored, _jsonOptions);
    }

    public MindmapDocument DeserializeDocument(string contentJson, Guid id, Guid ownerId, string title, DateTime updatedAt, string? shareCode)
    {
        var stored = JsonSerializer.Deserialize<StoredDocument>(contentJson, _jsonOptions) ?? new StoredDocument();
        stored.ShareCode ??= shareCode;
        return ToMindmapDocument(stored, id, ownerId, title, updatedAt);
    }
    
    // Helper methods and inner classes preserved...
    private static string ToHex(Color color) => color.ToString();
    private static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Colors.Transparent;
        if (ColorConverter.ConvertFromString(hex) is Color color) return color;
        return Colors.Transparent;
    }

    // 1. Xóa một Mindmap theo ID
    public async Task DeleteMapAsync(Guid mapId)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM MindmapDocuments WHERE Id = @id";
        command.Parameters.AddWithValue("@id", mapId.ToString());

        await command.ExecuteNonQueryAsync();
    }

    // 2. Đếm số lượng Map của User
    public async Task<int> GetMapCountAsync(Guid userId)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM MindmapDocuments WHERE UserId = @userId";
        command.Parameters.AddWithValue("@userId", userId.ToString());

        var result = await command.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    // 3. Lấy ID của Map cũ nhất (để gợi ý xóa khi đầy)
    public async Task<Guid?> GetOldestMapIdAsync(Guid userId)
    {
        await using var connection = _databaseService.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        // Lấy 1 dòng có UpdatedAt bé nhất (cũ nhất)
        command.CommandText = "SELECT Id FROM MindmapDocuments WHERE UserId = @userId ORDER BY UpdatedAt ASC LIMIT 1";
        command.Parameters.AddWithValue("@userId", userId.ToString());

        var result = await command.ExecuteScalarAsync();
        if (result != null && Guid.TryParse(result.ToString(), out Guid id))
        {
            return id;
        }
        return null;
    }
    // ✨ CÁC LỚP NỘI BỘ QUAN TRỌNG (Khắc phục lỗi biên dịch) ✨
    private class StoredDocument
    {
        public Guid Id { get; set; }
        public string? ShareCode { get; set; }
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

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using MindmapApp.Models;

namespace MindmapApp.Services;

public class MindmapAiService
{
    private readonly HttpClient _httpClient;

    public MindmapAiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MindmapDocument?> GenerateMindmapAsync(string topic, string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Bạn cần cấu hình API Key của Google Generative AI");
        }

        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={apiKey}";
        var prompt = $"Hãy tạo mindmap cho chủ đề '{topic}'. Hãy phản hồi ở dạng JSON với các nút: title, description, color (RGB hex) và connections (sourceTitle, targetTitle).";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(requestUri, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);
        var text = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var mindmapJson = text.Trim().Trim('`');
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var aiMindmap = JsonSerializer.Deserialize<AiMindmap>(mindmapJson, options);
        if (aiMindmap == null)
        {
            return null;
        }

        var documentModel = new MindmapDocument { Title = aiMindmap.Title ?? $"Mindmap về {topic}" };
        var nodeLookup = new Dictionary<string, NodeModel>();
        foreach (var node in aiMindmap.Nodes)
        {
            var color = (node.Color ?? "#E3F2FD").Trim();
            if (!color.StartsWith("#", StringComparison.Ordinal))
            {
                color = "#" + color;
            }

            var wpfColor = (Color)ColorConverter.ConvertFromString(color)!;
            var nodeModel = new NodeModel
            {
                Title = node.Title,
                Description = node.Description,
                BackgroundColor = wpfColor,
                X = node.X,
                Y = node.Y
            };
            documentModel.Nodes.Add(nodeModel);
            nodeLookup[node.Title] = nodeModel;
        }

        foreach (var connection in aiMindmap.Connections)
        {
            if (nodeLookup.TryGetValue(connection.SourceTitle, out var source) &&
                nodeLookup.TryGetValue(connection.TargetTitle, out var target))
            {
                documentModel.Connections.Add(new ConnectionModel
                {
                    SourceId = source.Id,
                    TargetId = target.Id
                });
            }
        }

        return documentModel;
    }

    private record AiMindmap(string? Title, AiMindmapNode[] Nodes, AiMindmapConnection[] Connections);
    private record AiMindmapNode(string Title, string? Description, string? Color, double X, double Y);
    private record AiMindmapConnection(string SourceTitle, string TargetTitle);
}

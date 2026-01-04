using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MindmapApp.Services;

public class MindmapCloudService
{
    private readonly HttpClient _httpClient;
    private static readonly Uri DefaultBaseUri = new Uri("http://localhost:5076/");

    public MindmapCloudService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = ResolveBaseUri(),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<string?> PublishMapAsync(MindmapPayload payload)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Map", payload);
        if (!response.IsSuccessStatusCode) return null;

        var data = await response.Content.ReadFromJsonAsync<ShareResponse>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return data?.ShareCode;
    }

    public async Task<MindmapPayload?> FetchSharedMapAsync(string code)
    {
        var response = await _httpClient.GetAsync($"api/Map/shared/{code}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MindmapPayload>();
    }

    private static Uri ResolveBaseUri()
    {
        var env = Environment.GetEnvironmentVariable("MINDMAP_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(env) && Uri.TryCreate(env, UriKind.Absolute, out var baseUri))
        {
            return baseUri.ToString().EndsWith("/") ? baseUri : new Uri(baseUri.ToString() + "/");
        }

        return DefaultBaseUri;
    }

    public record MindmapPayload(Guid Id, Guid OwnerId, string Title, string ContentJson, string ShareCode, DateTime UpdatedAt);

    private record ShareResponse(string? ShareCode);
}

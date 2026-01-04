using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using MindmapApp.Models;

namespace MindmapApp.Services;

public class SignalRService
{
    private HubConnection _connection;
    private readonly string _hubUrl;

    // --- CÁC EVENT ĐỂ UI LẮNG NGHE ---
    public event Action<string, double, double>? OnNodeMovedEvent;
    public event Action<string, string, string>? OnNodeLockedEvent;
    public event Action<string>? OnNodeUnlockedEvent;
    public event Action<string>? OnUserJoinedEvent;
    public event Action<NodeSyncDto>? OnNodeAddedEvent;
    public event Action<NodeSyncDto>? OnNodeUpdatedEvent;
    public event Action<ConnectionSyncDto>? OnConnectionAddedEvent;
    public event Action<ConnectionSyncDto>? OnConnectionUpdatedEvent;
    public event Action<string, string>? OnNodeTextUpdatedEvent;

    public SignalRService()
    {
        _hubUrl = ResolveHubUrl();
        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, double, double>("ReceiveNodeMoved", (nodeId, x, y) =>
        {
            OnNodeMovedEvent?.Invoke(nodeId, x, y);
        });

        _connection.On<string, string, string>("NodeLocked", (nodeId, email, color) =>
        {
            OnNodeLockedEvent?.Invoke(nodeId, email, color);
        });

        _connection.On<string>("NodeUnlocked", (nodeId) =>
        {
            OnNodeUnlockedEvent?.Invoke(nodeId);
        });

        _connection.On<string>("UserJoined", (email) =>
        {
            OnUserJoinedEvent?.Invoke(email);
        });

        _connection.On<NodeSyncDto>("ReceiveNodeAdded", (nodeData) =>
        {
            OnNodeAddedEvent?.Invoke(nodeData);
        });

        _connection.On<NodeSyncDto>("ReceiveNodeUpdated", (nodeData) =>
        {
            OnNodeUpdatedEvent?.Invoke(nodeData);
        });

        _connection.On<ConnectionSyncDto>("ReceiveConnection", (connData) =>
        {
            OnConnectionAddedEvent?.Invoke(connData);
        });

        _connection.On<ConnectionSyncDto>("ReceiveConnectionUpdated", (connData) =>
        {
            OnConnectionUpdatedEvent?.Invoke(connData);
        });

        _connection.On<string, string>("ReceiveTextUpdate", (nodeId, text) =>
        {
            OnNodeTextUpdatedEvent?.Invoke(nodeId, text);
        });
    }

    public async Task StartAsync()
    {
        if (_connection.State == HubConnectionState.Disconnected)
        {
            try { await _connection.StartAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SignalR Error: {ex.Message}"); }
        }
    }

    public async Task JoinMapGroup(string mapId, string userEmail)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("JoinMindmapGroup", mapId, userEmail);
    }

    public async Task SendMoveNode(string mapId, string nodeId, double x, double y)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("MoveNode", mapId, nodeId, x, y);
    }

    public async Task LockNode(string mapId, string nodeId, string userEmail, string color)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("LockNode", mapId, nodeId, userEmail, color);
    }

    public async Task UnlockNode(string mapId, string nodeId)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("UnlockNode", mapId, nodeId);
    }

    public async Task SendAddNode(string mapId, NodeSyncDto nodeData)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("AddNode", mapId, nodeData);
    }

    public async Task SendAddConnection(string mapId, ConnectionSyncDto connData)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("AddConnection", mapId, connData);
    }

    public async Task SendUpdateNode(string mapId, NodeSyncDto nodeData)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("UpdateNode", mapId, nodeData);
    }

    public async Task SendUpdateConnection(string mapId, ConnectionSyncDto connData)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("UpdateConnection", mapId, connData);
    }

    public async Task SendNodeTextUpdate(string mapId, string nodeId, string contentXaml)
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("UpdateNodeText", mapId, nodeId, contentXaml);
    }

    // 🔥 THÊM MỚI: Hàm ngắt kết nối
    public async Task StopAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
        }
    }

    private static string ResolveHubUrl()
    {
        var env = Environment.GetEnvironmentVariable("MINDMAP_HUB_URL");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var apiBase = Environment.GetEnvironmentVariable("MINDMAP_API_BASE_URL");
        var baseUri = string.IsNullOrWhiteSpace(apiBase) ? "http://localhost:5076/" : apiBase;
        if (!baseUri.EndsWith("/")) baseUri += "/";
        return $"{baseUri}mindmaphub";
    }
}

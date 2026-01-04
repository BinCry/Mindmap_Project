using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace MindmapAPI.Hubs;

public class MindmapHub : Hub
{
    // 1. Client gọi hàm này khi mở một Mindmap để vào "phòng" chung
    public async Task JoinMindmapGroup(string mindmapId, string userEmail)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, mindmapId);

        // Thông báo cho mọi người trong phòng biết có người mới vào để hiện Avatar
        await Clients.OthersInGroup(mindmapId).SendAsync("UserJoined", userEmail);
    }

    // 2. Khi A bắt đầu chạm vào Node (Khóa node để người khác không sửa đè)
    public async Task LockNode(string mindmapId, string nodeId, string userEmail, string userColor)
    {
        // Gửi thông báo cho người khác để hiện khung viền màu và tên người đang giữ
        await Clients.OthersInGroup(mindmapId).SendAsync("NodeLocked", nodeId, userEmail, userColor);
    }

    // 3. Khi A di chuyển node -> Server báo cho những người còn lại
    public async Task MoveNode(string mindmapId, string nodeId, double x, double y)
    {
        await Clients.OthersInGroup(mindmapId).SendAsync("ReceiveNodeMoved", nodeId, x, y);
    }

    // 4. Khi A thả Node ra (Mở khóa)
    public async Task UnlockNode(string mindmapId, string nodeId)
    {
        await Clients.OthersInGroup(mindmapId).SendAsync("NodeUnlocked", nodeId);
    }

    // 5. Khi A thêm node mới
    public async Task AddNode(string mindmapId, object nodeData)
    {
        await Clients.OthersInGroup(mindmapId).SendAsync("ReceiveNodeAdded", nodeData);
    }

    // 6. Khi A nối dây
    public async Task AddConnection(string mindmapId, object connData)
    {
        await Clients.OthersInGroup(mindmapId).SendAsync("ReceiveConnection", connData);
    }

    // 6.1 Khi A cập nhật node (màu, kích thước, font, ...)
    public async Task UpdateNode(string mindmapId, object nodeData)
    {
        await Clients.OthersInGroup(mindmapId).SendAsync("ReceiveNodeUpdated", nodeData);
    }

    // 6.2 Khi A cập nhật dây nối
    public async Task UpdateConnection(string mindmapId, object connData)
    {
        await Clients.OthersInGroup(mindmapId).SendAsync("ReceiveConnectionUpdated", connData);
    }

    // 7. Đồng bộ hóa nội dung văn bản khi đang gõ (Real-time Typing)
    public async Task UpdateNodeText(string mindmapId, string nodeId, string text)
    {
        await Clients.OthersInGroup(mindmapId).SendAsync("ReceiveTextUpdate", nodeId, text);
    }
}

using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace MindmapApp.Models;

public class MindmapDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; } = Guid.Empty;
    public string Title { get; set; } = "Mindmap không tên";

    // Thiết lập nền canvas toàn cục
    public Color CanvasBackgroundColor { get; set; } = Colors.Transparent;
    public string CanvasGridStyle { get; set; } = "Light"; // None / Light / Dense

    public ObservableCollection<NodeModel> Nodes { get; set; } = new();
    public ObservableCollection<ConnectionModel> Connections { get; set; } = new();
}

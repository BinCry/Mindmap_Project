using System;
using System.Collections.ObjectModel;

namespace MindmapApp.Models;

public class MindmapDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; } = Guid.Empty;
    public string Title { get; set; } = "Mindmap không tên";
    public ObservableCollection<NodeModel> Nodes { get; set; } = new();
    public ObservableCollection<ConnectionModel> Connections { get; set; } = new();
}

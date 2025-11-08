using System;
using System.Windows.Media;

namespace MindmapApp.Models;

public class ConnectionModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    public Color StrokeColor { get; set; } = Color.FromRgb(0x4E, 0x89, 0xAE);
    public double Thickness { get; set; } = 2.0;
    public bool IsCurved { get; set; } = true;
    public double DashOffset { get; set; }
    public DoubleCollection? DashArray { get; set; }
}

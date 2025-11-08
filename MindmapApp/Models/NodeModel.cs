using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace MindmapApp.Models;

public class NodeModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 220;
    public double Height { get; set; } = 120;
    public string Shape { get; set; } = "RoundedRectangle";
    public Color BackgroundColor { get; set; } = Color.FromRgb(0xE3, 0xF2, 0xFD);
    public Color BorderColor { get; set; } = Color.FromRgb(0x4E, 0x89, 0xAE);
    public Color TextColor { get; set; } = Color.FromRgb(0x27, 0x3C, 0x4E);
    public double FontSize { get; set; } = 16;
    public string FontFamily { get; set; } = "Segoe UI";
    public ObservableCollection<string> Tags { get; set; } = new();
}

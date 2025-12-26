using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace MindmapApp.Models
{
    public class NodeModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public string ContentXaml { get; set; } = string.Empty;

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 220;
        public double Height { get; set; } = 120;

        public string Shape { get; set; } = "RoundedRectangle";

        public Color BackgroundColor { get; set; } = Color.FromRgb(0xE3, 0xF2, 0xFD);
        public Color BorderColor { get; set; } = Color.FromRgb(0x4E, 0x89, 0xAE);
        public Color TextColor { get; set; } = Color.FromRgb(0x27, 0x3C, 0x4E);

        // Kiểu lưới nền cho node: None, Light, Dense
        public string BackgroundGridStyle { get; set; } = "None";

        public string FontWeight { get; set; } = "Normal";
        public double FontSize { get; set; } = 16;
        public string FontFamily { get; set; } = "Segoe UI";

        // --- CÁC THUỘC TÍNH ĐỊNH DẠNG (Quan trọng cho Export) ---
        public bool IsBold { get; set; } = false;
        public bool IsItalic { get; set; } = false;
        public bool IsUnderline { get; set; } = false;
        public bool IsStrikethrough { get; set; } = false;

        public ObservableCollection<string> Tags { get; set; } = new();

        public bool IsDraggable { get; set; } = true;
        public bool IsDeletable { get; set; } = true;
        public bool IsRoot { get; set; } = false;
    }
}
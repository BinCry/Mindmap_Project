using System;

namespace MindmapApp.Models;

public record NodeSyncDto(
    Guid Id,
    string Title,
    string ContentXaml,
    double X,
    double Y,
    double Width,
    double Height,
    string Shape,
    string BackgroundColor,
    string BorderColor,
    string TextColor,
    string BackgroundGridStyle,
    double FontSize,
    string FontFamily,
    bool IsBold,
    bool IsItalic,
    bool IsUnderline,
    bool IsStrikethrough
);

public record ConnectionSyncDto(
    Guid Id,
    Guid SourceId,
    Guid TargetId,
    string StrokeColor,
    double Thickness,
    bool IsCurved,
    double DashOffset,
    double[]? DashArray,
    string ArrowStyle
);

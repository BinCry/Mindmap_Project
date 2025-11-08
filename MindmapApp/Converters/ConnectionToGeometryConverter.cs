using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindmapApp.ViewModels;

namespace MindmapApp.Converters;

public class ConnectionToGeometryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return Geometry.Empty;
        }

        if (values[0] is not ConnectionViewModel connection || values[1] is not System.Collections.IEnumerable nodesEnumerable)
        {
            return Geometry.Empty;
        }

        var nodes = nodesEnumerable.Cast<NodeViewModel>();
        var source = nodes.FirstOrDefault(n => n.Id == connection.SourceId);
        var target = nodes.FirstOrDefault(n => n.Id == connection.TargetId);
        if (source == null || target == null)
        {
            return Geometry.Empty;
        }

        var start = new Point(source.X + source.Width / 2, source.Y + source.Height / 2);
        var end = new Point(target.X + target.Width / 2, target.Y + target.Height / 2);

        var controlOffset = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) / 2;
        var controlPoint1 = new Point(start.X + controlOffset, start.Y);
        var controlPoint2 = new Point(end.X - controlOffset, end.Y);

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new BezierSegment(controlPoint1, controlPoint2, end, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

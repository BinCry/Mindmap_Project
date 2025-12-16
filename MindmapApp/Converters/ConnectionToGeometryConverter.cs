using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MindmapApp.Converters
{
    public class ConnectionToGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Kiểm tra đủ 8 tham số (X, Y, Width, Height của Source và Target)
            if (values.Length < 8 || values[0] == DependencyProperty.UnsetValue)
                return Geometry.Empty;

            try
            {
                double sx = (double)values[0];
                double sy = (double)values[1];
                double sw = (double)values[2];
                double sh = (double)values[3];

                double tx = (double)values[4];
                double ty = (double)values[5];
                double tw = (double)values[6];
                double th = (double)values[7];

                // Tính tâm điểm
                Point start = new Point(sx + sw / 2, sy + sh / 2);
                Point end = new Point(tx + tw / 2, ty + th / 2);

                // Vẽ đường cong Bezier
                PathGeometry geometry = new PathGeometry();
                PathFigure figure = new PathFigure { StartPoint = start };

                double deltaX = Math.Abs(end.X - start.X) / 2;
                Point p1 = new Point(start.X + deltaX, start.Y); // Điểm điều khiển 1
                Point p2 = new Point(end.X - deltaX, end.Y);     // Điểm điều khiển 2

                // Nếu muốn cong kiểu cây (Tree style):
                // Point p1 = new Point(start.X, start.Y + 50);
                // Point p2 = new Point(end.X, end.Y - 50);

                figure.Segments.Add(new BezierSegment(p1, p2, end, true));
                geometry.Figures.Add(figure);

                return geometry;
            }
            catch
            {
                return Geometry.Empty;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
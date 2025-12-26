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
            // Kiểm tra an toàn
            if (values.Length < 8) return Geometry.Empty;
            foreach (var val in values)
            {
                if (val == DependencyProperty.UnsetValue || val == null) return Geometry.Empty;
                if (val is double d && (double.IsNaN(d) || double.IsInfinity(d))) return Geometry.Empty;
            }

            try
            {
                double sx = (double)values[0]; double sy = (double)values[1];
                double sw = (double)values[2]; double sh = (double)values[3];

                double tx = (double)values[4]; double ty = (double)values[5];
                double tw = (double)values[6]; double th = (double)values[7];

                if (sw <= 0 || sh <= 0 || tw <= 0 || th <= 0) return Geometry.Empty;

                Point sourceCenter = new Point(sx + sw / 2, sy + sh / 2);
                Point targetCenter = new Point(tx + tw / 2, ty + th / 2);

                Point rawStartPoint = GetIntersectionPoint(new Rect(sx, sy, sw, sh), sourceCenter, targetCenter);
                Point rawEndPoint = GetIntersectionPoint(new Rect(tx, ty, tw, th), targetCenter, sourceCenter);

                Vector direction = targetCenter - sourceCenter;
                if (direction.Length > 0) direction.Normalize();

                double overlap = 3.0;
                Point startPoint = rawStartPoint - (direction * overlap);
                Point endPoint = rawEndPoint + (direction * overlap);

                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(startPoint, false, false);
                    double distanceX = Math.Abs(endPoint.X - startPoint.X);
                    double distanceY = Math.Abs(endPoint.Y - startPoint.Y);
                    double controlDist = Math.Max(distanceX / 2, 50);
                    if (distanceY > 100 && distanceX < 50) controlDist = distanceY / 3;

                    Point p1 = new Point(startPoint.X + controlDist, startPoint.Y);
                    Point p2 = new Point(endPoint.X - controlDist, endPoint.Y);

                    ctx.BezierTo(p1, p2, endPoint, true, true);
                }
                geometry.Freeze();
                return geometry;
            }
            catch
            {
                return Geometry.Empty;
            }
        }

        // Hàm helper dùng riêng cho class này
        private Point GetIntersectionPoint(Rect rect, Point center, Point otherCenter)
        {
            double dx = otherCenter.X - center.X;
            double dy = otherCenter.Y - center.Y;

            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) return center;

            double halfWidth = rect.Width / 2.0;
            double halfHeight = rect.Height / 2.0;

            double tx = (dx == 0) ? double.MaxValue : halfWidth / Math.Abs(dx);
            double ty = (dy == 0) ? double.MaxValue : halfHeight / Math.Abs(dy);

            if (tx <= ty) return new Point(center.X + (dx > 0 ? halfWidth : -halfWidth), center.Y + tx * dy);
            else return new Point(center.X + ty * dx, center.Y + (dy > 0 ? halfHeight : -halfHeight));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
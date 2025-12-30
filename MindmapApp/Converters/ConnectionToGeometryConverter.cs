using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindmapApp.ViewModels; // Để nhận diện ConnectionStyle

namespace MindmapApp.Converters
{
    public class ConnectionToGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Kiểm tra an toàn (Cần ít nhất 8 tham số tọa độ)
            if (values.Length < 8) return Geometry.Empty;

            foreach (var val in values)
            {
                if (val == DependencyProperty.UnsetValue || val == null) return Geometry.Empty;
                if (val is double d && (double.IsNaN(d) || double.IsInfinity(d))) return Geometry.Empty;
            }

            try
            {
                // 1. Lấy tọa độ (Giữ nguyên)
                double sx = (double)values[0]; double sy = (double)values[1];
                double sw = (double)values[2]; double sh = (double)values[3];

                double tx = (double)values[4]; double ty = (double)values[5];
                double tw = (double)values[6]; double th = (double)values[7];

                // 2. Lấy ConnectionStyle (Tham số thứ 9 - Giữ nguyên logic của bạn)
                bool isCurved = true;
                if (values.Length > 8 && values[8] is ConnectionStyle style)
                {
                    isCurved = (style == ConnectionStyle.Bezier);
                }

                // 3. --- THÊM MỚI: Lấy ArrowStyle (Tham số thứ 10) ---
                bool hasArrow = false;
                if (values.Length > 9)
                {
                    // Kiểm tra chuỗi, nếu là "Arrow" thì true, "None" thì false
                    string arrowStyle = values[9]?.ToString();
                    hasArrow = (arrowStyle == "Arrow");
                }
                // ----------------------------------------------------

                if (sw <= 0 || sh <= 0 || tw <= 0 || th <= 0) return Geometry.Empty;

                Point sourceCenter = new Point(sx + sw / 2, sy + sh / 2);
                Point targetCenter = new Point(tx + tw / 2, ty + th / 2);

                Point startPoint, endPoint;

                // 4. --- LOGIC QUYẾT ĐỊNH ĐIỂM ĐẦU/CUỐI ---
                if (hasArrow)
                {
                    // CÓ MŨI TÊN: Dùng thuật toán Intersection cũ của bạn để dừng ở viền
                    Point rawStartPoint = GetIntersectionPoint(new Rect(sx, sy, sw, sh), sourceCenter, targetCenter);
                    Point rawEndPoint = GetIntersectionPoint(new Rect(tx, ty, tw, th), targetCenter, sourceCenter);

                    Vector direction = targetCenter - sourceCenter;
                    if (direction.Length > 0) direction.Normalize();

                    double overlap = 3.0; // Giữ nguyên overlap
                    startPoint = rawStartPoint - (direction * overlap);
                    endPoint = rawEndPoint + (direction * overlap);
                }
                else
                {
                    // KHÔNG MŨI TÊN: Đi thẳng vào tâm Node
                    startPoint = sourceCenter;
                    endPoint = targetCenter;
                }
                // ------------------------------------------

                // 5. Vẽ hình (Giữ nguyên logic cũ)
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(startPoint, false, false);
                    if (isCurved)
                    {
                        double distanceX = Math.Abs(endPoint.X - startPoint.X);
                        double distanceY = Math.Abs(endPoint.Y - startPoint.Y);
                        double controlDist = Math.Max(distanceX / 2, 50);
                        if (distanceY > 100 && distanceX < 50) controlDist = distanceY / 3;

                        Point p1 = new Point(startPoint.X + controlDist, startPoint.Y);
                        Point p2 = new Point(endPoint.X - controlDist, endPoint.Y);

                        ctx.BezierTo(p1, p2, endPoint, true, true);
                    }
                    else
                    {
                        ctx.LineTo(endPoint, true, true);
                    }
                }
                geometry.Freeze();
                return geometry;
            }
            catch
            {
                return Geometry.Empty;
            }
        }

        // Giữ nguyên hàm GetIntersectionPoint cũ của bạn 100%
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
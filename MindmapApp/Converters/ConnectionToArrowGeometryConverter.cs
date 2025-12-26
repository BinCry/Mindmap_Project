using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MindmapApp.Converters
{
    public class ConnectionToArrowGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 8) return Geometry.Empty;
            foreach (var val in values)
            {
                if (val == DependencyProperty.UnsetValue || val == null) return Geometry.Empty;
                if (val is double d && (double.IsNaN(d) || double.IsInfinity(d))) return Geometry.Empty;
            }

            try
            {
                // Kiểm tra tham số ArrowStyle (tham số thứ 9)
                string arrowStyle = "Arrow";
                if (values.Length > 8 && values[8] is string style) arrowStyle = style;

                // Nếu style là None hoặc rỗng thì không vẽ gì cả
                if (string.IsNullOrEmpty(arrowStyle) || arrowStyle == "None") return Geometry.Empty;

                double sx = (double)values[0]; double sy = (double)values[1];
                double sw = (double)values[2]; double sh = (double)values[3];

                double tx = (double)values[4]; double ty = (double)values[5];
                double tw = (double)values[6]; double th = (double)values[7];

                Point sourceCenter = new Point(sx + sw / 2, sy + sh / 2);
                Point targetCenter = new Point(tx + tw / 2, ty + th / 2);

                Point rawStartPoint = GetIntersectionPoint(new Rect(sx, sy, sw, sh), sourceCenter, targetCenter);
                Point rawEndPoint = GetIntersectionPoint(new Rect(tx, ty, tw, th), targetCenter, sourceCenter);

                Vector direction = targetCenter - sourceCenter;
                if (direction.Length > 0) direction.Normalize();

                double overlap = 3.0;
                Point startPoint = rawStartPoint - (direction * overlap);
                Point endPoint = rawEndPoint + (direction * overlap);

                // --- FIX QUAN TRỌNG: Tính Vector hướng mũi tên ---
                // Tính điểm điều khiển Bezier để xác định hướng tiếp tuyến tại điểm cuối
                double distanceX = Math.Abs(endPoint.X - startPoint.X);
                double distanceY = Math.Abs(endPoint.Y - startPoint.Y);
                double controlDist = Math.Max(distanceX / 2, 50);
                if (distanceY > 100 && distanceX < 50) controlDist = distanceY / 3;

                Point control2 = new Point(endPoint.X - controlDist, endPoint.Y);

                Vector tangent = endPoint - control2;

                // Nếu 2 điểm quá gần nhau hoặc thẳng hàng làm vector = 0 -> Dùng hướng thẳng nối 2 tâm
                if (tangent.Length < 0.1)
                {
                    tangent = endPoint - startPoint;
                }

                if (tangent.Length > 0) tangent.Normalize();

                // Vẽ hình mũi tên
                StreamGeometry arrowGeo = new StreamGeometry();
                using (StreamGeometryContext ctx = arrowGeo.Open())
                {
                    double arrowLen = 10;
                    double arrowWidth = 4;

                    Vector vBack = -tangent; // Hướng ngược lại để vẽ đuôi mũi tên
                    Vector vLeft = new Vector(-tangent.Y, tangent.X); // Vuông góc trái

                    // Đỉnh mũi tên nằm tại rawEndPoint (chạm mép node)
                    Point tip = rawEndPoint;
                    Point pBase1 = tip + (vBack * arrowLen) + (vLeft * arrowWidth);
                    Point pBase2 = tip + (vBack * arrowLen) - (vLeft * arrowWidth);

                    ctx.BeginFigure(tip, true, true); // true = filled
                    ctx.PolyLineTo(new[] { pBase1, pBase2 }, true, true);
                }
                arrowGeo.Freeze();
                return arrowGeo;
            }
            catch { return Geometry.Empty; }
        }

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
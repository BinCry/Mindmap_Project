using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindmapApp.ViewModels; // Để dùng Enum ConnectionStyle

namespace MindmapApp.Converters
{
    public class ConnectionToArrowGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 1. Kiểm tra an toàn
            if (values.Length < 10) return Geometry.Empty;
            foreach (var val in values)
            {
                if (val == DependencyProperty.UnsetValue || val == null) return Geometry.Empty;
                if (val is double d && (double.IsNaN(d) || double.IsInfinity(d))) return Geometry.Empty;
            }

            try
            {
                // 2. Lấy tọa độ (Giữ nguyên)
                double sx = (double)values[0]; double sy = (double)values[1];
                double sw = (double)values[2]; double sh = (double)values[3];
                double tx = (double)values[4]; double ty = (double)values[5];
                double tw = (double)values[6]; double th = (double)values[7];

                // 3. Lấy ConnectionStyle (Tham số thứ 8 - Khớp với XAML)
                bool isCurved = true;
                if (values[8] is ConnectionStyle style)
                {
                    isCurved = (style == ConnectionStyle.Bezier);
                }

                // 4. Lấy ArrowStyle (Tham số thứ 9 - Khớp với XAML)
                bool hasArrow = false;
                if (values[9]?.ToString() == "Arrow")
                {
                    hasArrow = true;
                }

                // Nếu không có mũi tên thì trả về rỗng
                if (!hasArrow) return Geometry.Empty;

                // 5. Tính toán điểm (Giữ nguyên logic Intersection chuẩn)
                Point sourceCenter = new Point(sx + sw / 2, sy + sh / 2);
                Point targetCenter = new Point(tx + tw / 2, ty + th / 2);

                Point rawStartPoint = GetIntersectionPoint(new Rect(sx, sy, sw, sh), sourceCenter, targetCenter);
                Point rawEndPoint = GetIntersectionPoint(new Rect(tx, ty, tw, th), targetCenter, sourceCenter);

                Vector direction = targetCenter - sourceCenter;
                if (direction.Length > 0) direction.Normalize();

                double overlap = 3.0;
                Point startPoint = rawStartPoint - (direction * overlap);
                Point endPoint = rawEndPoint + (direction * overlap);

                // 6. Tính toán hướng mũi tên (Vector Tangent)
                Vector tangent;
                if (isCurved)
                {
                    // Logic tính hướng cho dây cong
                    double distanceX = Math.Abs(endPoint.X - startPoint.X);
                    double distanceY = Math.Abs(endPoint.Y - startPoint.Y);
                    double controlDist = Math.Max(distanceX / 2, 50);
                    if (distanceY > 100 && distanceX < 50) controlDist = distanceY / 3;

                    Point control2 = new Point(endPoint.X - controlDist, endPoint.Y);
                    tangent = endPoint - control2;
                }
                else
                {
                    // Logic tính hướng cho dây thẳng
                    tangent = endPoint - startPoint;
                }

                if (tangent.Length < 0.1) tangent = endPoint - startPoint;
                if (tangent.Length > 0) tangent.Normalize();

                // --- 7. VẼ HÌNH MŨI TÊN (ĐÃ CHỈNH KÍCH THƯỚC TO LÊN) ---
                StreamGeometry arrowGeo = new StreamGeometry();
                using (StreamGeometryContext ctx = arrowGeo.Open())
                {
                    // === CHỈNH KÍCH THƯỚC Ở ĐÂY ===
                    double arrowLen = 18;  // Độ dài mũi tên (Cũ là 10)
                    double arrowWidth = 12; // Độ rộng đáy (Cũ là 4)
                    // ==============================

                    Vector vBack = -tangent; // Hướng ngược lại
                    Vector vLeft = new Vector(-tangent.Y, tangent.X); // Vuông góc

                    Point tip = rawEndPoint; // Đỉnh mũi tên chạm vào viền Node
                    Point pBase1 = tip + (vBack * arrowLen) + (vLeft * (arrowWidth / 2));
                    Point pBase2 = tip + (vBack * arrowLen) - (vLeft * (arrowWidth / 2));

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
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindmapApp.ViewModels; // Để nhận diện Enum ConnectionStyle

namespace MindmapApp.Converters
{
    public class ConnectionToArrowGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 1. Kiểm tra an toàn (10 tham số)
            if (values.Length < 10) return Geometry.Empty;
            foreach (var val in values)
            {
                if (val == DependencyProperty.UnsetValue || val == null) return Geometry.Empty;
                if (val is double d && (double.IsNaN(d) || double.IsInfinity(d))) return Geometry.Empty;
            }

            try
            {
                // 2. Lấy tọa độ
                double sx = (double)values[0]; double sy = (double)values[1];
                double sw = (double)values[2]; double sh = (double)values[3];
                double tx = (double)values[4]; double ty = (double)values[5];
                double tw = (double)values[6]; double th = (double)values[7];

                // 3. Lấy ConnectionStyle (Tham số thứ 8 - index 8)
                bool isCurved = true;
                if (values[8] is ConnectionStyle style)
                {
                    isCurved = (style == ConnectionStyle.Bezier);
                }

                // 4. Lấy ArrowStyle (Tham số thứ 9 - index 9)
                bool hasArrow = false;
                if (values[9]?.ToString() == "Arrow")
                {
                    hasArrow = true;
                }

                // Nếu không có mũi tên thì trả về rỗng ngay
                if (!hasArrow) return Geometry.Empty;

                // 5. TÍNH TOÁN TỌA ĐỘ (Logic: Tâm Nguồn -> Rìa Đích)

                // Tâm của các Node
                Point sourceCenter = new Point(sx + sw / 2, sy + sh / 2);
                Point targetCenter = new Point(tx + tw / 2, ty + th / 2);

                // a. Điểm bắt đầu: TÂM NODE NGUỒN (theo yêu cầu của bạn)
                Point startPoint = sourceCenter;

                // b. Điểm kết thúc: RÌA NODE ĐÍCH
                Point rawEndPoint = GetIntersectionPoint(new Rect(tx, ty, tw, th), targetCenter, sourceCenter);

                Vector direction = targetCenter - sourceCenter;
                if (direction.Length > 0) direction.Normalize();

                // Thụt vào 3px giống như đường dây để khớp vị trí
                double overlap = 3.0;
                Point endPoint = rawEndPoint + (direction * overlap);

                // 6. Tính toán hướng mũi tên (Vector Tangent)
                Vector tangent;
                if (isCurved)
                {
                    // Logic tính hướng cho dây cong Bezier
                    double distanceX = Math.Abs(endPoint.X - startPoint.X);
                    double distanceY = Math.Abs(endPoint.Y - startPoint.Y);

                    double controlDist = Math.Max(distanceX / 2, 50);
                    if (distanceY > 100 && distanceX < 50) controlDist = distanceY / 3;

                    // Tính điểm điều khiển cuối (phải khớp logic với file vẽ dây)
                    Point control2 = new Point(endPoint.X - controlDist, endPoint.Y);

                    // Hướng tiếp tuyến tại điểm cuối = EndPoint - ControlPoint2
                    tangent = endPoint - control2;
                }
                else
                {
                    // Logic tính hướng cho dây thẳng
                    tangent = endPoint - startPoint;
                }

                // Chuẩn hóa vector hướng
                if (tangent.Length < 0.1) tangent = endPoint - startPoint;
                if (tangent.Length > 0) tangent.Normalize();

                // --- 7. VẼ HÌNH MŨI TÊN ---
                StreamGeometry arrowGeo = new StreamGeometry();
                using (StreamGeometryContext ctx = arrowGeo.Open())
                {
                    // Kích thước mũi tên (To rõ)
                    double arrowLen = 18;
                    double arrowWidth = 12;

                    Vector vBack = -tangent; // Hướng ngược lại
                    Vector vLeft = new Vector(-tangent.Y, tangent.X); // Vuông góc trái

                    Point tip = endPoint; // Đỉnh mũi tên
                    Point pBase1 = tip + (vBack * arrowLen) + (vLeft * (arrowWidth / 2));
                    Point pBase2 = tip + (vBack * arrowLen) - (vLeft * (arrowWidth / 2));

                    ctx.BeginFigure(tip, true, true); // true = filled (tô màu)
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
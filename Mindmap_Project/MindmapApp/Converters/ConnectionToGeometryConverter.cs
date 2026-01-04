using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindmapApp.ViewModels;

namespace MindmapApp.Converters
{
    public class ConnectionToGeometryConverter : IMultiValueConverter
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
                double sx = (double)values[0]; double sy = (double)values[1];
                double sw = (double)values[2]; double sh = (double)values[3];
                double tx = (double)values[4]; double ty = (double)values[5];
                double tw = (double)values[6]; double th = (double)values[7];

                bool isCurved = true;
                if (values.Length > 8 && values[8] is ConnectionStyle style)
                    isCurved = (style == ConnectionStyle.Bezier);

                bool hasArrow = false;
                if (values.Length > 9) hasArrow = (values[9]?.ToString() == "Arrow");

                if (sw <= 0 || sh <= 0 || tw <= 0 || th <= 0) return Geometry.Empty;

                Rect targetRect = new Rect(tx, ty, tw, th);
                Point sourceCenter = new Point(sx + sw / 2, sy + sh / 2);
                Point targetCenter = new Point(tx + tw / 2, ty + th / 2);

                Point startPoint, endPoint;

                // --- XÁC ĐỊNH ĐIỂM ĐẦU / CUỐI ---
                if (hasArrow)
                {
                    startPoint = sourceCenter;
                    Point rawEndPoint = GetIntersectionPoint(targetRect, sourceCenter, targetCenter);

                    // Xác định hướng dựa trên vị trí điểm kết thúc thực tế
                    // Nếu điểm nằm trên cạnh Trái/Phải -> Hướng Ngang. Ngược lại -> Hướng Dọc
                    Vector dir;
                    bool isSideHorizontal = Math.Abs(rawEndPoint.X - targetRect.Left) < 0.1 || Math.Abs(rawEndPoint.X - targetRect.Right) < 0.1;

                    if (isSideHorizontal)
                        dir = new Vector(targetCenter.X - sourceCenter.X, 0);
                    else
                        dir = new Vector(0, targetCenter.Y - sourceCenter.Y);

                    if (dir.Length > 0) dir.Normalize();
                    endPoint = rawEndPoint - (dir * 3.0);
                }
                else
                {
                    startPoint = sourceCenter;
                    endPoint = targetCenter;
                }

                // --- VẼ DÂY ---
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(startPoint, false, false);

                    if (isCurved)
                    {
                        double diffX = endPoint.X - startPoint.X;
                        double diffY = endPoint.Y - startPoint.Y;
                        Point c1, c2;

                        // LOGIC QUAN TRỌNG: Quyết định hướng cong dựa vào điểm đích nằm ở cạnh nào
                        // Nếu điểm đích nằm cạnh Trái/Phải -> Ưu tiên vẽ cong Ngang
                        // Nếu điểm đích nằm cạnh Trên/Dưới -> Ưu tiên vẽ cong Dọc
                        // Ta kiểm tra lại endPoint nằm gần cạnh nào của TargetRect

                        bool isHorizontalCurve;
                        if (hasArrow)
                        {
                            // Nếu có mũi tên, ta tin tưởng hoàn toàn vào vị trí điểm kết thúc đã tính toán
                            isHorizontalCurve = Math.Abs(endPoint.X - targetRect.Left) < 5.0 || Math.Abs(endPoint.X - targetRect.Right) < 5.0;
                        }
                        else
                        {
                            // Nếu không có mũi tên (nối tâm), dùng tỷ lệ khung hình để quyết định
                            double normalizedX = Math.Abs(diffX) / (tw > 0 ? tw : 1);
                            double normalizedY = Math.Abs(diffY) / (th > 0 ? th : 1);
                            isHorizontalCurve = normalizedX >= normalizedY;
                        }

                        if (isHorizontalCurve)
                        {
                            double dist = Math.Max(Math.Abs(diffX) / 2, 50);
                            if (Math.Abs(diffY) > 100 && Math.Abs(diffX) < 50) dist = Math.Abs(diffY) / 3;
                            double sign = (diffX > 0) ? 1 : -1;

                            c1 = new Point(startPoint.X + (dist * sign), startPoint.Y);
                            c2 = new Point(endPoint.X - (dist * sign), endPoint.Y);
                        }
                        else
                        {
                            double dist = Math.Max(Math.Abs(diffY) / 2, 50);
                            if (Math.Abs(diffX) > 100 && Math.Abs(diffY) < 50) dist = Math.Abs(diffX) / 3;
                            double sign = (diffY > 0) ? 1 : -1;

                            c1 = new Point(startPoint.X, startPoint.Y + (dist * sign));
                            c2 = new Point(endPoint.X, endPoint.Y - (dist * sign));
                        }

                        ctx.BezierTo(c1, c2, endPoint, true, true);
                    }
                    else
                    {
                        ctx.LineTo(endPoint, true, true);
                    }
                }
                geometry.Freeze();
                return geometry;
            }
            catch { return Geometry.Empty; }
        }

        // --- HÀM TÍNH GIAO ĐIỂM DỰA TRÊN TỶ LỆ KHUNG HÌNH (ASPECT RATIO) ---
        private Point GetIntersectionPoint(Rect targetRect, Point sourceCenter, Point targetCenter)
        {
            double dx = targetCenter.X - sourceCenter.X;
            double dy = targetCenter.Y - sourceCenter.Y;

            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) return targetCenter;

            // CHÌA KHÓA GIẢI QUYẾT VẤN ĐỀ:
            // Chuẩn hóa khoảng cách theo kích thước của Node đích.
            // Điều này giúp thuật toán "hiểu" rằng Node rộng thì cạnh trên/dưới dài hơn,
            // nên cần góc nghiêng lớn hơn mới chuyển sang cạnh bên.
            double normalizedX = Math.Abs(dx) / (targetRect.Width > 0 ? targetRect.Width : 1);
            double normalizedY = Math.Abs(dy) / (targetRect.Height > 0 ? targetRect.Height : 1);

            bool isHorizontal = normalizedX >= normalizedY;

            if (isHorizontal)
            {
                // Bắt vào cạnh TRÁI hoặc PHẢI
                double intersectY = targetCenter.Y;
                double intersectX = (dx > 0) ? targetRect.Left : targetRect.Right;
                return new Point(intersectX, intersectY);
            }
            else
            {
                // Bắt vào cạnh TRÊN hoặc DƯỚI
                double intersectX = targetCenter.X;
                double intersectY = (dy > 0) ? targetRect.Top : targetRect.Bottom;
                return new Point(intersectX, intersectY);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
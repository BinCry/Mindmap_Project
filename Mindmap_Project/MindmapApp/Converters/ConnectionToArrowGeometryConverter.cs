using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindmapApp.ViewModels;

namespace MindmapApp.Converters
{
    public class ConnectionToArrowGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 10) return Geometry.Empty;
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
                if (values[8] is ConnectionStyle style) isCurved = (style == ConnectionStyle.Bezier);

                bool hasArrow = false;
                if (values[9]?.ToString() == "Arrow") hasArrow = true;

                if (!hasArrow) return Geometry.Empty;

                Rect targetRect = new Rect(tx, ty, tw, th);
                Point sourceCenter = new Point(sx + sw / 2, sy + sh / 2);
                Point targetCenter = new Point(tx + tw / 2, ty + th / 2);
                Point startPoint = sourceCenter;

                // 1. Tìm điểm giữa cạnh (Logic Aspect Ratio)
                Point rawEndPoint = GetIntersectionPoint(targetRect, sourceCenter, targetCenter);

                // 2. Tính hướng dựa trên vị trí điểm
                Vector direction;
                bool isSideHorizontal = Math.Abs(rawEndPoint.X - targetRect.Left) < 0.1 || Math.Abs(rawEndPoint.X - targetRect.Right) < 0.1;

                if (isSideHorizontal) direction = new Vector(targetCenter.X - sourceCenter.X, 0);
                else direction = new Vector(0, targetCenter.Y - sourceCenter.Y);

                if (direction.Length > 0) direction.Normalize();
                double overlap = 3.0;
                Point endPoint = rawEndPoint - (direction * overlap);

                // 3. Tính Vector hướng mũi tên
                Vector tangent;
                if (isCurved)
                {
                    double diffX = endPoint.X - startPoint.X;
                    double diffY = endPoint.Y - startPoint.Y;
                    Point c2;

                    // Logic đồng bộ: Nếu điểm nằm cạnh bên -> Cong ngang. Cạnh trên/dưới -> Cong dọc
                    bool isHorizontalCurve = Math.Abs(endPoint.X - targetRect.Left) < 5.0 || Math.Abs(endPoint.X - targetRect.Right) < 5.0;

                    if (isHorizontalCurve)
                    {
                        double dist = Math.Max(Math.Abs(diffX) / 2, 50);
                        if (Math.Abs(diffY) > 100 && Math.Abs(diffX) < 50) dist = Math.Abs(diffY) / 3;
                        double sign = (diffX > 0) ? 1 : -1;
                        c2 = new Point(endPoint.X - (dist * sign), endPoint.Y);
                    }
                    else
                    {
                        double dist = Math.Max(Math.Abs(diffY) / 2, 50);
                        if (Math.Abs(diffX) > 100 && Math.Abs(diffY) < 50) dist = Math.Abs(diffX) / 3;
                        double sign = (diffY > 0) ? 1 : -1;
                        c2 = new Point(endPoint.X, endPoint.Y - (dist * sign));
                    }
                    tangent = endPoint - c2;
                }
                else
                {
                    tangent = endPoint - startPoint;
                }

                if (tangent.Length < 0.1) tangent = endPoint - startPoint;
                if (tangent.Length > 0) tangent.Normalize();

                StreamGeometry arrowGeo = new StreamGeometry();
                using (StreamGeometryContext ctx = arrowGeo.Open())
                {
                    double arrowLen = 18; double arrowWidth = 12;
                    Vector vBack = -tangent;
                    Vector vLeft = new Vector(-tangent.Y, tangent.X);
                    Point tip = endPoint;
                    Point pBase1 = tip + (vBack * arrowLen) + (vLeft * (arrowWidth / 2));
                    Point pBase2 = tip + (vBack * arrowLen) - (vLeft * (arrowWidth / 2));

                    ctx.BeginFigure(tip, true, true);
                    ctx.PolyLineTo(new[] { pBase1, pBase2 }, true, true);
                }
                arrowGeo.Freeze();
                return arrowGeo;
            }
            catch { return Geometry.Empty; }
        }

        private Point GetIntersectionPoint(Rect targetRect, Point sourceCenter, Point targetCenter)
        {
            double dx = targetCenter.X - sourceCenter.X;
            double dy = targetCenter.Y - sourceCenter.Y;

            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) return targetCenter;

            // Logic Aspect Ratio (Tương tự file trên)
            double normalizedX = Math.Abs(dx) / (targetRect.Width > 0 ? targetRect.Width : 1);
            double normalizedY = Math.Abs(dy) / (targetRect.Height > 0 ? targetRect.Height : 1);

            bool isHorizontal = normalizedX >= normalizedY;

            if (isHorizontal)
            {
                double intersectY = targetCenter.Y;
                double intersectX = (dx > 0) ? targetRect.Left : targetRect.Right;
                return new Point(intersectX, intersectY);
            }
            else
            {
                double intersectX = targetCenter.X;
                double intersectY = (dy > 0) ? targetRect.Top : targetRect.Bottom;
                return new Point(intersectX, intersectY);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
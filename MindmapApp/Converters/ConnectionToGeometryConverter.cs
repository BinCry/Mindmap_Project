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
    #region Core Logic (Drawing Bezier Curve)

    /// <summary>
    /// Chuyển đổi 2 Node (Source & Target) thành một đường cong Bezier
    /// </summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // 1. KIỂM TRA INPUT (Phiên bản mới)
        // Bây giờ chúng ta mong đợi 4 giá trị kiểu double từ XAML gửi xuống
        if (values.Length != 4 ||
            !(values[0] is double sourceX) ||
            !(values[1] is double sourceY) ||
            !(values[2] is double targetX) ||
            !(values[3] is double targetY))
        {
            // Nếu dữ liệu chưa sẵn sàng hoặc bị null -> Không vẽ gì cả
            return Geometry.Empty;
        }

        var start = new Point(sourceX, sourceY);
        var end = new Point(targetX, targetY);

        // Tạo một nhóm hình học để chứa cả Dây và Mũi tên
        var geometryGroup = new GeometryGroup();

        // =========================================================
        // PHẦN 1: VẼ DÂY CONG (BEZIER CURVE)
        // =========================================================

        // Tính toán độ cong dựa trên khoảng cách
        var controlOffset = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) / 2;

        // Điểm uốn 1: Đi ra từ nguồn
        var controlPoint1 = new Point(start.X + controlOffset, start.Y);
        // Điểm uốn 2: Đi vào đích
        var controlPoint2 = new Point(end.X - controlOffset, end.Y);

        var pathFigure = new PathFigure { StartPoint = start, IsClosed = false };
        pathFigure.Segments.Add(new BezierSegment(controlPoint1, controlPoint2, end, true));

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        geometryGroup.Children.Add(pathGeometry);

        // =========================================================
        // PHẦN 2: VẼ MŨI TÊN (ARROW HEAD)
        // =========================================================

        // Tính vectơ hướng của dây tại điểm cuối (từ ControlPoint2 -> End)
        Vector vector = end - controlPoint2;

        // Nếu dây quá thẳng (2 điểm trùng nhau), lấy hướng từ Start -> End
        if (vector.Length < 0.1) vector = end - start;

        vector.Normalize(); // Chuẩn hóa vectơ về độ dài 1

        double arrowSize = 8; // Kích thước mũi tên (bạn có thể chỉnh to nhỏ ở đây)
        Point tip = end;      // Mũi nhọn nằm đúng tại điểm kết thúc

        // Tính toán 2 cánh của mũi tên bằng ma trận xoay
        // Cánh 1: Xoay vectơ 1 góc và lùi lại
        Point wing1 = end - (vector * arrowSize) + (new Vector(-vector.Y, vector.X) * (arrowSize * 0.5));
        // Cánh 2: Xoay ngược lại
        Point wing2 = end - (vector * arrowSize) - (new Vector(-vector.Y, vector.X) * (arrowSize * 0.5));

        var arrowFigure = new PathFigure { StartPoint = tip, IsClosed = true, IsFilled = true };
        arrowFigure.Segments.Add(new LineSegment(wing1, true));
        arrowFigure.Segments.Add(new LineSegment(wing2, true));

        var arrowGeometry = new PathGeometry();
        arrowGeometry.Figures.Add(arrowFigure);

        geometryGroup.Children.Add(arrowGeometry);

        return geometryGroup;
    }
    #endregion
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

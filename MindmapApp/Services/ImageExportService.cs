using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;
using MindmapApp.ViewModels;

namespace MindmapApp.Services
{
    public class ImageExportService
    {
        public void ExportImage(string filePath, IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections)
        {
            var nodeList = nodes.ToList();
            if (!nodeList.Any()) return;

            // 1. TÍNH TOÁN VÙNG BAO (BOUNDING BOX)
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var node in nodeList)
            {
                if (node.X < minX) minX = node.X;
                if (node.Y < minY) minY = node.Y;
                if (node.X + node.Width > maxX) maxX = node.X + node.Width;
                if (node.Y + node.Height > maxY) maxY = node.Y + node.Height;
            }

            double padding = 50;
            double width = (maxX - minX) + (padding * 2);
            double height = (maxY - minY) + (padding * 2);

            // 2. TÍNH TOÁN DPI AN TOÀN (Để tránh lỗi ảnh quá khổ gây đen/trắng ảnh)
            // Giới hạn cạnh lớn nhất khoảng 4000-5000px là đẹp cho file PNG
            double maxDimension = Math.Max(width, height);
            double dpi = 300; // Mặc định nét cao

            if (maxDimension * (300.0 / 96.0) > 6000)
            {
                // Tự động giảm DPI nếu hình quá to (để không bị crash bộ nhớ)
                dpi = (6000.0 / maxDimension) * 96.0;
                if (dpi < 96) dpi = 96;
            }
            double scale = dpi / 96.0;

            // 3. KHỞI TẠO BẢNG VẼ (DrawingVisual)
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                // A. Vẽ nền trắng (Lót dưới cùng)
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                // B. Dịch chuyển gốc tọa độ để hình nằm giữa
                dc.PushTransform(new TranslateTransform(-minX + padding, -minY + padding));

                // C. VẼ DÂY (Connections)
                foreach (var conn in connections)
                {
                    DrawConnection(dc, conn);
                }

                // D. VẼ NODE
                foreach (var node in nodeList)
                {
                    DrawNode(dc, node);
                }

                dc.Pop(); // Kết thúc dịch chuyển
            }

            // 4. RENDER RA BITMAP
            int pixelWidth = (int)Math.Ceiling(width * scale);
            int pixelHeight = (int)Math.Ceiling(height * scale);

            RenderTargetBitmap rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            // 5. LƯU FILE
            BitmapEncoder encoder;
            if (filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                encoder = new JpegBitmapEncoder();
            else
                encoder = new PngBitmapEncoder(); // Mặc định PNG

            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var stream = File.Create(filePath))
            {
                encoder.Save(stream);
            }
        }

        private void DrawConnection(DrawingContext dc, ConnectionViewModel conn)
        {
            if (conn.Source == null || conn.Target == null) return;

            // Tạo Pen (Bút vẽ)
            Brush strokeBrush = new SolidColorBrush(conn.StrokeColor);
            Pen pen = new Pen(strokeBrush, conn.Thickness);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;

            double x1 = conn.Source.X + conn.Source.Width / 2;
            double y1 = conn.Source.Y + conn.Source.Height / 2;
            double x2 = conn.Target.X + conn.Target.Width / 2;
            double y2 = conn.Target.Y + conn.Target.Height / 2;

            // Vẽ đường cong Bezier
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(x1, y1), false, false);

                // Tính điểm uốn cong
                double offset = Math.Abs(x2 - x1) * 0.5;
                Point p1 = new Point(x1 + offset, y1);
                Point p2 = new Point(x2 - offset, y2);

                ctx.BezierTo(p1, p2, new Point(x2, y2), true, true);
            }

            dc.DrawGeometry(null, pen, geometry);
        }

        private void DrawNode(DrawingContext dc, NodeViewModel node)
        {
            Brush bgBrush = new SolidColorBrush(node.BackgroundColor);
            Brush borderBrush = new SolidColorBrush(node.BorderColor);
            Pen borderPen = new Pen(borderBrush, 1); // Viền dày 1px

            Rect rect = new Rect(node.X, node.Y, node.Width, node.Height);

            // --- VẼ HÌNH DÁNG ---
            switch (node.Shape)
            {
                case "Ellipse":
                    double radiusX = node.Width / 2;
                    double radiusY = node.Height / 2;
                    dc.DrawEllipse(bgBrush, borderPen, new Point(node.X + radiusX, node.Y + radiusY), radiusX, radiusY);
                    break;

                case "Diamond":
                    StreamGeometry diamondGeo = new StreamGeometry();
                    using (StreamGeometryContext ctx = diamondGeo.Open())
                    {
                        ctx.BeginFigure(new Point(node.X + node.Width / 2, node.Y), true, true);
                        ctx.LineTo(new Point(node.X + node.Width, node.Y + node.Height / 2), true, true);
                        ctx.LineTo(new Point(node.X + node.Width / 2, node.Y + node.Height), true, true);
                        ctx.LineTo(new Point(node.X, node.Y + node.Height / 2), true, true);
                    }
                    dc.DrawGeometry(bgBrush, borderPen, diamondGeo);
                    break;

                case "Rectangle":
                    dc.DrawRectangle(bgBrush, borderPen, rect);
                    break;

                case "RoundedRectangle":
                default:
                    dc.DrawRoundedRectangle(bgBrush, borderPen, rect, 10, 10);
                    break;
            }

            // --- VẼ CHỮ (TEXT) ---
            DrawNodeText(dc, node);
        }

        private void DrawNodeText(DrawingContext dc, NodeViewModel node)
        {
            if (string.IsNullOrEmpty(node.Title)) return;

            Typeface typeface = new Typeface(new FontFamily(node.FontFamily ?? "Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            Brush textBrush = new SolidColorBrush(node.TextColor);

            FormattedText text = new FormattedText(
                node.Title,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                node.FontSize,
                textBrush,
                VisualTreeHelper.GetDpi(new ContainerVisual()).PixelsPerDip);

            text.MaxTextWidth = node.Width;
            text.MaxTextHeight = node.Height;
            text.TextAlignment = TextAlignment.Center;

            // Căn giữa theo chiều dọc (Vertical Center)
            double yOffset = (node.Height - text.Height) / 2;
            if (yOffset < 0) yOffset = 0; // Tránh lỗi nếu chữ quá dài

            dc.DrawText(text, new Point(node.X, node.Y + yOffset));
        }
    }
}
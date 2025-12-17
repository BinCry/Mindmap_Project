using PdfSharp.Pdf;
using PdfSharp.Drawing;
using MindmapApp.ViewModels;
using System.Collections.Generic;
using System.Windows.Media; // Dùng để lấy Color của WPF
using System;
using System.Linq;

namespace MindmapApp.Services
{
    public class PdfExportService
    {
        public void ExportPdf(string filePath, string title, IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections)
        {
            // 1. TÍNH TOÁN VÙNG BAO (BOUNDING BOX)
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            var nodeList = nodes.ToList();
            if (!nodeList.Any()) return;

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

            // 2. KHỞI TẠO DOCUMENT
            PdfDocument document = new PdfDocument();
            document.Info.Title = title;

            PdfPage page = document.AddPage();
            page.Width = width;
            page.Height = height;

            XGraphics gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.White, 0, 0, page.Width, page.Height); // Nền trắng

            // Dời gốc tọa độ để hình nằm giữa trang
            gfx.TranslateTransform(-minX + padding, -minY + padding);

            // 3. VẼ DÂY (Vẽ trước để nằm dưới Node)
            foreach (var conn in connections)
            {
                DrawConnection(gfx, conn);
            }

            // 4. VẼ NODE (Vẽ đè lên dây)
            foreach (var node in nodeList)
            {
                DrawNode(gfx, node);
            }

            // 5. LƯU FILE
            document.Save(filePath);
        }

        private void DrawConnection(XGraphics gfx, ConnectionViewModel conn)
        {
            if (conn.Source == null || conn.Target == null) return;

            XColor strokeColor = ToXColor(conn.StrokeColor);
            XPen pen = new XPen(strokeColor, conn.Thickness);

            // Lấy tâm của 2 node
            double x1 = conn.Source.X + conn.Source.Width / 2;
            double y1 = conn.Source.Y + conn.Source.Height / 2;
            double x2 = conn.Target.X + conn.Target.Width / 2;
            double y2 = conn.Target.Y + conn.Target.Height / 2;

            // NÂNG CẤP: Vẽ đường cong Bezier cho mềm mại (giống XMind)
            // Tính điểm điều khiển (Control Points) để uốn cong dây
            double offset = Math.Abs(x2 - x1) * 0.5;
            XPoint start = new XPoint(x1, y1);
            XPoint end = new XPoint(x2, y2);
            XPoint p1 = new XPoint(x1 + offset, y1); // Kéo ra ngang từ điểm đầu
            XPoint p2 = new XPoint(x2 - offset, y2); // Kéo ra ngang từ điểm cuối

            gfx.DrawBezier(pen, start, p1, p2, end);
        }

        private void DrawNode(XGraphics gfx, NodeViewModel node)
        {
            XColor bgColor = ToXColor(node.BackgroundColor);
            XColor borderColor = ToXColor(node.BorderColor);
            XBrush bgBrush = new XSolidBrush(bgColor);
            XPen borderPen = new XPen(borderColor, 1);

            // --- XỬ LÝ HÌNH DÁNG (FLEXIBILITY) ---
            switch (node.Shape)
            {
                case "Ellipse":
                    gfx.DrawEllipse(borderPen, bgBrush, node.X, node.Y, node.Width, node.Height);
                    break;

                case "Diamond":
                    // Tính 4 đỉnh hình thoi
                    XPoint p1 = new XPoint(node.X + node.Width / 2, node.Y); // Đỉnh trên
                    XPoint p2 = new XPoint(node.X + node.Width, node.Y + node.Height / 2); // Đỉnh phải
                    XPoint p3 = new XPoint(node.X + node.Width / 2, node.Y + node.Height); // Đỉnh dưới
                    XPoint p4 = new XPoint(node.X, node.Y + node.Height / 2); // Đỉnh trái
                    gfx.DrawPolygon(borderPen, bgBrush, new[] { p1, p2, p3, p4 }, XFillMode.Winding);
                    break;

                case "Rectangle": // Hình chữ nhật vuông góc
                    gfx.DrawRectangle(borderPen, bgBrush, node.X, node.Y, node.Width, node.Height);
                    break;

                case "RoundedRectangle":
                default:
                    gfx.DrawRoundedRectangle(borderPen, bgBrush, node.X, node.Y, node.Width, node.Height, 10, 10);
                    break;
            }

            // --- VẼ TEXT ---
            DrawNodeText(gfx, node);
        }

        private void DrawNodeText(XGraphics gfx, NodeViewModel node)
        {
            XColor textColor = ToXColor(node.TextColor);
            XBrush textBrush = new XSolidBrush(textColor);

            // Xử lý Font (Fallback về Arial nếu tên font lạ)
            string fontName = string.IsNullOrEmpty(node.FontFamily) ? "Arial" : node.FontFamily;

            // Fix lỗi XFontStyle.Bold: Dùng ép kiểu số nguyên (1 = Bold, 0 = Regular)
            XFont font = new XFont(fontName, node.FontSize, (PdfSharp.Drawing.XFontStyleEx)1);

            XRect layoutRect = new XRect(node.X, node.Y, node.Width, node.Height);
            XStringFormat format = new XStringFormat();
            format.Alignment = XStringAlignment.Center;
            format.LineAlignment = XLineAlignment.Center;

            // Nếu node quá bé, text có thể bị tràn. Ở đây ta vẽ đơn giản.
            // Để chuyên nghiệp hơn cần dùng XTextFormatter (thư viện layout text)
            gfx.DrawString(node.Title, font, textBrush, layoutRect, format);
        }

        private XColor ToXColor(System.Windows.Media.Color wpfColor)
        {
            return XColor.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
        }
    }
}
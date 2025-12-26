using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Security;
using System;
using System.Collections.Generic;
using System.Windows;
using MindmapApp.ViewModels;
using System.Linq;

namespace MindmapApp.Services
{
    public class PdfExportService
    {
        public void ExportPdf(string filePath, string title, IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections, string author, bool showWatermark = true, string? password = null)
        {
            try
            {
                // 1. Tạo tài liệu PDF
                PdfDocument document = new PdfDocument();
                document.Info.Title = title;
                document.Info.Author = author;

                // Cấu hình bảo mật (Giữ nguyên)
                if (!string.IsNullOrEmpty(password))
                {
                    document.Version = 14;
                    document.SecuritySettings.UserPassword = password;
                    document.SecuritySettings.OwnerPassword = Guid.NewGuid().ToString();
                    document.SecuritySettings.PermitFullQualityPrint = true;
                    document.SecuritySettings.PermitPrint = true;
                    document.SecuritySettings.PermitModifyDocument = false;
                    document.SecuritySettings.PermitAssembleDocument = false;
                    document.SecuritySettings.PermitExtractContent = false;
                    document.SecuritySettings.PermitAnnotations = false;
                    document.SecuritySettings.PermitFormsFill = false;
                }

                if (!nodes.Any())
                {
                    document.Save(filePath);
                    return;
                }

                // 2. Tính toán biên
                double minX = nodes.Min(n => n.X);
                double minY = nodes.Min(n => n.Y);
                double maxX = nodes.Max(n => n.X + n.Width);
                double maxY = nodes.Max(n => n.Y + n.Height);

                double padding = 50;
                double width = maxX - minX + (padding * 2);
                double height = maxY - minY + (padding * 2);

                PdfPage page = document.AddPage();
                page.Width = XUnit.FromPoint(width);
                page.Height = XUnit.FromPoint(height);

                XGraphics gfx = XGraphics.FromPdfPage(page);
                gfx.TranslateTransform(padding - minX, padding - minY);

                // 3. Vẽ Dây và Mũi tên
                foreach (var conn in connections)
                {
                    if (conn.Source == null || conn.Target == null) continue;

                    XColor strokeColor = XColor.FromArgb(conn.StrokeColor.A, conn.StrokeColor.R, conn.StrokeColor.G, conn.StrokeColor.B);
                    XPen pen = new XPen(strokeColor, conn.Thickness);

                    if (conn.LineStyle == "Dashed") pen.DashStyle = XDashStyle.Dash;

                    Rect sourceRect = new Rect(conn.Source.X, conn.Source.Y, conn.Source.Width, conn.Source.Height);
                    Rect targetRect = new Rect(conn.Target.X, conn.Target.Y, conn.Target.Width, conn.Target.Height);

                    Point sourceCenter = new Point(sourceRect.X + sourceRect.Width / 2, sourceRect.Y + sourceRect.Height / 2);
                    Point targetCenter = new Point(targetRect.X + targetRect.Width / 2, targetRect.Y + targetRect.Height / 2);

                    Point pStart = GetIntersectionPoint(sourceRect, sourceCenter, targetCenter); // Điểm mép thực tế
                    Point pEnd = GetIntersectionPoint(targetRect, targetCenter, sourceCenter);   // Điểm mép thực tế

                    Vector direction = targetCenter - sourceCenter;
                    if (direction.Length > 0) direction.Normalize();

                    double overlap = 3.0;
                    Point startPoint = pStart - (direction * overlap);
                    Point endPoint = pEnd + (direction * overlap);

                    double distanceX = Math.Abs(endPoint.X - startPoint.X);
                    double distanceY = Math.Abs(endPoint.Y - startPoint.Y);

                    double controlDist = Math.Max(distanceX / 2, 50);
                    if (distanceY > 100 && distanceX < 50) controlDist = distanceY / 3;

                    Point control1 = new Point(startPoint.X + controlDist, startPoint.Y);
                    Point control2 = new Point(endPoint.X - controlDist, endPoint.Y);

                    // Vẽ đường dây cong
                    gfx.DrawBezier(pen,
                        new XPoint(startPoint.X, startPoint.Y),
                        new XPoint(control1.X, control1.Y),
                        new XPoint(control2.X, control2.Y),
                        new XPoint(endPoint.X, endPoint.Y));

                    // [SỬA LỖI] Vẽ mũi tên
                    if (conn.ArrowStyle == "Arrow")
                    {
                        // Tính vector tiếp tuyến tại điểm cuối
                        Vector tangent = endPoint - control2;

                        // Fix lỗi: Nếu vector quá nhỏ (do thẳng hàng hoặc quá gần), dùng vector nối 2 điểm đầu cuối
                        if (tangent.Length < 0.1)
                        {
                            tangent = endPoint - startPoint;
                        }

                        // Vẽ mũi tên tại pEnd (điểm nằm trên biên Node) để không bị che khuất
                        DrawArrow(gfx, pen.Color, new XPoint(pEnd.X, pEnd.Y), tangent);
                    }
                }

                // 4. Vẽ Nodes
                foreach (var node in nodes)
                {
                    var model = node.ToModel();

                    XColor nodeBg = XColor.FromArgb(node.BackgroundColor.A, node.BackgroundColor.R, node.BackgroundColor.G, node.BackgroundColor.B);
                    XColor nodeBorder = XColor.FromArgb(node.BorderColor.A, node.BorderColor.R, node.BorderColor.G, node.BorderColor.B);
                    XBrush brush = new XSolidBrush(nodeBg);
                    XPen borderPen = new XPen(nodeBorder, 2);

                    XRect rect = new XRect(node.X, node.Y, node.Width, node.Height);

                    if (node.Shape == "Ellipse")
                        gfx.DrawEllipse(borderPen, brush, rect);
                    else
                        gfx.DrawRoundedRectangle(borderPen, brush, rect, new XSize(10, 10));

                    XColor textColor = XColor.FromArgb(node.TextColor.A, node.TextColor.R, node.TextColor.G, node.TextColor.B);
                    XBrush textBrush = new XSolidBrush(textColor);

                    XFontStyleEx style = XFontStyleEx.Regular;
                    if (model.IsBold) style |= XFontStyleEx.Bold;
                    if (model.IsItalic) style |= XFontStyleEx.Italic;
                    if (style == (XFontStyleEx.Bold | XFontStyleEx.Italic)) style = XFontStyleEx.BoldItalic;

                    XFont font = new XFont(node.FontFamily ?? "Arial", node.FontSize, style);

                    gfx.DrawString(node.Title, font, textBrush, rect, XStringFormats.Center);
                }

                // 5. Watermark
                if (showWatermark)
                {
                    gfx.TranslateTransform(-(padding - minX), -(padding - minY));

                    string watermarkText = $"Created by {author}";
                    XFont watermarkFont = new XFont("Arial", 10, XFontStyleEx.Italic);
                    XBrush watermarkBrush = new XSolidBrush(XColor.FromArgb(128, 100, 100, 100));

                    XSize textSize = gfx.MeasureString(watermarkText, watermarkFont);
                    double margin = 20;
                    double x = page.Width.Point - textSize.Width - margin;
                    double y = page.Height.Point - textSize.Height - margin;

                    gfx.DrawString(watermarkText, watermarkFont, watermarkBrush, new XPoint(x, y));
                }

                document.Save(filePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi xuất PDF: {ex.Message}");
            }
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

        private void DrawArrow(XGraphics gfx, XColor color, XPoint tip, Vector direction)
        {
            if (direction.Length > 0) direction.Normalize();
            Vector vLeft = new Vector(-direction.Y, direction.X);
            Vector vBack = -direction;
            double arrowLen = 10;
            double arrowWidth = 4;
            XPoint p1 = new XPoint(tip.X + vBack.X * arrowLen + vLeft.X * arrowWidth, tip.Y + vBack.Y * arrowLen + vLeft.Y * arrowWidth);
            XPoint p2 = new XPoint(tip.X + vBack.X * arrowLen - vLeft.X * arrowWidth, tip.Y + vBack.Y * arrowLen - vLeft.Y * arrowWidth);
            XBrush brush = new XSolidBrush(color);
            gfx.DrawPolygon(XPens.Transparent, brush, new XPoint[] { tip, p1, p2 }, XFillMode.Winding);
        }
    }
}
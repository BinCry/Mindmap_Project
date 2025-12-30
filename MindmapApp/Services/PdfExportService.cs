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

                // Cấu hình bảo mật
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

                // 2. Tính toán biên và trang
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

                // 3. VẼ DÂY VÀ MŨI TÊN (Logic mới: Aspect Ratio & 4 Hướng)
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

                    Point startPoint, endPoint;
                    bool hasArrow = (conn.ArrowStyle.ToString() == "Arrow");

                    // --- A. XÁC ĐỊNH ĐIỂM ĐẦU / CUỐI ---
                    if (hasArrow)
                    {
                        startPoint = sourceCenter;

                        // 1. Tìm điểm cắt dựa trên Tỷ lệ khung hình
                        Point rawEndPoint = GetIntersectionPoint(targetRect, sourceCenter, targetCenter);

                        // 2. Tính vector hướng dựa trên vị trí thực tế
                        Vector dir;
                        bool isSideHorizontal = Math.Abs(rawEndPoint.X - targetRect.Left) < 0.1 || Math.Abs(rawEndPoint.X - targetRect.Right) < 0.1;

                        if (isSideHorizontal)
                            dir = new Vector(targetCenter.X - sourceCenter.X, 0); // Ngang
                        else
                            dir = new Vector(0, targetCenter.Y - sourceCenter.Y); // Dọc

                        if (dir.Length > 0) dir.Normalize();

                        // 3. Thụt vào 3px
                        endPoint = rawEndPoint - (dir * 3.0);
                    }
                    else
                    {
                        startPoint = sourceCenter;
                        endPoint = targetCenter;
                    }

                    // --- B. VẼ DÂY (BEZIER THÔNG MINH) ---
                    bool isCurved = (conn.ConnectionStyle != ConnectionStyle.Straight);
                    Point control2 = endPoint;

                    if (isCurved)
                    {
                        double diffX = endPoint.X - startPoint.X;
                        double diffY = endPoint.Y - startPoint.Y;
                        Point c1, c2;

                        bool isHorizontalCurve;
                        if (hasArrow)
                        {
                            // Có mũi tên: dựa vào cạnh đích
                            isHorizontalCurve = Math.Abs(endPoint.X - targetRect.Left) < 5.0 || Math.Abs(endPoint.X - targetRect.Right) < 5.0;
                        }
                        else
                        {
                            // Nối tâm: dựa vào tỷ lệ khung hình
                            double normalizedX = Math.Abs(diffX) / (targetRect.Width > 0 ? targetRect.Width : 1);
                            double normalizedY = Math.Abs(diffY) / (targetRect.Height > 0 ? targetRect.Height : 1);
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

                        control2 = c2;
                        gfx.DrawBezier(pen,
                            new XPoint(startPoint.X, startPoint.Y),
                            new XPoint(c1.X, c1.Y),
                            new XPoint(c2.X, c2.Y),
                            new XPoint(endPoint.X, endPoint.Y));
                    }
                    else
                    {
                        gfx.DrawLine(pen, new XPoint(startPoint.X, startPoint.Y), new XPoint(endPoint.X, endPoint.Y));
                        control2 = startPoint;
                    }

                    // --- C. VẼ MŨI TÊN ---
                    if (hasArrow)
                    {
                        Vector tangent = endPoint - control2;
                        if (tangent.Length < 0.1) tangent = endPoint - startPoint;

                        DrawArrow(gfx, strokeColor, new XPoint(endPoint.X, endPoint.Y), tangent);
                    }
                }

                // 4. VẼ NODES
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

        // --- HÀM TÍNH GIAO ĐIỂM (ASPECT RATIO LOGIC) ---
        private Point GetIntersectionPoint(Rect targetRect, Point sourceCenter, Point targetCenter)
        {
            double dx = targetCenter.X - sourceCenter.X;
            double dy = targetCenter.Y - sourceCenter.Y;

            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) return targetCenter;

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

        private void DrawArrow(XGraphics gfx, XColor color, XPoint tip, Vector direction)
        {
            if (direction.Length > 0) direction.Normalize();
            Vector vLeft = new Vector(-direction.Y, direction.X);
            Vector vBack = -direction;
            double arrowLen = 18;
            double arrowWidth = 12;

            XPoint p1 = new XPoint(tip.X + vBack.X * arrowLen + vLeft.X * (arrowWidth / 2), tip.Y + vBack.Y * arrowLen + vLeft.Y * (arrowWidth / 2));
            XPoint p2 = new XPoint(tip.X + vBack.X * arrowLen - vLeft.X * (arrowWidth / 2), tip.Y + vBack.Y * arrowLen - vLeft.Y * (arrowWidth / 2));

            XBrush brush = new XSolidBrush(color);
            gfx.DrawPolygon(XPens.Transparent, brush, new XPoint[] { tip, p1, p2 }, XFillMode.Winding);
        }
    }
}
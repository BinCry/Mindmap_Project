using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MindmapApp.ViewModels;

namespace MindmapApp.Services
{
    public class ImageExportService
    {
        public void ExportImage(string filePath, IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections, string author)
        {
            if (!nodes.Any()) return;

            // 1. Tính toán kích thước ảnh
            double minX = nodes.Min(n => n.X);
            double minY = nodes.Min(n => n.Y);
            double maxX = nodes.Max(n => n.X + n.Width);
            double maxY = nodes.Max(n => n.Y + n.Height);

            double padding = 50;
            double width = maxX - minX + (padding * 2);
            double height = maxY - minY + (padding * 2);

            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                // Vẽ nền trắng
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                dc.PushTransform(new TranslateTransform(padding - minX, padding - minY));

                // 2. VẼ DÂY VÀ MŨI TÊN
                foreach (var conn in connections)
                {
                    if (conn.Source == null || conn.Target == null) continue;

                    Brush strokeBrush = new SolidColorBrush(conn.StrokeColor);
                    Pen pen = new Pen(strokeBrush, conn.Thickness);
                    if (conn.LineStyle == "Dashed") pen.DashStyle = DashStyles.Dash;

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
                        Point rawEndPoint = GetIntersectionPoint(targetRect, sourceCenter, targetCenter);

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

                    // --- B. VẼ DÂY ---
                    bool isCurved = (conn.ConnectionStyle != ConnectionStyle.Straight);
                    Point control2 = endPoint;

                    StreamGeometry geometry = new StreamGeometry();
                    using (StreamGeometryContext ctx = geometry.Open())
                    {
                        ctx.BeginFigure(startPoint, false, false);

                        if (isCurved)
                        {
                            double diffX = endPoint.X - startPoint.X;
                            double diffY = endPoint.Y - startPoint.Y;
                            Point c1, c2;

                            bool isHorizontalCurve;
                            if (hasArrow)
                                isHorizontalCurve = Math.Abs(endPoint.X - targetRect.Left) < 5.0 || Math.Abs(endPoint.X - targetRect.Right) < 5.0;
                            else
                            {
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
                            ctx.BezierTo(c1, c2, endPoint, true, true);
                        }
                        else
                        {
                            ctx.LineTo(endPoint, true, true);
                            control2 = startPoint;
                        }
                    }
                    dc.DrawGeometry(null, pen, geometry);

                    // --- C. VẼ MŨI TÊN ---
                    if (hasArrow)
                    {
                        Vector tangent = endPoint - control2;
                        if (tangent.Length < 0.1) tangent = endPoint - startPoint;
                        if (tangent.Length > 0) tangent.Normalize();

                        Vector vBack = -tangent;
                        Vector vLeft = new Vector(-tangent.Y, tangent.X);
                        double arrowLen = 18;
                        double arrowWidth = 12;

                        Point tip = endPoint;
                        Point p1 = tip + (vBack * arrowLen) + (vLeft * (arrowWidth / 2));
                        Point p2 = tip + (vBack * arrowLen) - (vLeft * (arrowWidth / 2));

                        StreamGeometry arrowGeo = new StreamGeometry();
                        using (StreamGeometryContext ctx = arrowGeo.Open())
                        {
                            ctx.BeginFigure(tip, true, true);
                            ctx.PolyLineTo(new[] { p1, p2 }, true, true);
                        }
                        dc.DrawGeometry(strokeBrush, null, arrowGeo);
                    }
                }

                // 3. VẼ NODES (ĐÃ SỬA LỖI CĂN CHỮ)
                foreach (var node in nodes)
                {
                    Brush bgBrush = new SolidColorBrush(node.BackgroundColor);
                    Brush borderBrush = new SolidColorBrush(node.BorderColor);
                    Pen borderPen = new Pen(borderBrush, 2);

                    Rect rect = new Rect(node.X, node.Y, node.Width, node.Height);

                    if (node.Shape == "Ellipse")
                        dc.DrawEllipse(bgBrush, borderPen, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2);
                    else
                        dc.DrawRoundedRectangle(bgBrush, borderPen, rect, 10, 10);

                    string fwStr = node.FontWeight?.ToString() ?? "Normal";
                    FontWeight fw = fwStr.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? FontWeights.Bold : FontWeights.Normal;
                    FontStyle fs = FontStyles.Normal;

                    var typeface = new Typeface(new FontFamily(node.FontFamily ?? "Segoe UI"), fs, fw, FontStretches.Normal);

                    FormattedText text = new FormattedText(
                        node.Title,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        node.FontSize,
                        new SolidColorBrush(node.TextColor),
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);

                    // --- SỬA LỖI CĂN GIỮA Ở ĐÂY ---
                    // 1. Căn giữa chữ trong khung text
                    text.TextAlignment = TextAlignment.Center;

                    // 2. Đặt khung text rộng bằng chiều rộng Node (trừ padding an toàn 10px)
                    text.MaxTextWidth = Math.Max(1, node.Width - 10);
                    text.MaxTextHeight = Math.Max(1, node.Height - 10);

                    // 3. Tính tọa độ vẽ: 
                    // X: Bắt đầu từ mép trái + 5px (để cân bằng với việc trừ 10px chiều rộng)
                    // Y: Vẫn tính thủ công để căn giữa theo chiều dọc
                    double drawX = rect.X + 5;
                    double drawY = rect.Y + (rect.Height - text.Height) / 2;

                    dc.DrawText(text, new Point(drawX, drawY));
                }
            }

            // Xuất file
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            BitmapEncoder encoder;
            if (filePath.EndsWith(".jpg")) encoder = new JpegBitmapEncoder();
            else encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }

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
    }
}
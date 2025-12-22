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
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                dc.PushTransform(new TranslateTransform(padding - minX, padding - minY));

                // 3. Vẽ Dây cong
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

                    Point pStart = GetIntersectionPoint(sourceRect, sourceCenter, targetCenter);
                    Point pEnd = GetIntersectionPoint(targetRect, targetCenter, sourceCenter);

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

                    StreamGeometry geometry = new StreamGeometry();
                    using (StreamGeometryContext ctx = geometry.Open())
                    {
                        ctx.BeginFigure(startPoint, false, false);
                        ctx.BezierTo(control1, control2, endPoint, true, true);
                    }
                    dc.DrawGeometry(null, pen, geometry);
                }

                // 4. Vẽ Nodes
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

                    // --- FIX LỖI: Tự động phát hiện Bold/Italic từ chuỗi FontWeight/FontStyle ---
                    string fwStr = node.FontWeight?.ToString() ?? "Normal";
                    // Giả sử FontStyle chưa có trong ViewModel cũ

                    FontWeight fw = fwStr.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? FontWeights.Bold : FontWeights.Normal;
                    FontStyle fs = FontStyles.Normal; // Mặc định normal nếu chưa có property

                    var typeface = new Typeface(new FontFamily(node.FontFamily ?? "Segoe UI"),
                        fs,
                        fw,
                        FontStretches.Normal);

                    FormattedText text = new FormattedText(
                        node.Title,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        node.FontSize,
                        new SolidColorBrush(node.TextColor),
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);

                    text.TextAlignment = TextAlignment.Center;
                    Point textPos = new Point(rect.X + (rect.Width - text.Width) / 2, rect.Y + (rect.Height - text.Height) / 2);

                    dc.DrawText(text, new Point(rect.X + rect.Width / 2, rect.Y + (rect.Height - text.Height) / 2));
                }
            }

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
    }
}
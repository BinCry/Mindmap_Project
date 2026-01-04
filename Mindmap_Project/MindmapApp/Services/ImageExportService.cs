using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
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

                // 3. VẼ NODES
                foreach (var node in nodes)
                {
                    Brush bgBrush = new SolidColorBrush(node.BackgroundColor);
                    Brush borderBrush = new SolidColorBrush(node.BorderColor);
                    Pen borderPen = new Pen(borderBrush, 2);

                    Rect rect = new Rect(node.X, node.Y, node.Width, node.Height);

                    Geometry shapeGeometry = CreateNodeGeometry(node.Shape, rect);
                    dc.DrawGeometry(bgBrush, borderPen, shapeGeometry);

                    var contentBitmap = RenderFlowDocumentBitmap(node, rect.Width, rect.Height, out var contentHeight);
                    if (contentBitmap != null)
                    {
                        double offsetY = Math.Max(0, (rect.Height - contentHeight) / 2);
                        dc.DrawImage(contentBitmap, new Rect(rect.X, rect.Y + offsetY, rect.Width, Math.Min(rect.Height, contentHeight)));
                    }
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

        private static Geometry CreateNodeGeometry(string shape, Rect rect)
        {
            switch (shape)
            {
                case "Rectangle":
                    return new RectangleGeometry(rect);
                case "RoundedRectangle":
                    return new RectangleGeometry(rect, 12, 12);
                case "Ellipse":
                    return new EllipseGeometry(rect);
                case "Diamond":
                    return CreatePolygonGeometry(rect, new[]
                    {
                        new Point(rect.X + rect.Width * 0.5, rect.Y),
                        new Point(rect.Right, rect.Y + rect.Height * 0.5),
                        new Point(rect.X + rect.Width * 0.5, rect.Bottom),
                        new Point(rect.X, rect.Y + rect.Height * 0.5)
                    });
                case "Parallelogram":
                    return CreatePolygonGeometry(rect, new[]
                    {
                        new Point(rect.X + rect.Width * 0.2, rect.Y),
                        new Point(rect.Right, rect.Y),
                        new Point(rect.X + rect.Width * 0.8, rect.Bottom),
                        new Point(rect.X, rect.Bottom)
                    });
                case "Hexagon":
                    return CreatePolygonGeometry(rect, new[]
                    {
                        new Point(rect.X + rect.Width * 0.15, rect.Y),
                        new Point(rect.X + rect.Width * 0.85, rect.Y),
                        new Point(rect.Right, rect.Y + rect.Height * 0.5),
                        new Point(rect.X + rect.Width * 0.85, rect.Bottom),
                        new Point(rect.X + rect.Width * 0.15, rect.Bottom),
                        new Point(rect.X, rect.Y + rect.Height * 0.5)
                    });
                default:
                    return new RectangleGeometry(rect, 12, 12);
            }
        }

        private static Geometry CreatePolygonGeometry(Rect rect, Point[] points)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], true, true);
                ctx.PolyLineTo(points.Skip(1).ToArray(), true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        private static BitmapSource? RenderFlowDocumentBitmap(NodeViewModel node, double width, double height, out double contentHeight)
        {
            FlowDocument document;
            contentHeight = height;
            try
            {
                if (!string.IsNullOrWhiteSpace(node.ContentXaml))
                {
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ContentXaml));
                    document = (FlowDocument)XamlReader.Load(stream);
                }
                else
                {
                    document = new FlowDocument();
                }
            }
            catch
            {
                document = new FlowDocument();
            }

            document.PagePadding = new Thickness(0);
            document.ColumnWidth = width;
            document.PageWidth = width;
            document.PageHeight = height;
            document.FontFamily = new FontFamily(node.FontFamily ?? "Arial");
            document.FontSize = node.FontSize;
            document.Foreground = new SolidColorBrush(node.TextColor);
            var richTextBox = new RichTextBox
            {
                Document = document,
                Width = width,
                Height = double.NaN,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };

            richTextBox.Measure(new Size(width, double.PositiveInfinity));
            double desiredHeight = Math.Ceiling(richTextBox.DesiredSize.Height);
            contentHeight = Math.Min(height, Math.Max(1, desiredHeight));
            richTextBox.Height = contentHeight;
            richTextBox.Arrange(new Rect(0, 0, width, contentHeight));
            richTextBox.UpdateLayout();

            var rtb = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(contentHeight), 96, 96, PixelFormats.Pbgra32);
            rtb.Render(richTextBox);
            rtb.Freeze();
            return rtb;
        }
    }
}

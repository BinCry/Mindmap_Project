using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MindmapApp.Models;

namespace MindmapApp.Services
{
    public class MindmapThumbnailService
    {
        public ImageSource? CreateThumbnail(MindmapDocument document, int width, int height)
        {
            if (document.Nodes.Count == 0 || width <= 0 || height <= 0) return null;

            double minX = document.Nodes.Min(n => n.X);
            double minY = document.Nodes.Min(n => n.Y);
            double maxX = document.Nodes.Max(n => n.X + n.Width);
            double maxY = document.Nodes.Max(n => n.Y + n.Height);

            double contentWidth = Math.Max(1, maxX - minX);
            double contentHeight = Math.Max(1, maxY - minY);

            double padding = 12;
            double scaleX = (width - (padding * 2)) / contentWidth;
            double scaleY = (height - (padding * 2)) / contentHeight;
            double scale = Math.Max(0.1, Math.Min(scaleX, scaleY));

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var backgroundBrush = new SolidColorBrush(document.CanvasBackgroundColor);
                backgroundBrush.Opacity = 0.9;
                dc.DrawRectangle(backgroundBrush, null, new Rect(0, 0, width, height));

                dc.PushTransform(new TranslateTransform(padding - (minX * scale), padding - (minY * scale)));
                dc.PushTransform(new ScaleTransform(scale, scale));

                foreach (var conn in document.Connections)
                {
                    var source = document.Nodes.FirstOrDefault(n => n.Id == conn.SourceId);
                    var target = document.Nodes.FirstOrDefault(n => n.Id == conn.TargetId);
                    if (source == null || target == null) continue;

                    var stroke = new SolidColorBrush(conn.StrokeColor) { Opacity = 0.4 };
                    var pen = new Pen(stroke, Math.Max(1, conn.Thickness / 2));

                    var start = new Point(source.X + (source.Width / 2), source.Y + (source.Height / 2));
                    var end = new Point(target.X + (target.Width / 2), target.Y + (target.Height / 2));
                    dc.DrawLine(pen, start, end);
                }

                foreach (var node in document.Nodes)
                {
                    var background = new SolidColorBrush(node.BackgroundColor) { Opacity = 0.7 };
                    var border = new Pen(new SolidColorBrush(node.BorderColor) { Opacity = 0.5 }, 1.5);
                    var rect = new Rect(node.X, node.Y, node.Width, node.Height);

                    if (node.Shape == "Ellipse")
                    {
                        dc.DrawEllipse(background, border, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2);
                    }
                    else
                    {
                        dc.DrawRoundedRectangle(background, border, rect, 6, 6);
                    }
                }

                dc.Pop();
                dc.Pop();
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}

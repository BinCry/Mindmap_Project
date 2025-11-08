using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MindmapApp.Services;

public class MindmapExportService
{
    public async Task SaveAsImageAsync(FrameworkElement element, string filePath)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Đường dẫn không hợp lệ", nameof(filePath));

        await element.Dispatcher.InvokeAsync(() =>
        {
            var size = new Size(element.ActualWidth, element.ActualHeight);
            if (size.Width <= 0 || size.Height <= 0)
            {
                size = new Size(element.Width, element.Height);
            }

            var dpi = 144d;
            var renderTarget = new RenderTargetBitmap((int)(size.Width * dpi / 96), (int)(size.Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var brush = new VisualBrush(element);
                context.DrawRectangle(brush, null, new Rect(new Point(0, 0), size));
            }

            renderTarget.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);
        });
    }

    public async Task SaveAsPdfAsync(FrameworkElement element, string filePath)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Đường dẫn không hợp lệ", nameof(filePath));

        byte[]? imageBytes = null;
        await element.Dispatcher.InvokeAsync(() =>
        {
            var size = new Size(element.ActualWidth, element.ActualHeight);
            if (size.Width <= 0 || size.Height <= 0)
            {
                size = new Size(element.Width, element.Height);
            }

            var renderTarget = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(new VisualBrush(element), null, new Rect(new Point(0, 0), size));
            }

            renderTarget.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            imageBytes = ms.ToArray();
        });

        if (imageBytes == null)
        {
            return;
        }

        using var document = new PdfDocument();
        var page = document.AddPage();
        using var xGraphics = XGraphics.FromPdfPage(page);
        using var imageStream = new MemoryStream(imageBytes);
        using var xImage = XImage.FromStream(() => imageStream);

        page.Width = xImage.PixelWidth * 72 / xImage.HorizontalResolution;
        page.Height = xImage.PixelHeight * 72 / xImage.VerticalResolution;

        xGraphics.DrawImage(xImage, 0, 0, page.Width, page.Height);
        document.Save(filePath);
    }
}

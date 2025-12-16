using Microsoft.Win32;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Cần cho Thumb (Node)
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes; // Quan trọng: Cần cho Shape/Path (Dây nối)
using MindmapApp.Views;

namespace MindmapApp.Services;

public class MindmapExportService
{
    // --- CẤU HÌNH RENDER ---
    private const double ExportDpi = 300.0;           // 300 DPI: Chuẩn in ấn nét căng
    private const double PageMargin = 40.0;           // Lề giấy PDF
    private const double ContentPadding = 50.0;       // [FIXED] Lề ảnh bao quanh Mindmap
    private const double MaxPixelDimension = 12000.0; // Giới hạn pixel an toàn cho RAM

    // Danh sách lưu trữ hành động hoàn tác (Undo) để phục hồi giao diện
    private List<Action> _revertActions = new List<Action>();

    // --- 1. XUẤT ẢNH (PNG) ---
    public async Task SaveAsImageAsync(FrameworkElement element, string defaultFileName)
    {
        if (element == null) return;

        SaveFileDialog dlg = new SaveFileDialog
        {
            FileName = defaultFileName,
            DefaultExt = ".png",
            Filter = "PNG Image (.png)|*.png|JPEG Image (.jpg)|*.jpg"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            await SaveAsImageInternal(element, dlg.FileName);
            MessageBox.Show("Đã xuất ảnh chất lượng cao thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // --- 2. XUẤT PDF (CĂN GIỮA & FIT KHUNG) ---
    public async Task SaveAsPdfAsync(FrameworkElement element, string defaultFileName, string authorName)
    {
        if (element == null) return;

        var optionWindow = new PdfExportOptionsWindow();
        if (Application.Current.MainWindow != null)
            optionWindow.Owner = Application.Current.MainWindow;

        if (optionWindow.ShowDialog() != true) return;

        SaveFileDialog dlg = new SaveFileDialog
        {
            FileName = defaultFileName,
            DefaultExt = ".pdf",
            Filter = "PDF Document (.pdf)|*.pdf"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            await SaveAsPdfInternal(element, dlg.FileName, optionWindow.Password, optionWindow.IncludeWatermark, authorName);
            MessageBox.Show("Đã xuất PDF thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất PDF: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==========================================================
    // CORE RENDER ENGINE (FIX TRANG TRẮNG & MẤT BIẾN)
    // ==========================================================

    private async Task<byte[]?> RenderElementToBytesAsync(FrameworkElement element)
    {
        return await element.Dispatcher.InvokeAsync<byte[]?>(() =>
        {
            GC.Collect();
            _revertActions.Clear();

            // 1. RESET ZOOM VỀ 100% (BẮT BUỘC ĐỂ KHÔNG BỊ TRẮNG TRANG)
            // Lưu lại Transform hiện tại
            Transform originalTransform = element.LayoutTransform;

            // Ép buộc Mindmap về kích thước thật (Scale 1.0)
            if (element.LayoutTransform is ScaleTransform scaleTransform)
            {
                double oldX = scaleTransform.ScaleX;
                double oldY = scaleTransform.ScaleY;

                // Lưu hành động hoàn tác
                _revertActions.Add(() =>
                {
                    scaleTransform.ScaleX = oldX;
                    scaleTransform.ScaleY = oldY;
                });

                // Reset về 1.0
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
            }
            else
            {
                // Trường hợp dùng MatrixTransform hoặc cái khác
                element.LayoutTransform = new ScaleTransform(1.0, 1.0);
                _revertActions.Add(() => element.LayoutTransform = originalTransform);
            }

            // Cập nhật layout ngay lập tức để hệ thống tính toán lại tọa độ
            element.UpdateLayout();

            // 2. TÍNH TOÁN VÙNG CHỨA NỘI DUNG (INK HUNTING)
            // Chỉ lấy vùng có Node và Dây, cắt bỏ khoảng trắng thừa
            Rect contentBounds = GetPreciseContentBounds(element);

            if (contentBounds == Rect.Empty || contentBounds.Width <= 0 || contentBounds.Height <= 0)
            {
                RestoreState();
                throw new Exception("Mindmap trống, không tìm thấy nội dung để xuất.");
            }

            // 3. TÍNH TOÁN ĐỘ PHÂN GIẢI (HIGH QUALITY)
            // Tính toán tỉ lệ phóng đại để đạt 300 DPI
            double dpiScale = ExportDpi / 96.0;

            // Kiểm tra giới hạn RAM an toàn
            if (contentBounds.Width * dpiScale > MaxPixelDimension)
                dpiScale = MaxPixelDimension / contentBounds.Width;
            if (contentBounds.Height * dpiScale > MaxPixelDimension)
                dpiScale = MaxPixelDimension / contentBounds.Height;

            // Đảm bảo không bao giờ bị vỡ hình (tối thiểu 1.0)
            if (dpiScale < 1.0) dpiScale = 1.0;

            int renderWidth = (int)(contentBounds.Width * dpiScale);
            int renderHeight = (int)(contentBounds.Height * dpiScale);

            // 4. XỬ LÝ GIAO DIỆN (MÀU SẮC & BACKGROUND)
            Panel? panel = element as Panel;
            Brush originalBackground = Brushes.Transparent;

            if (panel != null)
            {
                originalBackground = panel.Background;
                panel.Background = Brushes.Transparent; // Ẩn nền Grid tối đi
            }

            // --- SMART CONTRAST (BẢO VỆ TEXT TRONG NODE) ---
            ApplySmartContrast(element);

            element.UpdateLayout();

            RenderTargetBitmap? renderTarget = null;
            DrawingVisual visual = new DrawingVisual();

            try
            {
                // Tạo ảnh Bitmap với độ phân giải cao
                renderTarget = new RenderTargetBitmap(renderWidth, renderHeight, 96.0 * dpiScale, 96.0 * dpiScale, PixelFormats.Pbgra32);

                using (DrawingContext context = visual.RenderOpen())
                {
                    // A. Vẽ nền TRẮNG GIẤY (Full kích thước ảnh)
                    context.DrawRectangle(Brushes.White, null, new Rect(0, 0, contentBounds.Width, contentBounds.Height));

                    // B. Vẽ nội dung Mindmap đè lên
                    VisualBrush vb = new VisualBrush(element);
                    vb.Viewbox = contentBounds; // Cắt đúng vùng chứa nội dung (Auto-Crop)
                    vb.ViewboxUnits = BrushMappingMode.Absolute;
                    vb.Stretch = Stretch.Fill; // Fill đầy khung hình

                    context.DrawRectangle(vb, null, new Rect(0, 0, contentBounds.Width, contentBounds.Height));
                }

                renderTarget.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            finally
            {
                // 5. CLEANUP: Trả lại mọi thứ như cũ
                if (panel != null) panel.Background = originalBackground;
                RestoreState(); // Khôi phục Zoom và Màu sắc

                renderTarget?.Clear();
                renderTarget = null;
                visual = null;
                GC.Collect();
            }
        });
    }

    // ==========================================================
    // TÌM KIẾM NỘI DUNG CHÍNH XÁC (INK HUNTING)
    // ==========================================================
    private Rect GetPreciseContentBounds(FrameworkElement rootElement)
    {
        Rect bounds = Rect.Empty;

        void WalkVisualTree(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement fe && fe.Visibility == Visibility.Visible && fe.Opacity > 0)
                {
                    // Chỉ quan tâm: Thumb (Node) và Path (Dây)
                    // Sử dụng System.Windows.Shapes.Path đầy đủ để tránh lỗi Ambiguous
                    bool isContent = fe is Thumb || (fe is System.Windows.Shapes.Path && fe.ActualWidth > 0 && fe.ActualHeight > 0);

                    // Lọc bỏ layer nền (thường size rất lớn > 15000)
                    if (isContent && fe.ActualWidth < 15000 && fe.ActualHeight < 15000)
                    {
                        try
                        {
                            // Lấy tọa độ tuyệt đối
                            GeneralTransform transform = fe.TransformToAncestor(rootElement);
                            Rect itemBounds = transform.TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));

                            if (bounds == Rect.Empty) bounds = itemBounds;
                            else bounds.Union(itemBounds);
                        }
                        catch { }
                    }
                    WalkVisualTree(fe);
                }
            }
        }

        WalkVisualTree(rootElement);

        if (bounds == Rect.Empty) return Rect.Empty;

        // Thêm Padding để ảnh thoáng, không bị sát mép
        // [FIXED] Đã khai báo biến ContentPadding ở đầu class
        bounds.Inflate(ContentPadding, ContentPadding);
        return bounds;
    }

    // ==========================================================
    // MÀU SẮC THÔNG MINH (BẢO VỆ NODE)
    // ==========================================================
    private void ApplySmartContrast(DependencyObject node)
    {
        if (node == null) return;

        // --- QUAN TRỌNG: NẾU GẶP NODE (THUMB) -> DỪNG LẠI ---
        // Không can thiệp vào bên trong Node để giữ nguyên chữ trắng/nền xanh
        if (node is Thumb)
        {
            return;
        }

        // 1. XỬ LÝ DÂY (PATH)
        // Chỉ xử lý các Path nằm ngoài Node (tức là Dây nối)
        if (node is Shape shape)
        {
            if (shape.Stroke != null)
            {
                // Chỉ đổi màu Dây nếu nó màu TRẮNG (hoặc gần trắng) -> Đổi sang Xám Đậm
                // Giữ nguyên các màu khác (Xanh, Đỏ, Vàng...) để giống App
                if (IsInvisibleOnWhite(shape.Stroke))
                {
                    ChangePropertySafe(shape, Shape.StrokeProperty, new SolidColorBrush(Color.FromRgb(80, 80, 80)));
                }
            }
        }
        // 2. XỬ LÝ TEXT BÊN NGOÀI (Nếu có)
        else if (node is TextBlock textBlock)
        {
            if (IsInvisibleOnWhite(textBlock.Foreground))
                ChangePropertySafe(textBlock, TextBlock.ForegroundProperty, Brushes.Black);
        }

        // Đệ quy
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            ApplySmartContrast(VisualTreeHelper.GetChild(node, i));
        }
    }

    private void ChangePropertySafe(DependencyObject target, DependencyProperty dp, Brush newValue)
    {
        var bindingExpr = BindingOperations.GetBindingExpression(target, dp);
        if (bindingExpr != null)
        {
            var parentBinding = bindingExpr.ParentBinding;
            target.SetValue(dp, newValue);
            _revertActions.Add(() => BindingOperations.SetBinding(target, dp, parentBinding));
        }
        else
        {
            var originalValue = target.GetValue(dp);
            target.SetValue(dp, newValue);
            _revertActions.Add(() => target.SetValue(dp, originalValue));
        }
    }

    private void RestoreState()
    {
        for (int i = _revertActions.Count - 1; i >= 0; i--)
        {
            try { _revertActions[i](); } catch { }
        }
        _revertActions.Clear();
    }

    private bool IsInvisibleOnWhite(Brush brush)
    {
        if (brush is SolidColorBrush scb)
        {
            // Độ sáng > 0.9 tức là gần như trắng tinh
            double lum = (0.299 * scb.Color.R + 0.587 * scb.Color.G + 0.114 * scb.Color.B) / 255.0;
            return lum > 0.90;
        }
        return false;
    }

    // ==========================================================
    // LƯU FILE (INTERNAL)
    // ==========================================================

    private async Task SaveAsImageInternal(FrameworkElement element, string filePath)
    {
        byte[]? bytes = await RenderElementToBytesAsync(element);
        if (bytes != null) await Task.Run(() => File.WriteAllBytes(filePath, bytes));
    }

    private async Task SaveAsPdfInternal(FrameworkElement element, string filePath, string? password, bool hasWatermark, string authorName)
    {
        byte[]? imageBytes = await RenderElementToBytesAsync(element);
        if (imageBytes == null) return;

        await Task.Run(() =>
        {
            using var document = new PdfDocument();
            if (!string.IsNullOrEmpty(password))
            {
                document.SecuritySettings.UserPassword = password;
                document.SecuritySettings.OwnerPassword = password;
                document.SecuritySettings.PermitFullQualityPrint = true;
                document.SecuritySettings.PermitModifyDocument = false;
            }

            var page = document.AddPage();
            // Mặc định A4 (595 x 842 points)
            page.Size = PdfSharpCore.PageSize.A4;

            using var ms = new MemoryStream(imageBytes);
            using var xImage = XImage.FromStream(() => ms);
            using var xGraphics = XGraphics.FromPdfPage(page);

            // --- THUẬT TOÁN SCALE FIT (AUTO-SIZE) ---

            double pageWidth = page.Width.Point;
            double pageHeight = page.Height.Point;

            // Vùng vẽ an toàn (trừ lề)
            double drawAreaW = pageWidth - (PageMargin * 2);
            double drawAreaH = pageHeight - (PageMargin * 2);

            // Tính tỉ lệ khung hình
            double imageRatio = (double)xImage.PixelWidth / xImage.PixelHeight;
            double pageRatio = drawAreaW / drawAreaH;

            double finalW, finalH;

            if (imageRatio > pageRatio)
            {
                // Hình bè ngang -> Fit theo chiều ngang
                finalW = drawAreaW;
                finalH = finalW / imageRatio;
            }
            else
            {
                // Hình cao dọc -> Fit theo chiều dọc
                finalH = drawAreaH;
                finalW = finalH * imageRatio;
            }

            // Tính tọa độ để căn giữa (Center Align)
            double x = PageMargin + (drawAreaW - finalW) / 2;
            double y = PageMargin + (drawAreaH - finalH) / 2;

            // Vẽ hình ảnh vào vị trí đã tính toán
            xGraphics.DrawImage(xImage, x, y, finalW, finalH);

            // --- WATERMARK NHỎ ---
            if (hasWatermark)
            {
                string watermarkText = $"MindMap App • {authorName}";
                var font = new XFont("Arial", 10, XFontStyle.Italic);
                var brush = new XSolidBrush(XColor.FromArgb(80, 100, 100, 100));

                var textSize = xGraphics.MeasureString(watermarkText, font);

                // Đặt ở góc dưới cùng bên phải
                double wx = pageWidth - textSize.Width - 20;
                double wy = pageHeight - textSize.Height - 20;

                xGraphics.DrawString(watermarkText, font, brush, wx, wy);
            }

            document.Save(filePath);
        });

        imageBytes = null;
        GC.Collect();
    }
}
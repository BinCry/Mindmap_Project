using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace MindmapApp.Helpers
{
    public class RichTextBoxHelper
    {
        // Property chính để Binding XAML
        public static readonly DependencyProperty DocumentXamlProperty =
            DependencyProperty.RegisterAttached(
                "DocumentXaml",
                typeof(string),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata(
                    "",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnDocumentXamlChanged)
            );

        // Property phụ (Private) để đánh dấu trạng thái đang cập nhật -> Tránh vòng lặp
        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(RichTextBoxHelper));

        public static string GetDocumentXaml(DependencyObject obj) => (string)obj.GetValue(DocumentXamlProperty);
        public static void SetDocumentXaml(DependencyObject obj, string value) => obj.SetValue(DocumentXamlProperty, value);

        private static bool GetIsUpdating(DependencyObject obj) => (bool)obj.GetValue(IsUpdatingProperty);
        private static void SetIsUpdating(DependencyObject obj, bool value) => obj.SetValue(IsUpdatingProperty, value);

        // 1. KHI VIEWMODEL THAY ĐỔI -> CẬP NHẬT VIEW
        private static void OnDocumentXamlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RichTextBox richTextBox) return;

            // Nếu thay đổi này do chính RichTextBox đang gõ tạo ra (Flag = true) -> Bỏ qua, không load lại
            if (GetIsUpdating(richTextBox)) return;

            string newXaml = e.NewValue as string ?? string.Empty;

            // Ngắt sự kiện để tránh trigger TextChanged khi đang load
            richTextBox.TextChanged -= RichTextBox_TextChanged;

            try
            {
                FlowDocument document;

                if (string.IsNullOrWhiteSpace(newXaml))
                {
                    // Không có nội dung -> tạo FlowDocument rỗng
                    document = new FlowDocument();
                }
                else
                {
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(newXaml));
                    // Đọc trực tiếp FlowDocument từ XAML
                    document = (FlowDocument)XamlReader.Load(stream);
                }

                richTextBox.Document = document;
            }
            catch
            {
                // Nếu có lỗi format XAML -> fallback sang document rỗng,
                // tránh làm app crash nhưng node vẫn update lần gõ tiếp theo.
                richTextBox.Document = new FlowDocument();
            }
            finally
            {
                // Đăng ký lại sự kiện sau khi load xong
                // CHỈ ĐĂNG KÝ NẾU LÀ EDITOR (Không phải ReadOnly)
                if (!richTextBox.IsReadOnly)
                {
                    richTextBox.TextChanged += RichTextBox_TextChanged;
                }
            }
        }

        // 2. KHI NGƯỜI DÙNG GÕ (VIEW) -> CẬP NHẬT VIEWMODEL
        private static void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var richTextBox = sender as RichTextBox;
            if (richTextBox == null || richTextBox.IsReadOnly) return;

            // Bật cờ: "Tôi đang cập nhật, đừng load lại cái tôi vừa gửi"
            SetIsUpdating(richTextBox, true);
            try
            {
                string xaml = XamlWriter.Save(richTextBox.Document);
                SetDocumentXaml(richTextBox, xaml);
            }
            finally
            {
                SetIsUpdating(richTextBox, false);
            }
        }
    }
}
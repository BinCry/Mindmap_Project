using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MindmapApp.Views
{
    public partial class TextEditorSidebar : UserControl
    {
        // Cờ hiệu để ngăn chặn vòng lặp cập nhật giao diện
        private bool _isUpdatingVisualState = false;

        public TextEditorSidebar()
        {
            InitializeComponent();
        }

        #region Helper Methods (Hàm hỗ trợ)
        // Hàm áp dụng thuộc tính cho văn bản đang chọn
        private void ApplyToSelection(DependencyProperty formattingProperty, object value)
        {
            if (EditorBox == null || EditorBox.Selection == null) return;
            EditorBox.Selection.ApplyPropertyValue(formattingProperty, value);
            EditorBox.Focus(); // Focus lại để người dùng gõ tiếp được ngay
        }
        #endregion

        #region ComboBox Handlers (Xử lý chọn Font, Size)
        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingVisualState) return;
            if (FontFamilyComboBox.SelectedItem is string familyName && !string.IsNullOrWhiteSpace(familyName))
            {
                ApplyToSelection(TextElement.FontFamilyProperty, new FontFamily(familyName));
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingVisualState) return;
            if (FontSizeComboBox.SelectedItem is double size && size > 0)
            {
                ApplyToSelection(TextElement.FontSizeProperty, size);
            }
        }

        private void FontStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingVisualState) return;
            if (FontStyleComboBox.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString();

                // Reset về Normal trước để tránh chồng chéo style
                ApplyToSelection(TextElement.FontWeightProperty, FontWeights.Normal);
                ApplyToSelection(TextElement.FontStyleProperty, FontStyles.Normal);

                if (tag != null)
                {
                    if (tag.Contains("Bold")) ApplyToSelection(TextElement.FontWeightProperty, FontWeights.Bold);
                    if (tag.Contains("Italic")) ApplyToSelection(TextElement.FontStyleProperty, FontStyles.Italic);
                }
            }
        }
        #endregion

        #region Button Click Handlers (SỬA LỖI THIẾU HÀM TẠI ĐÂY)

        // --- BOLD, ITALIC, UNDERLINE ---
        private void BtnBold_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra trạng thái nút sau khi click để áp dụng
            bool isBold = BtnBold.IsChecked == true;
            ApplyToSelection(TextElement.FontWeightProperty, isBold ? FontWeights.Bold : FontWeights.Normal);
            UpdateVisualState();
        }

        private void BtnItalic_Click(object sender, RoutedEventArgs e)
        {
            bool isItalic = BtnItalic.IsChecked == true;
            ApplyToSelection(TextElement.FontStyleProperty, isItalic ? FontStyles.Italic : FontStyles.Normal);
            UpdateVisualState();
        }

        private void BtnUnderline_Click(object sender, RoutedEventArgs e)
        {
            bool isUnderline = BtnUnderline.IsChecked == true;
            if (isUnderline)
            {
                TextDecorationCollection tdc = new TextDecorationCollection();
                tdc.Add(TextDecorations.Underline);
                ApplyToSelection(Inline.TextDecorationsProperty, tdc);
            }
            else
            {
                ApplyToSelection(Inline.TextDecorationsProperty, null);
            }
            UpdateVisualState();
        }

        // --- ALIGNMENT (CĂN LỀ - CHỈ CHỌN 1) ---
        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyAlignment(TextAlignment.Left);
        }

        private void BtnAlignCenter_Click(object sender, RoutedEventArgs e)
        {
            ApplyAlignment(TextAlignment.Center);
        }

        private void BtnAlignRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyAlignment(TextAlignment.Right);
        }

        private void ApplyAlignment(TextAlignment alignment)
        {
            ApplyToSelection(Block.TextAlignmentProperty, alignment);
            UpdateVisualState(); // Cập nhật lại để tắt các nút căn lề khác
        }

        // --- LISTS (DANH SÁCH) ---
        private void BtnBullets_Click(object sender, RoutedEventArgs e)
        {
            if (EditorBox != null)
            {
                EditingCommands.ToggleBullets.Execute(null, EditorBox);
                UpdateVisualState();
            }
        }

        private void BtnNumbering_Click(object sender, RoutedEventArgs e)
        {
            if (EditorBox != null)
            {
                EditingCommands.ToggleNumbering.Execute(null, EditorBox);
                UpdateVisualState();
            }
        }
        #endregion

        #region Sync Logic (ĐỒNG BỘ TRẠNG THÁI TỪ VĂN BẢN LÊN NÚT)

        // Sự kiện này chạy mỗi khi con trỏ di chuyển hoặc chọn văn bản
        private void EditorBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();
        }

        // Hàm này kiểm tra văn bản và ép nút hiển thị đúng trạng thái
        private void UpdateVisualState()
        {
            if (EditorBox == null || EditorBox.Selection == null) return;

            _isUpdatingVisualState = true; // Khóa sự kiện thay đổi để tránh vòng lặp

            try
            {
                // 1. Cập nhật BOLD / ITALIC / UNDERLINE
                var fontWeight = EditorBox.Selection.GetPropertyValue(TextElement.FontWeightProperty);
                var fontStyle = EditorBox.Selection.GetPropertyValue(TextElement.FontStyleProperty);
                var textDecorations = EditorBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty);

                BtnBold.IsChecked = (fontWeight != DependencyProperty.UnsetValue) && (fontWeight.Equals(FontWeights.Bold));
                BtnItalic.IsChecked = (fontStyle != DependencyProperty.UnsetValue) && (fontStyle.Equals(FontStyles.Italic));

                BtnUnderline.IsChecked = false;
                if (textDecorations != DependencyProperty.UnsetValue && textDecorations is TextDecorationCollection decorations)
                {
                    BtnUnderline.IsChecked = decorations.Any(d => d.Location == TextDecorationLocation.Underline);
                }

                // 2. Cập nhật ALIGNMENT (Đảm bảo chỉ 1 nút sáng)
                var textAlignment = EditorBox.Selection.GetPropertyValue(Block.TextAlignmentProperty);

                // Tắt hết trước
                BtnAlignLeft.IsChecked = false;
                BtnAlignCenter.IsChecked = false;
                BtnAlignRight.IsChecked = false;

                if (textAlignment != DependencyProperty.UnsetValue && textAlignment is TextAlignment align)
                {
                    if (align == TextAlignment.Center) BtnAlignCenter.IsChecked = true;
                    else if (align == TextAlignment.Right) BtnAlignRight.IsChecked = true;
                    else BtnAlignLeft.IsChecked = true; // Left hoặc Justify mặc định là Left
                }
                else
                {
                    BtnAlignLeft.IsChecked = true; // Mặc định
                }

                // 3. Cập nhật LISTS
                BtnBullets.IsChecked = false;
                BtnNumbering.IsChecked = false;

                TextPointer start = EditorBox.Selection.Start;
                Paragraph paragraph = start.Paragraph;
                if (paragraph != null && paragraph.Parent is ListItem listItem && listItem.Parent is List list)
                {
                    if (list.MarkerStyle == TextMarkerStyle.Decimal)
                    {
                        BtnNumbering.IsChecked = true;
                    }
                    else if (list.MarkerStyle == TextMarkerStyle.Disc || list.MarkerStyle == TextMarkerStyle.Circle || list.MarkerStyle == TextMarkerStyle.Square)
                    {
                        BtnBullets.IsChecked = true;
                    }
                }

                // 4. Cập nhật ComboBox Size (nếu cần)
                var fontSize = EditorBox.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                if (fontSize != DependencyProperty.UnsetValue && fontSize is double size)
                {
                    FontSizeComboBox.SelectedValue = size;
                }
            }
            finally
            {
                _isUpdatingVisualState = false; // Mở khóa
            }
        }
        #endregion

        // Hàm chèn ảnh
        private void InsertImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorBox == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh chèn vào node",
                Filter = "Ảnh|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tất cả|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new System.Uri(dlg.FileName, System.UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                var image = new Image
                {
                    Source = bitmap,
                    Width = 150,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4)
                };

                var menu = new ContextMenu();
                void AddSizeItem(string header, double width)
                {
                    var m = new MenuItem { Header = header, Tag = width };
                    m.Click += (_, _) => image.Width = (double)m.Tag;
                    menu.Items.Add(m);
                }

                AddSizeItem("Nhỏ (100px)", 100);
                AddSizeItem("Vừa (150px)", 150);
                AddSizeItem("Lớn (250px)", 250);
                image.ContextMenu = menu;

                var container = new InlineUIContainer(image, EditorBox.CaretPosition);
                EditorBox.CaretPosition = container.ElementEnd;
                EditorBox.Focus();
            }
            catch
            {
                MessageBox.Show("Không thể chèn ảnh này.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
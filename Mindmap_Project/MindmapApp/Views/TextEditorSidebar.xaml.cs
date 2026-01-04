using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Win32;
using MindmapApp.ViewModels;

namespace MindmapApp.Views
{
    public partial class TextEditorSidebar : UserControl
    {
        private bool _isUpdatingVisualState = false;

        public TextEditorSidebar()
        {
            InitializeComponent();
            
            // Enable filtering (suggestions) logic
            this.Loaded += TextEditorSidebar_Loaded;
        }

        private void TextEditorSidebar_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to TextChanged event for filtering
            FontFamilyComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, 
                new TextChangedEventHandler(OnFontFamilyTextChanged));
            FontSizeComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, 
                new TextChangedEventHandler(OnFontSizeTextChanged));

            // Subscribe to DropDownClosed to reset filtering
            FontFamilyComboBox.DropDownClosed += (s, args) => ResetFilter(FontFamilyComboBox);
            FontSizeComboBox.DropDownClosed += (s, args) => ResetFilter(FontSizeComboBox);
        }

        private void OnFontFamilyTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingVisualState) return;
            FilterComboBox(FontFamilyComboBox);
        }

        private void OnFontSizeTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingVisualState) return;
            FilterComboBox(FontSizeComboBox);
        }

        private void FilterComboBox(ComboBox comboBox)
        {
            // Fix Bug: Only filter/open if user is actually typing (has focus)
            if (!comboBox.IsKeyboardFocusWithin) return;

            if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox textBox)
            {
                string searchText = textBox.Text;
                
                // Get the default view of the ItemsSource
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(comboBox.ItemsSource);
                if (view == null) return;

                if (string.IsNullOrEmpty(searchText))
                {
                    view.Filter = null;
                    comboBox.IsDropDownOpen = true; // Open dropdown to show all options when cleared
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item == null) return false;
                        return item.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
                    };
                    comboBox.IsDropDownOpen = true;
                }
            }
        }

        private void ResetFilter(ComboBox comboBox)
        {
             var view = System.Windows.Data.CollectionViewSource.GetDefaultView(comboBox.ItemsSource);
             if (view != null) view.Filter = null;
        }

        #region Helper Methods
        private void ApplyToSelection(DependencyProperty formattingProperty, object value, bool focusEditor = true)
        {
            if (DataContext is MainViewModel vm) vm.RecordHistory();
            if (EditorBox == null || EditorBox.Selection == null) return;
            EditorBox.Selection.ApplyPropertyValue(formattingProperty, value);
            if (focusEditor)
            {
                EditorBox.Focus();
            }
        }
        #endregion

        // ĐÃ XÓA: ShapeComboBox_SelectionChanged

        #region ComboBox Handlers (Font, Size)
        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingVisualState) return;
            if (FontFamilyComboBox.SelectedItem is string familyName && !string.IsNullOrWhiteSpace(familyName))
            {
                // Pass false to prevent stealing focus from ComboBox while typing
                ApplyToSelection(TextElement.FontFamilyProperty, new FontFamily(familyName), false);
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingVisualState) return;
            if (FontSizeComboBox.SelectedItem is double size && size > 0)
            {
                // Pass false to prevent stealing focus from ComboBox while typing
                ApplyToSelection(TextElement.FontSizeProperty, size, false);
            }
        }
        #endregion

        #region Button Click Handlers
        private void BtnBold_Click(object sender, RoutedEventArgs e)
        {
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
            UpdateVisualState();
        }

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

        #region Sync Logic
        private void EditorBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (EditorBox == null || EditorBox.Selection == null) return;

            _isUpdatingVisualState = true;

            try
            {
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

                var textAlignment = EditorBox.Selection.GetPropertyValue(Block.TextAlignmentProperty);
                BtnAlignLeft.IsChecked = false;
                BtnAlignCenter.IsChecked = false;
                BtnAlignRight.IsChecked = false;

                if (textAlignment != DependencyProperty.UnsetValue && textAlignment is TextAlignment align)
                {
                    if (align == TextAlignment.Center) BtnAlignCenter.IsChecked = true;
                    else if (align == TextAlignment.Right) BtnAlignRight.IsChecked = true;
                    else BtnAlignLeft.IsChecked = true;
                }
                else
                {
                    BtnAlignLeft.IsChecked = true;
                }

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

                var fontSize = EditorBox.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                if (fontSize != DependencyProperty.UnsetValue && fontSize is double size)
                {
                    FontSizeComboBox.SelectedValue = size;
                }
            }
            finally
            {
                _isUpdatingVisualState = false;
            }
        }
        private void EditorBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
             if (DataContext is MainViewModel vm)
             {
                 vm.RecordHistory();
                 if (vm.SelectedNode != null) vm.RequestNodeLock(vm.SelectedNode);
             }
        }

        private void EditorBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.SelectedNode != null)
            {
                vm.RequestNodeUnlock(vm.SelectedNode);
            }
        }
        #endregion

        private void InsertImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.RecordHistory();
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

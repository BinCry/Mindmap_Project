using CommunityToolkit.Mvvm.ComponentModel; // Đảm bảo có thư viện này nếu dùng ObservableObject
using MindmapApp.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MindmapApp.Commands; 

namespace MindmapApp.ViewModels
{
    public class NodeViewModel : BaseViewModel
    {
        #region Relations
        private NodeViewModel? _parent;
        public ObservableCollection<NodeViewModel> Children { get; } = new();

        public NodeViewModel? Parent
        {
            get => _parent;
            set
            {
                if (_parent != value)
                {
                    if (_parent != null) _parent.Children.Remove(this);
                    SetProperty(ref _parent, value);

                    // Logic thêm con vào cha:
                    if (_parent != null && !_parent.Children.Contains(this))
                    {
                        _parent.Children.Add(this);
                    }

                    // Cập nhật giao diện nút + cho cha
                    if (_parent != null) _parent.NotifyChildrenChanged();
                    OnPropertyChanged(nameof(HasChildren));
                }
            }
        }
        #endregion

        private bool _isExpanded = true;
        private bool _isVisible = true;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    UpdateChildrenVisibility(); // Khi cha đóng/mở -> cập nhật con
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        // Property kiểm tra xem node có con không (để hiện nút +/-)
        public bool HasChildren => Children.Count > 0;

        // Command khi nhấn nút +/-
        public ICommand ToggleExpandCommand { get; }

        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }

        // Hàm đệ quy: Cập nhật trạng thái hiển thị của các con cháu
        public void UpdateChildrenVisibility()
        {
            foreach (var child in Children)
            {
                // Con chỉ hiện khi: Cha đang mở VÀ Cha đang hiện
                child.IsVisible = this.IsExpanded && this.IsVisible;

                // Tiếp tục cập nhật cho cháu chắt
                child.UpdateChildrenVisibility();
            }
        }

        // Hàm reset về trạng thái ban đầu khi BẮT ĐẦU trình chiếu
        public void ResetForPresentation(bool isRoot)
        {
            IsVisible = isRoot; // Chỉ hiện nếu là Root
            IsExpanded = false; // Đóng tất cả lại ban đầu
            // Không gọi UpdateChildrenVisibility ở đây để tránh đệ quy thừa, 
            // MainViewModel sẽ lo việc này.
        }

        // Hàm khôi phục lại trạng thái bình thường (khi THOÁT trình chiếu)
        public void ResetToNormal()
        {
            IsVisible = true;
            IsExpanded = true;
        }

        // Khi thêm con mới, phải cập nhật lại HasChildren để hiện nút +
        public void NotifyChildrenChanged()
        {
            OnPropertyChanged(nameof(HasChildren));
        }


        #region Fields & Constructor

        private readonly NodeModel _model;
        private bool _isSelected;

        public NodeViewModel(NodeModel model)
        {
            _model = model;

            ToggleExpandCommand = new RelayCommand(obj => ToggleExpand());
        }

        public NodeModel ToModel() => _model;

        public bool IsDraggable
        {
            get => _model.IsDraggable;
            set
            {
                if (_model.IsDraggable != value)
                {
                    _model.IsDraggable = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDeletable
        {
            get => _model.IsDeletable;
            set
            {
                if (_model.IsDeletable != value)
                {
                    _model.IsDeletable = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region Geometry & Content
        public Guid Id => _model.Id;

        public string ContentXaml
        {
            get => string.IsNullOrEmpty(_model.ContentXaml) ? CreateDefaultXaml(_model.Title) : _model.ContentXaml;
            set
            {
                if (_model.ContentXaml != value)
                {
                    _model.ContentXaml = value;
                    OnPropertyChanged();
                }
            }
        }
        private string CreateDefaultXaml(string text) => $"<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" PagePadding=\"0\"><Paragraph TextAlignment=\"Center\"><Run>{text}</Run></Paragraph></FlowDocument>";

        public double X
        {
            get => _model.X;
            set { if (Math.Abs(_model.X - value) > 0.1) { _model.X = value; OnPropertyChanged(); } }
        }
        public double Y
        {
            get => _model.Y;
            set { if (Math.Abs(_model.Y - value) > 0.1) { _model.Y = value; OnPropertyChanged(); } }
        }
        public double Width
        {
            get => _model.Width;
            set { if (value < 50) value = 50; if (Math.Abs(_model.Width - value) > 0.1) { _model.Width = value; OnPropertyChanged(); } }
        }
        public double Height
        {
            get => _model.Height;
            set { if (value < 40) value = 40; if (Math.Abs(_model.Height - value) > 0.1) { _model.Height = value; OnPropertyChanged(); } }
        }

        public string Title
        {
            get => _model.Title;
            set { if (_model.Title != value) { _model.Title = value; OnPropertyChanged(); } }
        }
        #endregion

        #region Appearance & Formatting

        public string Shape
        {
            get => _model.Shape;
            set { if (_model.Shape != value) { _model.Shape = value; OnPropertyChanged(); } }
        }
        public Color BackgroundColor
        {
            get => _model.BackgroundColor;
            set { if (_model.BackgroundColor != value) { _model.BackgroundColor = value; OnPropertyChanged(); } }
        }
        public Color BorderColor
        {
            get => _model.BorderColor;
            set { if (_model.BorderColor != value) { _model.BorderColor = value; OnPropertyChanged(); } }
        }
        public Color TextColor
        {
            get => _model.TextColor;
            set { if (_model.TextColor != value) { _model.TextColor = value; OnPropertyChanged(); } }
        }
        public string BackgroundGridStyle
        {
            get => _model.BackgroundGridStyle;
            set { if (_model.BackgroundGridStyle != value) { _model.BackgroundGridStyle = value; OnPropertyChanged(); } }
        }
        public double FontSize
        {
            get => _model.FontSize;
            set { if (Math.Abs(_model.FontSize - value) > 0.1) { _model.FontSize = value; OnPropertyChanged(); } }
        }
        public string FontFamily
        {
            get => _model.FontFamily;
            set { if (_model.FontFamily != value) { _model.FontFamily = value; OnPropertyChanged(); } }
        }

        // --- CÁC THUỘC TÍNH BỊ THIẾU GÂY LỖI CS1061 ---
        public bool IsBold
        {
            get => _model.IsBold;
            set
            {
                if (_model.IsBold != value)
                {
                    _model.IsBold = value;
                    _model.FontWeight = value ? "Bold" : "Normal"; // Đồng bộ
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FontWeight)); // Notify View
                }
            }
        }
        public bool IsItalic
        {
            get => _model.IsItalic;
            set
            {
                if (_model.IsItalic != value)
                {
                    _model.IsItalic = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FontStyleValue));
                }
            }
        }
        public bool IsUnderline
        {
            get => _model.IsUnderline;
            set
            {
                if (_model.IsUnderline != value)
                {
                    _model.IsUnderline = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TextDecorationsValue));
                }
            }
        }
        public bool IsStrikethrough
        {
            get => _model.IsStrikethrough;
            set
            {
                if (_model.IsStrikethrough != value)
                {
                    _model.IsStrikethrough = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TextDecorationsValue));
                }
            }
        }

        // Helpers cho View (XAML Binding)
        public string FontWeight => _model.FontWeight;
        public FontStyle FontStyleValue => IsItalic ? FontStyles.Italic : FontStyles.Normal;
        public TextDecorationCollection TextDecorationsValue
        {
            get
            {
                var collection = new TextDecorationCollection();
                if (IsUnderline) collection.Add(TextDecorations.Underline);
                if (IsStrikethrough) collection.Add(TextDecorations.Strikethrough);
                return collection;
            }
        }
        #endregion

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
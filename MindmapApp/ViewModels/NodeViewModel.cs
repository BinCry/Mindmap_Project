using System;
using System.Windows.Media;
using MindmapApp.Models;
using System.Collections.ObjectModel; 

namespace MindmapApp.ViewModels;

public class NodeViewModel : BaseViewModel
{

    #region Relations
    private NodeViewModel? _parent;
    // Danh sách các node con
    public ObservableCollection<NodeViewModel> Children { get; } = new();

    public NodeViewModel? Parent
    {
        get => _parent;
        set
        {
            if (_parent != value)
            {
                // 1. Rời khỏi "gia đình" cũ (nếu có)
                if (_parent != null)
                {
                    _parent.Children.Remove(this);
                }

                // 2. Gán cha mới
                SetProperty(ref _parent, value);

                // 3. Gia nhập "gia đình" mới
                if (_parent != null && !_parent.Children.Contains(this))
                {
                    _parent.Children.Add(this);
                }
            }
        }
    }
        #endregion
    #region Fields & Constructor

    private readonly NodeModel _model;
    private bool _isSelected;

    public NodeViewModel(NodeModel model)
    {
        _model = model;
    }
    // Khả năng kéo thả: 
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
    // Khả năng Xóa: 
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

    public NodeModel ToModel() => _model;

    #endregion

    #region Identity

    // Id chỉ đọc, định danh duy nhất của Node
    public Guid Id => _model.Id;

    #endregion

    #region Geometry (Position & Size) 

    // Tính toán tâm Node (Computed Properties) cho dây kết nối
    public double CenterX => X + (Width / 2);
    public double CenterY => Y + (Height / 2);

    public double X
    {
        get => _model.X;
        set
        {
            if (Math.Abs(_model.X - value) > 0.1)
            {
                _model.X = value;
                OnPropertyChanged();

               // Kích hoạt dây vẽ lại: 
                OnPropertyChanged(nameof(CenterX));
            }
        }
    }

    public double Y
    {
        get => _model.Y;
        set
        {
            if (Math.Abs(_model.Y - value) > 0.1)
            {
                _model.Y = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenterY)); // Báo tâm Y thay đổi
            }
        }
    }

    public double Width
    {
        get => _model.Width;
        set
        {
            if (Math.Abs(_model.Width - value) > 0.1)
            {
                _model.Width = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenterX)); // Rộng đổi -> Tâm lệch
            }
        }
    }

    public double Height
    {
        get => _model.Height;
        set
        {
            if (Math.Abs(_model.Height - value) > 0.1)
            {
                _model.Height = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenterY)); // Cao đổi -> Tâm lệch
            }
        }
    }

    #endregion

    #region Content Data (Text)

    public string Title
    {
        get => _model.Title;
        set
        {
            if (_model.Title != value)
            {
                _model.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Description
    {
        get => _model.Description;
        set
        {
            if (_model.Description != value)
            {
                _model.Description = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Appearance (Visual Styles)

    public string Shape
    {
        get => _model.Shape;
        set
        {
            if (_model.Shape != value)
            {
                _model.Shape = value;
                OnPropertyChanged();
            }
        }
    }

    public Color BackgroundColor
    {
        get => _model.BackgroundColor;
        set
        {
            if (_model.BackgroundColor != value)
            {
                _model.BackgroundColor = value;
                OnPropertyChanged();
            }
        }
    }

    public Color BorderColor
    {
        get => _model.BorderColor;
        set
        {
            if (_model.BorderColor != value)
            {
                _model.BorderColor = value;
                OnPropertyChanged();
            }
        }
    }

    public Color TextColor
    {
        get => _model.TextColor;
        set
        {
            if (_model.TextColor != value)
            {
                _model.TextColor = value;
                OnPropertyChanged();
            }
        }
    }

    public double FontSize
    {
        get => _model.FontSize;
        set
        {
            if (Math.Abs(_model.FontSize - value) > 0.1)
            {
                _model.FontSize = value;
                OnPropertyChanged();
            }
        }
    }

    public string FontFamily
    {
        get => _model.FontFamily;
        set
        {
            if (_model.FontFamily != value)
            {
                _model.FontFamily = value;
                OnPropertyChanged();
            }
        }
    }
    public string FontWeight
    {
        get => _model.FontWeight;
        set
        {
            if (_model.FontWeight != value)
            {
                _model.FontWeight = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region View State (Selection)

    // Property này không nằm trong Model, chỉ dùng cho View tạm thời
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    #endregion

}

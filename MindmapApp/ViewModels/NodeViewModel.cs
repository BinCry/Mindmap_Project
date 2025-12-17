using System;
using System.Windows.Media;
using MindmapApp.Models;
using System.Collections.ObjectModel;

namespace MindmapApp.ViewModels;

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

    public NodeModel ToModel() => _model;

    #endregion

    #region Identity
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
                OnPropertyChanged(nameof(CenterX)); // Cập nhật tâm dây nối
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
                OnPropertyChanged(nameof(CenterY)); // Cập nhật tâm dây nối
            }
        }
    }

    public double Width
    {
        get => _model.Width;
        set
        {
            // LOGIC HỢP NHẤT: Kiểm tra an toàn trước khi gán vào Model
            if (value < 50) value = 50;

            if (Math.Abs(_model.Width - value) > 0.1)
            {
                _model.Width = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenterX)); // Rộng đổi -> Tâm X lệch -> Cập nhật dây
            }
        }
    }

    public double Height
    {
        get => _model.Height;
        set
        {
            // LOGIC HỢP NHẤT: Kiểm tra an toàn trước khi gán vào Model
            if (value < 40) value = 40;

            if (Math.Abs(_model.Height - value) > 0.1)
            {
                _model.Height = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenterY)); // Cao đổi -> Tâm Y lệch -> Cập nhật dây
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

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    #endregion
}
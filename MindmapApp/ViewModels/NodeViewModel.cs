using System;
using System.Windows.Media;
using MindmapApp.Models;

namespace MindmapApp.ViewModels;

public class NodeViewModel : BaseViewModel
{
    private readonly NodeModel _model;
    private bool _isSelected;

    public NodeViewModel(NodeModel model)
    {
        _model = model;
    }

    public Guid Id => _model.Id;

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

    public double X
    {
        get => _model.X;
        set
        {
            if (Math.Abs(_model.X - value) > 0.1)
            {
                _model.X = value;
                OnPropertyChanged();
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
            }
        }
    }

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

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public NodeModel ToModel() => _model;
}

using System;
using System.Windows.Media;
using MindmapApp.Models;

namespace MindmapApp.ViewModels;

public class ConnectionViewModel : BaseViewModel
{
    #region Fields & Constructor

    private readonly ConnectionModel _model; 
    private bool _isHighlighted;

    public ConnectionViewModel(ConnectionModel model)
    {
        _model = model;
    }

    public ConnectionModel ToModel() => _model;

    #endregion

    #region Identity & Relations

    public Guid Id => _model.Id;

    public Guid SourceId
    {
        get => _model.SourceId;
        set
        {
            if (_model.SourceId != value)
            {
                _model.SourceId = value;
                OnPropertyChanged();
            }
        }
    }

    public Guid TargetId
    {
        get => _model.TargetId;
        set
        {
            if (_model.TargetId != value)
            {
                _model.TargetId = value;
                OnPropertyChanged();
            }
        }
    }

    private NodeViewModel? _source;
    private NodeViewModel? _target;

    public NodeViewModel? Source
    {
        get => _source;
        set => SetProperty(ref _source, value);
    }

    public NodeViewModel? Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    #endregion

    #region Appearance (Visual Styles)

    public Color StrokeColor // Màu dây. 
    {
        get => _model.StrokeColor;
        set
        {
            if (_model.StrokeColor != value)
            {
                _model.StrokeColor = value;
                OnPropertyChanged();
            }
        }
    }

    public double Thickness // Độ dày. 
    {
        get => _model.Thickness;
        set
        {
            if (Math.Abs(_model.Thickness - value) > 0.1)
            {
                _model.Thickness = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCurved // Dây cong hay thẳng. 
    {
        get => _model.IsCurved;
        set
        {
            if (_model.IsCurved != value)
            {
                _model.IsCurved = value;
                OnPropertyChanged();
            }
        }
    }

    public DoubleCollection? DashArray // Nét liền hay đứt. 
    {
        get => _model.DashArray;
        set
        {
            if (_model.DashArray != value)
            {
                _model.DashArray = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region View State

    // Biến này dùng để đổi màu dây khi được chọn
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    #endregion
}

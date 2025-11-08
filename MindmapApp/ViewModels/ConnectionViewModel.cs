using System;
using System.Windows.Media;
using MindmapApp.Models;

namespace MindmapApp.ViewModels;

public class ConnectionViewModel : BaseViewModel
{
    private readonly ConnectionModel _model;
    private bool _isHighlighted;

    public ConnectionViewModel(ConnectionModel model)
    {
        _model = model;
    }

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

    public Color StrokeColor
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

    public double Thickness
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

    public bool IsCurved
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

    public DoubleCollection? DashArray
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

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    public ConnectionModel ToModel() => _model;
}

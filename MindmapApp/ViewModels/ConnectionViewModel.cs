using CommunityToolkit.Mvvm.ComponentModel;
using MindmapApp.Models;
using System;
using System.Windows.Media;

namespace MindmapApp.ViewModels
{
    public partial class ConnectionViewModel : ObservableObject
    {
        private readonly ConnectionModel _model;

        // Khai báo biến private để Toolkit tự sinh Property public
        [ObservableProperty] private NodeViewModel _source;
        [ObservableProperty] private NodeViewModel _target;
        [ObservableProperty] private Color _strokeColor;
        [ObservableProperty] private double _thickness;
        [ObservableProperty] private DoubleCollection? _dashArray;
        [ObservableProperty] private string _lineStyle;
        [ObservableProperty] private string _arrowStyle;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isHighlighted;

        public ConnectionViewModel(NodeViewModel source, NodeViewModel target)
        {
            _model = new ConnectionModel
            {
                SourceId = source.Id,
                TargetId = target.Id,
                StrokeColor = Colors.Gray,
                Thickness = 2,
                ArrowStyle = "Arrow"
            };

            _source = source;
            _target = target;
            _strokeColor = Colors.Gray;
            _thickness = 2;
            _lineStyle = "Solid";
            _arrowStyle = "Arrow";
        }

        public ConnectionViewModel(ConnectionModel model)
        {
            _model = model;
            _strokeColor = model.StrokeColor;
            _thickness = model.Thickness;
            _dashArray = model.DashArray;
            _lineStyle = model.DashArray != null ? "Dashed" : "Solid";
            _arrowStyle = string.IsNullOrEmpty(model.ArrowStyle) ? "Arrow" : model.ArrowStyle;
        }

        partial void OnStrokeColorChanged(Color value) => _model.StrokeColor = value;
        partial void OnThicknessChanged(double value) => _model.Thickness = value;
        partial void OnDashArrayChanged(DoubleCollection? value) => _model.DashArray = value;
        partial void OnArrowStyleChanged(string value) => _model.ArrowStyle = value;

        partial void OnLineStyleChanged(string value)
        {
            if (value == "Dashed") DashArray = new DoubleCollection { 4, 2 };
            else DashArray = null;
        }

        public Guid Id => _model.Id;
        public Guid SourceId => _model.SourceId;
        public Guid TargetId => _model.TargetId;

        public ConnectionModel ToModel()
        {
            _model.SourceId = Source?.Id ?? Guid.Empty;
            _model.TargetId = Target?.Id ?? Guid.Empty;
            _model.StrokeColor = StrokeColor;
            _model.Thickness = Thickness;
            _model.DashArray = DashArray;
            _model.ArrowStyle = ArrowStyle;
            return _model;
        }
    }
}
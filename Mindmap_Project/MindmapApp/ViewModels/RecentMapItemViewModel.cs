using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MindmapApp.Models;

namespace MindmapApp.ViewModels
{
    public partial class RecentMapItemViewModel : ObservableObject
    {
        public RecentMapItemViewModel(MindmapDocument document, ImageSource? thumbnail, bool isPlaceholder)
        {
            Document = document;
            Thumbnail = thumbnail;
            IsPlaceholder = isPlaceholder;
        }

        public MindmapDocument Document { get; }

        public Guid Id => Document.Id;
        public string Title => Document.Title;
        public DateTime UpdatedAt => Document.UpdatedAt;
        public Color CanvasBackgroundColor => Document.CanvasBackgroundColor;

        [ObservableProperty]
        private ImageSource? _thumbnail;

        [ObservableProperty]
        private bool _isPlaceholder;
    }
}

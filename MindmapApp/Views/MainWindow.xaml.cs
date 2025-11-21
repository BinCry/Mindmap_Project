using Microsoft.Win32;
using MindmapApp.Models;
using MindmapApp.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace MindmapApp.Views;

public partial class MainWindow : Window
{
    private bool isSidebarVisible = true;
    private readonly MainViewModel _viewModel;

    public MainWindow(UserAccount account)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(App.ExportService, App.SearchService, App.AiService, App.MindmapStorageService, account);
        DataContext = _viewModel;
        _viewModel.RequestExportImage += async (_, _) => await ExportAsImageAsync();
        _viewModel.RequestExportPdf += async (_, _) => await ExportAsPdfAsync();
        Placeholder.Visibility = string.IsNullOrEmpty(_viewModel.SearchText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        Storyboard storyboard = isSidebarVisible
            ? (Storyboard)FindResource("HideSidebar")
            : (Storyboard)FindResource("ShowSidebar");

        storyboard.Begin(this);
        isSidebarVisible = !isSidebarVisible;

        ToggleButton.Content = isSidebarVisible ? "◀" : "▶";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (Placeholder != null && SearchBox != null)
        {
            Placeholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            await _viewModel.FlushAutoSaveAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExportAsImageAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Ảnh PNG (*.png)|*.png",
            FileName = "mindmap.png"
        };

        //if (dialog.ShowDialog(this) == true)
        //{
        //    await _viewModel.ExportToImageAsync(MindmapSurface, dialog.FileName);
        //    MessageBox.Show(this, "Đã lưu ảnh mindmap thành công", "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);
        //}
    }

    private async Task ExportAsPdfAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Tài liệu PDF (*.pdf)|*.pdf",
            FileName = "mindmap.pdf"
        };

        //if (dialog.ShowDialog(this) == true)
        //{
        //    await _viewModel.ExportToPdfAsync(MindmapSurface, dialog.FileName);
        //    MessageBox.Show(this, "Đã lưu PDF mindmap thành công", "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);
        //}
    }

    private void NodeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb thumb || thumb.Tag is not NodeViewModel node)
        {
            return;
        }

        var scale = _viewModel.ZoomLevel;
        node.X += e.HorizontalChange / scale;
        node.Y += e.VerticalChange / scale;
    }

    private void NodeThumb_OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Thumb thumb && thumb.Tag is NodeViewModel node)
        {
            _viewModel.SelectedNode = node;
            _viewModel.CompleteConnectionCommand.Execute(node);
        }
    }

    private void StartConnectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNode != null)
        {
            _viewModel.StartConnectionCommand.Execute(_viewModel.SelectedNode);
        }
    }

    private void DeleteNodeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.CommandParameter is NodeViewModel node)
        {
            _viewModel.SelectedNode = node;
            if (_viewModel.DeleteNodeCommand.CanExecute(null))
            {
                _viewModel.DeleteNodeCommand.Execute(null);
            }
        }
    }

    // Đóng chương trình: 
    private void Image_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        this.Close();
    }

    // Thu nhỏ: 
    private void Image_PreviewMouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
}

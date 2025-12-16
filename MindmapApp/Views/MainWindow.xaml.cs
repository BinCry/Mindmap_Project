using Microsoft.Win32;
using MindmapApp.Models;
using MindmapApp.ViewModels;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq; // Cần thiết cho FirstOrDefault
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MindmapApp.Views;

public partial class MainWindow : Window
{
    #region Fields & Constructor

    private bool isSidebarVisible = true;
    private readonly MainViewModel _viewModel;

    private Point _panStartMousePoint;
    private bool _isPanningMode;
    private double _previousZoomLevel = 1.0;

    public MainWindow(UserAccount account)
    {
        InitializeComponent();

        _viewModel = new MainViewModel(
            App.ExportService,
            App.SearchService,
            App.AiService,
            App.MindmapStorageService,
            App.UserService,
            account
        );

        DataContext = _viewModel;
        _previousZoomLevel = _viewModel.ZoomLevel;

        Placeholder.Visibility = string.IsNullOrEmpty(_viewModel.SearchText) ? Visibility.Visible : Visibility.Collapsed;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        Loaded += OnLoaded;
        Closing += OnClosing;

        _viewModel.RequestCenterView += (s, e) => CenterScrollViewer();
        _viewModel.LogoutRequested += ViewModel_LogoutRequested;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ZoomLevel))
        {
            HandleZoomScaling();
        }
    }

    private void ViewModel_LogoutRequested(object? sender, EventArgs e)
    {
        LoginWindow loginWindow = new LoginWindow();
        loginWindow.Show();
        this.Close();
    }

    #endregion

    #region Zoom & Center Logic

    private void HandleZoomScaling()
    {
        if (MainScrollViewer == null) return;

        double newZoom = _viewModel.ZoomLevel;
        double oldZoom = _previousZoomLevel;

        double viewportCenterX = MainScrollViewer.HorizontalOffset + MainScrollViewer.ViewportWidth / 2;
        double viewportCenterY = MainScrollViewer.VerticalOffset + MainScrollViewer.ViewportHeight / 2;

        double contentCenterX = viewportCenterX / oldZoom;
        double contentCenterY = viewportCenterY / oldZoom;

        double newHorizontalOffset = (contentCenterX * newZoom) - (MainScrollViewer.ViewportWidth / 2);
        double newVerticalOffset = (contentCenterY * newZoom) - (MainScrollViewer.ViewportHeight / 2);

        MainScrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
        MainScrollViewer.ScrollToVerticalOffset(newVerticalOffset);

        _previousZoomLevel = newZoom;
    }

    // SỬA: Logic Center mới cho Canvas lớn
    private void CenterScrollViewer()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (MainScrollViewer == null || MindmapWorkspace == null) return;

            double currentZoom = _viewModel.ZoomLevel;

            // Tìm Node gốc để căn giữa, nếu không thì lấy giữa Canvas
            var rootNode = _viewModel.Nodes.FirstOrDefault(n => n.ToModel().IsRoot);

            double targetX, targetY;

            if (rootNode != null)
            {
                targetX = rootNode.X + (rootNode.Width / 2);
                targetY = rootNode.Y + (rootNode.Height / 2);
            }
            else
            {
                targetX = MainViewModel.CanvasWidth / 2;
                targetY = MainViewModel.CanvasHeight / 2;
            }

            double scaledTargetX = targetX * currentZoom;
            double scaledTargetY = targetY * currentZoom;

            double offsetX = scaledTargetX - (MainScrollViewer.ViewportWidth / 2);
            double offsetY = scaledTargetY - (MainScrollViewer.ViewportHeight / 2);

            MainScrollViewer.ScrollToHorizontalOffset(offsetX);
            MainScrollViewer.ScrollToVerticalOffset(offsetY);

        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    #endregion

    #region Window Lifecycle

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

    private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        this.Close();
    }

    private void Image_PreviewMouseDown_1(object sender, MouseButtonEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    #endregion

    #region UI Logic

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

    #endregion

    #region Node Interaction

    private void NodeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb thumb || thumb.Tag is not NodeViewModel node) return;
        if (!node.IsDraggable) return;

        double changeX = e.HorizontalChange;
        double changeY = e.VerticalChange;

        double newX = node.X + changeX;
        double newY = node.Y + changeY;

        // SỬA: Clamp theo kích thước Canvas 20000
        newX = Math.Clamp(newX, 0, MainViewModel.CanvasWidth - node.Width);
        newY = Math.Clamp(newY, 0, MainViewModel.CanvasHeight - node.Height);

        node.X = newX;
        node.Y = newY;
    }

    private void Thumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Thumb thumb && thumb.Tag is NodeViewModel node)
        {
            if (_viewModel.SelectedNode == node)
            {
                _viewModel.SelectedNode = null;
            }

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
            if (!node.IsDeletable)
            {
                MessageBox.Show(this, "Đây là Chủ đề chính (Central Topic), bạn không thể xóa nó!", "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_viewModel.DeleteNodeCommand.CanExecute(null))
            {
                _viewModel.DeleteNodeCommand.Execute(null);
            }
        }
    }

    #endregion

    #region Panning Logic
    private void MindmapWorkspace_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == MindmapWorkspace)
        {
            _viewModel.SelectedNode = null;
            _viewModel.SelectedConnection = null;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isPanningMode = true;
                _panStartMousePoint = e.GetPosition(this);
                MindmapWorkspace.Cursor = Cursors.ScrollAll;
                MindmapWorkspace.CaptureMouse();
            }
        }
        else if (e.ChangedButton == MouseButton.Middle)
        {
            _isPanningMode = true;
            _panStartMousePoint = e.GetPosition(this);
            MindmapWorkspace.Cursor = Cursors.ScrollAll;
            MindmapWorkspace.CaptureMouse();
        }
    }

    private void MindmapWorkspace_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanningMode)
        {
            var currentMousePoint = e.GetPosition(this);
            var deltaX = currentMousePoint.X - _panStartMousePoint.X;
            var deltaY = currentMousePoint.Y - _panStartMousePoint.Y;

            MainScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset - deltaX);
            MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset - deltaY);

            _panStartMousePoint = currentMousePoint;
        }
    }

    private void MindmapWorkspace_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningMode)
        {
            _isPanningMode = false;
            MindmapWorkspace.ReleaseMouseCapture();
            MindmapWorkspace.Cursor = Cursors.Arrow;
        }
    }
    #endregion

    private void BtnToggleRight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            bool isOpening = btn.IsChecked == true;
            var animationKey = isOpening ? "ExpandRight" : "CollapseRight";
            var sb = this.FindResource(animationKey) as Storyboard;
            sb?.Begin();
        }
    }

    private void Path_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Shapes.Path path && path.DataContext is ConnectionViewModel connection)
        {
            _viewModel.SelectedConnection = connection;
            e.Handled = true;
        }
    }

    // --- LOGIC ẨN/HIỆN MẬT KHẨU ---
    private void ShowCurrentPass_Checked(object sender, RoutedEventArgs e)
    {
        VisiblePbCurrentPass.Text = PbCurrentPass.Password;
        VisiblePbCurrentPass.Visibility = Visibility.Visible;
        PbCurrentPass.Visibility = Visibility.Collapsed;
    }
    private void ShowCurrentPass_Unchecked(object sender, RoutedEventArgs e)
    {
        PbCurrentPass.Password = VisiblePbCurrentPass.Text;
        VisiblePbCurrentPass.Visibility = Visibility.Collapsed;
        PbCurrentPass.Visibility = Visibility.Visible;
    }

    private void ShowNewPass_Checked(object sender, RoutedEventArgs e)
    {
        VisiblePbNewPass.Text = PbNewPass.Password;
        VisiblePbNewPass.Visibility = Visibility.Visible;
        PbNewPass.Visibility = Visibility.Collapsed;
    }
    private void ShowNewPass_Unchecked(object sender, RoutedEventArgs e)
    {
        PbNewPass.Password = VisiblePbNewPass.Text;
        VisiblePbNewPass.Visibility = Visibility.Collapsed;
        PbNewPass.Visibility = Visibility.Visible;
    }

    private void ShowConfirmPass_Checked(object sender, RoutedEventArgs e)
    {
        VisiblePbConfirmPass.Text = PbConfirmPass.Password;
        VisiblePbConfirmPass.Visibility = Visibility.Visible;
        PbConfirmPass.Visibility = Visibility.Collapsed;
    }
    private void ShowConfirmPass_Unchecked(object sender, RoutedEventArgs e)
    {
        PbConfirmPass.Password = VisiblePbConfirmPass.Text;
        VisiblePbConfirmPass.Visibility = Visibility.Collapsed;
        PbConfirmPass.Visibility = Visibility.Visible;
    }

    private async void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            string current = VisiblePbCurrentPass.Visibility == Visibility.Visible
                ? VisiblePbCurrentPass.Text
                : PbCurrentPass.Password;

            string newP = VisiblePbNewPass.Visibility == Visibility.Visible
                ? VisiblePbNewPass.Text
                : PbNewPass.Password;

            string confirm = VisiblePbConfirmPass.Visibility == Visibility.Visible
                ? VisiblePbConfirmPass.Text
                : PbConfirmPass.Password;

            await vm.SaveProfileAsync(current, newP, confirm);

            if (!vm.IsProfileDialogOpen)
            {
                PbCurrentPass.Password = "";
                PbNewPass.Password = "";
                PbConfirmPass.Password = "";

                VisiblePbCurrentPass.Text = "";
                VisiblePbNewPass.Text = "";
                VisiblePbConfirmPass.Text = "";
            }
        }
    }
}
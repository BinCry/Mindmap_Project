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
                targetX = _viewModel.CanvasWidth / 2;
                targetY = _viewModel.CanvasHeight / 2;
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

    private Point _lastNodeDragPoint;
    private void NodeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb thumb || thumb.Tag is not NodeViewModel node) return;
        if (!node.IsDraggable) return;

        // 1. SỬA LỖI TRƯỢT KHI ZOOM:
        // Lấy vị trí chuột mới trên Canvas
        var currentPoint = Mouse.GetPosition(MindmapWorkspace);

        // Tính khoảng cách di chuyển bằng phép trừ toạ độ thực tế
        // Cách này loại bỏ hoàn toàn sai số do tỷ lệ Zoom
        double deltaX = currentPoint.X - _lastNodeDragPoint.X;
        double deltaY = currentPoint.Y - _lastNodeDragPoint.Y;

        double newX = node.X + deltaX;
        double newY = node.Y + deltaY;

        // 2. SỬA LỖI KÍCH THƯỚC ĐỘNG:
        // Thay vì dùng MainViewModel.CanvasWidth (số tĩnh), hãy dùng _viewModel.CanvasWidth
        // (Đây là property động thay đổi theo ComboBox mà ta đã làm ở bước trước)
        double currentCanvasW = _viewModel.CanvasWidth;
        double currentCanvasH = _viewModel.CanvasHeight;

        // Giới hạn biên (Clamping) theo kích thước hiện tại
        newX = Math.Max(0, Math.Min(newX, currentCanvasW - node.Width));
        newY = Math.Max(0, Math.Min(newY, currentCanvasH - node.Height));

        // 3. Cập nhật
        node.X = newX;
        node.Y = newY;

        // Lưu lại vị trí để tính cho frame tiếp theo
        _lastNodeDragPoint = currentPoint;
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

    private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is Thumb && _viewModel.SelectedNode != null)
        {
            // Lấy vị trí chuột tính theo hệ quy chiếu của Canvas (đã bao gồm Zoom)
            _lastNodeDragPoint = Mouse.GetPosition(MindmapWorkspace);
        }
    }

    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var thumb = sender as Thumb;
        var node = thumb.DataContext as NodeViewModel;

        if (node == null) return;

        // Lấy hướng kéo từ Tag (Top, Bottom, Left...) đã gán bên XAML
        string direction = thumb.Tag as string;

        // e.HorizontalChange: Độ lệch ngang (kéo sang phải là dương, trái là âm)
        // e.VerticalChange: Độ lệch dọc (kéo xuống là dương, lên là âm)

        switch (direction)
        {
            case "Right":
                // Kéo phải thì chỉ cần cộng thêm độ lệch vào chiều rộng
                node.Width += e.HorizontalChange;
                break;

            case "Bottom":
                // Kéo xuống thì cộng thêm độ lệch vào chiều cao
                node.Height += e.VerticalChange;
                break;

            case "Left":
                // Kéo trái khó hơn:
                // 1. Tính chiều rộng mới (chiều rộng cũ - độ lệch) -> Vì kéo sang trái (âm) thì rộng ra
                double newWidth = node.Width - e.HorizontalChange;
                // 2. Chỉ cho phép đổi nếu chiều rộng > 50 (giới hạn an toàn)
                if (newWidth > 50)
                {
                    node.X += e.HorizontalChange; // Dời vị trí X sang trái
                    node.Width = newWidth;        // Tăng chiều rộng
                }
                break;

            case "Top":
                double newHeight = node.Height - e.VerticalChange;
                if (newHeight > 40)
                {
                    node.Y += e.VerticalChange; // Dời vị trí Y lên trên
                    node.Height = newHeight;    // Tăng chiều cao
                }
                break;

            case "BottomRight":
                node.Width += e.HorizontalChange;
                node.Height += e.VerticalChange;
                break;

            case "TopRight":
                node.Width += e.HorizontalChange;
                // Logic Top (giống case "Top" ở trên)
                double newHeightTR = node.Height - e.VerticalChange;
                if (newHeightTR > 40)
                {
                    node.Y += e.VerticalChange;
                    node.Height = newHeightTR;
                }
                break;

            case "BottomLeft":
                node.Height += e.VerticalChange;
                // Logic Left (giống case "Left" ở trên)
                double newWidthBL = node.Width - e.HorizontalChange;
                if (newWidthBL > 50)
                {
                    node.X += e.HorizontalChange;
                    node.Width = newWidthBL;
                }
                break;

            case "TopLeft":
                // Kết hợp cả logic Top và Left
                double newWidthTL = node.Width - e.HorizontalChange;
                double newHeightTL = node.Height - e.VerticalChange;

                if (newWidthTL > 50)
                {
                    node.X += e.HorizontalChange;
                    node.Width = newWidthTL;
                }
                if (newHeightTL > 40)
                {
                    node.Y += e.VerticalChange;
                    node.Height = newHeightTL;
                }
                break;
        }

        // Ngăn không cho sự kiện này lan ra ngoài (để không bị dính vào sự kiện di chuyển node)
        e.Handled = true;
    }
}
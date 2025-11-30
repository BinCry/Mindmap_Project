using Microsoft.Win32;
using MindmapApp.Models;
using MindmapApp.ViewModels;
using System;
using System.ComponentModel;
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


    // Các biến dành cho tính năng kéo nền: 
    private Point _panStartMousePoint; // Lưu vị trí chuột bắt đầu
    private bool _isPanningMode;       // Cờ đánh dấu đang kéo


    public MainWindow(UserAccount account)
    {
        InitializeComponent();

        // Khởi tạo ViewModel và gán DataContext để Binding hoạt động: 
        _viewModel = new MainViewModel(App.ExportService, App.SearchService, App.AiService, App.MindmapStorageService, account);
        DataContext = _viewModel;

        // Đăng ký sự kiện Export (Xuất file): 
        _viewModel.RequestExportImage += async (_, _) => await ExportAsImageAsync();
        _viewModel.RequestExportPdf += async (_, _) => await ExportAsPdfAsync();

        // Xử lý Placeholder tìm kiếm (Ẩn hiện "Nhập tên node"): 
        Placeholder.Visibility = string.IsNullOrEmpty(_viewModel.SearchText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Đăng ký sự kiện vòng đời
        Loaded += OnLoaded;
        Closing += OnClosing;

        // Đăng ký sự kiện cuộn ra giữa: 
        _viewModel.RequestCenterView += (s, e) => CenterScrollViewer();
    }

    #endregion

    #region Window Lifecycle (Load, Close, Minimize)


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

    // Nút đóng chương trình (X)
    private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        this.Close();
    }

    // Nút thu nhỏ chương trình (-)
    private void Image_PreviewMouseDown_1(object sender, MouseButtonEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    #endregion

    #region UI Logic (Sidebar, Search)

    // Đóng/Mở thanh bên trái (Sidebar) dùng Animation: 
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        Storyboard storyboard = isSidebarVisible
            ? (Storyboard)FindResource("HideSidebar")
            : (Storyboard)FindResource("ShowSidebar");

        storyboard.Begin(this);
        isSidebarVisible = !isSidebarVisible; // Đảo ngược trạng thái. 

        ToggleButton.Content = isSidebarVisible ? "◀" : "▶"; // Đổi Icon nút bấm. 
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

    #region Node Interaction (Drag, Select, Connect, Delete)

    // 1. Xử lý kéo thả Node 
    // Sự kiện này được kích hoạt bởi Thumb bao quanh Node: 
    private void NodeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb thumb || thumb.Tag is not NodeViewModel node) return;

        if (!node.IsDraggable)
        {
            return;
        }
        // Lấy mức zoom hiện tại để tính toán tốc độ chuột
        var scale = _viewModel.ZoomLevel;
        if (scale <= 0.1) scale = 0.1; // Phòng hờ để mức zoom không được nhỏ hơn 0.1. 

        // Tính toán vị trí mới
        double newX = node.X + (e.HorizontalChange / scale);
        double newY = node.Y + (e.VerticalChange / scale);

        // (Tùy chọn) Giới hạn không cho kéo Node ra khỏi vùng giấy 3000x2000
      
        if (newX < 0) newX = 0;
        if (newY < 0) newY = 0;
        if (newX + node.Width > 3000) newX = 3000 - node.Width;
        if (newY + node.Height > 2000) newY = 2000 - node.Height;
       

        node.X = newX;
        node.Y = newY;
    }
    // 2. Xử lý chọn Node khi Click
    private void Thumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Thumb thumb && thumb.Tag is NodeViewModel node)
        {
            // 1. Cập nhật Node đang chọn
            _viewModel.SelectedNode = node;

            // 2. Nếu đang nối dây thì hoàn tất
            _viewModel.CompleteConnectionCommand.Execute(node);
        }
    }
  

    // 3. Xử lý nút bắt đầu nối dây trên Toolbar
    private void StartConnectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNode != null)
        {
            _viewModel.StartConnectionCommand.Execute(_viewModel.SelectedNode);
        }
    }

    // Xử lý Menu xóa Node (Context Menu)
    private void DeleteNodeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.CommandParameter is NodeViewModel node)
        {
            // set node đang chọn: 
            _viewModel.SelectedNode = node;
            if (!node.IsDeletable)
            {
                // Nếu không được xóa (Node gốc) -> Hiện cảnh báo
                MessageBox.Show(this,
                    "Đây là Chủ đề chính (Central Topic), bạn không thể xóa nó!\nHãy xóa các nhánh con hoặc sửa nội dung thay vì xóa node gốc.",
                    "Không thể xóa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return; // Dừng lại ngay, không thực hiện lệnh xóa bên dưới
            }


            if (_viewModel.DeleteNodeCommand.CanExecute(null))
            {
                _viewModel.DeleteNodeCommand.Execute(null);
            }
        }
    }

    #endregion

    #region Export Features

    private async Task ExportAsImageAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Ảnh PNG (*.png)|*.png",
            FileName = "mindmap.png"
        };

        if (dialog.ShowDialog(this) == true)
        {
            // Lưu ý: Bạn cần đảm bảo _viewModel.ExportToImageAsync nhận tham số phù hợp
            // await _viewModel.ExportToImageAsync(MindmapSurface, dialog.FileName);
            // MessageBox.Show(this, "Đã lưu ảnh mindmap thành công", "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task ExportAsPdfAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Tài liệu PDF (*.pdf)|*.pdf",
            FileName = "mindmap.pdf"
        };

        if (dialog.ShowDialog(this) == true)
        {
            // await _viewModel.ExportToPdfAsync(MindmapSurface, dialog.FileName);
            // MessageBox.Show(this, "Đã lưu PDF mindmap thành công", "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #endregion

    #region Panning Logic 
    private void MindmapWorkspace_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Chỉ kích hoạt khi nhấn vào vùng trống (Grid), không phải nhấn vào Node
        // (Vì Node là Thumb nên nó sẽ bắt sự kiện trước, hàm này sẽ không chạy nếu click trúng Node)
        if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
        {
            _isPanningMode = true;
            _panStartMousePoint = e.GetPosition(this); // Lấy tọa độ so với cửa sổ

            // Đổi con trỏ chuột thành hình bàn tay nắm
            MindmapWorkspace.Cursor = Cursors.ScrollAll;

            // Bắt chuột (Capture) để khi kéo ra ngoài vùng biên vẫn nhận sự kiện
            MindmapWorkspace.CaptureMouse();
        }
    }
    private void MindmapWorkspace_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanningMode)
        {
            var currentMousePoint = e.GetPosition(this);

            // Tính khoảng cách chuột đã di chuyển (Delta) 
            var deltaX = currentMousePoint.X - _panStartMousePoint.X;
            var deltaY = currentMousePoint.Y - _panStartMousePoint.Y;

            // Logic quan trọng: Kéo chuột sang phải -> Scroll phải lùi sang trái (Trừ đi Delta)
            MainScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset - deltaX);
            MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset - deltaY);

            // Cập nhật lại vị trí cũ để tính cho lần di chuyển tiếp theo
            _panStartMousePoint = currentMousePoint;
        }
    }

    private void MindmapWorkspace_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningMode)
        {
            _isPanningMode = false;
            MindmapWorkspace.ReleaseMouseCapture(); // Thả chuột ra
            MindmapWorkspace.Cursor = Cursors.Arrow; // Trả lại con trỏ bình thường
        }
    }

    // Hàm xử lý cuộn thanh Scroll ra chính giữa
    private void CenterScrollViewer()
    {
        // Cần chờ UI load xong mới tính toán được kích thước Viewport
        Dispatcher.InvokeAsync(() =>
        {
            if (MainScrollViewer == null) return;

            // Tính toán điểm giữa của tờ giấy
            double centerH = MindmapWorkspace.Width / 2;
            double centerV = MindmapWorkspace.Height / 2;

            // Trừ đi một nửa kích thước màn hình hiển thị để tâm tờ giấy nằm giữa màn hình
            double offsetX = centerH - (MainScrollViewer.ViewportWidth / 2);
            double offsetY = centerV - (MainScrollViewer.ViewportHeight / 2);

            MainScrollViewer.ScrollToHorizontalOffset(offsetX);
            MainScrollViewer.ScrollToVerticalOffset(offsetY);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
    #endregion

   
}

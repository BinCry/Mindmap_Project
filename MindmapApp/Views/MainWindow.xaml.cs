using Microsoft.Win32;
using MindmapApp.Models;
using MindmapApp.Services;
using MindmapApp.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MindmapApp.Views
{
    public partial class MainWindow : Window
    {
        #region Fields & Constructor

        private bool isSidebarVisible = true;
        private readonly MainViewModel _viewModel;

        private Point _panStartMousePoint;
        private bool _isPanningMode;
        private double _previousZoomLevel = 1.0;
        private bool _isMouseWheelZooming = false;

        private Guid? _pendingMapId;

        public MainWindow(UserAccount account, Guid? mapId = null)
        {
            _pendingMapId = mapId;
            InitializeComponent();

            _viewModel = new MainViewModel(
                App.SearchService,
                App.AiService,
                App.MindmapStorageService,
                App.UserService,
                account
            );

            _viewModel.GetViewportCenterHelper = () =>
            {
                if (MainScrollViewer == null) return new Point(MainViewModel.FixedCanvasWidth / 2, MainViewModel.FixedCanvasHeight / 2);

                double centerX = (MainScrollViewer.HorizontalOffset + MainScrollViewer.ViewportWidth / 2) / _viewModel.ZoomLevel;
                double centerY = (MainScrollViewer.VerticalOffset + MainScrollViewer.ViewportHeight / 2) / _viewModel.ZoomLevel;

                return new Point(centerX, centerY);
            };

            DataContext = _viewModel;
            _previousZoomLevel = _viewModel.ZoomLevel;

            Placeholder.Visibility = string.IsNullOrEmpty(_viewModel.SearchText) ? Visibility.Visible : Visibility.Collapsed;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Loaded += OnLoaded;
            Closing += OnClosing;

            _viewModel.RequestCenterView += (s, e) => CenterOnRootNode();
            _viewModel.LogoutRequested += ViewModel_LogoutRequested;

            MindmapWorkspace.MouseWheel += MindmapWorkspace_MouseWheel;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.ZoomLevel))
            {
                if (!_isMouseWheelZooming)
                {
                    HandleZoomToViewportCenter();
                }
            }
        }

        private void ViewModel_LogoutRequested(object? sender, EventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        #endregion

        #region Zoom Logic & Center Lock

        private void MindmapWorkspace_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                _isMouseWheelZooming = true;

                double oldZoom = _viewModel.ZoomLevel;
                Point mousePosInViewport = e.GetPosition(MainScrollViewer);

                double mouseX_Grid = (MainScrollViewer.HorizontalOffset + mousePosInViewport.X) / oldZoom;
                double mouseY_Grid = (MainScrollViewer.VerticalOffset + mousePosInViewport.Y) / oldZoom;

                double zoomFactor = 1.1;
                double newZoom = e.Delta > 0 ? oldZoom * zoomFactor : oldZoom / zoomFactor;
                newZoom = Math.Clamp(newZoom, 0.2, 5.0);

                _viewModel.ZoomLevel = newZoom;

                double newOffsetX = (mouseX_Grid * newZoom) - mousePosInViewport.X;
                double newOffsetY = (mouseY_Grid * newZoom) - mousePosInViewport.Y;

                MainScrollViewer.ScrollToHorizontalOffset(newOffsetX);
                MainScrollViewer.ScrollToVerticalOffset(newOffsetY);

                _previousZoomLevel = newZoom;
                _isMouseWheelZooming = false;
            }
        }

        private void HandleZoomToViewportCenter()
        {
            if (MainScrollViewer == null) return;

            double oldZoom = _previousZoomLevel;
            double newZoom = _viewModel.ZoomLevel;

            if (Math.Abs(newZoom - oldZoom) < 0.0001) return;

            double viewportCenterX = MainScrollViewer.ViewportWidth / 2;
            double viewportCenterY = MainScrollViewer.ViewportHeight / 2;

            double centerX_Grid = (MainScrollViewer.HorizontalOffset + viewportCenterX) / oldZoom;
            double centerY_Grid = (MainScrollViewer.VerticalOffset + viewportCenterY) / oldZoom;

            double newOffsetX = (centerX_Grid * newZoom) - viewportCenterX;
            double newOffsetY = (centerY_Grid * newZoom) - viewportCenterY;

            MainScrollViewer.ScrollToHorizontalOffset(newOffsetX);
            MainScrollViewer.ScrollToVerticalOffset(newOffsetY);

            _previousZoomLevel = newZoom;
        }

        private void CenterOnRootNode()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (MainScrollViewer == null || _viewModel == null) return;

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

                double currentZoom = _viewModel.ZoomLevel;
                double scaledTargetX = targetX * currentZoom;
                double scaledTargetY = targetY * currentZoom;

                double offsetX = scaledTargetX - (MainScrollViewer.ViewportWidth / 2);
                double offsetY = scaledTargetY - (MainScrollViewer.ViewportHeight / 2);

                MainScrollViewer.ScrollToHorizontalOffset(offsetX);
                MainScrollViewer.ScrollToVerticalOffset(offsetY);

                _previousZoomLevel = currentZoom;

            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        #endregion

        #region Window Lifecycle

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.InitializeAsync(_pendingMapId);
                CenterOnRootNode();
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

        private bool isHeaderVisible = true;
        private double headerTopHeight = 60;
        private double headerToolbarHeight = 60;

        private void ToggleHeader_Click(object sender, RoutedEventArgs e)
        {
            if (isHeaderVisible)
            {
                // Capture heights
                headerTopHeight = HeaderContentGrid.ActualHeight;
                headerToolbarHeight = HeaderToolbarBorder.ActualHeight;

                // Set explicit height to allow animation
                HeaderContentGrid.Height = headerTopHeight;
                HeaderToolbarBorder.Height = headerToolbarHeight;

                // Animate to 0
                DoubleAnimation animTop = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.25)));
                DoubleAnimation animToolbar = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.25)));
                
                HeaderContentGrid.BeginAnimation(FrameworkElement.HeightProperty, animTop);
                HeaderToolbarBorder.BeginAnimation(FrameworkElement.HeightProperty, animToolbar);

                ToggleHeaderButton.Content = "▼";
            }
            else
            {
                // Expand
                DoubleAnimation animTop = new DoubleAnimation(headerTopHeight, new Duration(TimeSpan.FromSeconds(0.25)));
                DoubleAnimation animToolbar = new DoubleAnimation(headerToolbarHeight, new Duration(TimeSpan.FromSeconds(0.25)));

                animTop.Completed += (s, args) => { HeaderContentGrid.Height = double.NaN; }; // Reset to Auto
                animToolbar.Completed += (s, args) => { HeaderToolbarBorder.Height = double.NaN; };

                HeaderContentGrid.BeginAnimation(FrameworkElement.HeightProperty, animTop);
                HeaderToolbarBorder.BeginAnimation(FrameworkElement.HeightProperty, animToolbar);

                ToggleHeaderButton.Content = "▲";
            }
            isHeaderVisible = !isHeaderVisible;
        }

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

            var currentPoint = Mouse.GetPosition(MindmapWorkspace);
            double deltaX = currentPoint.X - _lastNodeDragPoint.X;
            double deltaY = currentPoint.Y - _lastNodeDragPoint.Y;

            double newX = node.X + deltaX;
            double newY = node.Y + deltaY;

            double limitW = MainViewModel.FixedCanvasWidth;
            double limitH = MainViewModel.FixedCanvasHeight;

            newX = Math.Max(0, Math.Min(newX, limitW - node.Width));
            newY = Math.Max(0, Math.Min(newY, limitH - node.Height));

            node.X = newX;
            node.Y = newY;

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
                e.Handled = false;
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

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb && _viewModel.SelectedNode != null)
            {
                _viewModel.RecordHistory();
                _lastNodeDragPoint = Mouse.GetPosition(MindmapWorkspace);
            }
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var thumb = sender as Thumb;
            var node = thumb.DataContext as NodeViewModel;

            if (node == null) return;
            string direction = thumb.Tag as string;

            switch (direction)
            {
                case "Right":
                    node.Width += e.HorizontalChange;
                    break;
                case "Bottom":
                    node.Height += e.VerticalChange;
                    break;
                case "Left":
                    double newWidth = node.Width - e.HorizontalChange;
                    if (newWidth > 50)
                    {
                        node.X += e.HorizontalChange;
                        node.Width = newWidth;
                    }
                    break;
                case "Top":
                    double newHeight = node.Height - e.VerticalChange;
                    if (newHeight > 40)
                    {
                        node.Y += e.VerticalChange;
                        node.Height = newHeight;
                    }
                    break;
                case "BottomRight":
                    node.Width += e.HorizontalChange;
                    node.Height += e.VerticalChange;
                    break;
                case "TopRight":
                    node.Width += e.HorizontalChange;
                    double newHeightTR = node.Height - e.VerticalChange;
                    if (newHeightTR > 40)
                    {
                        node.Y += e.VerticalChange;
                        node.Height = newHeightTR;
                    }
                    break;
                case "BottomLeft":
                    node.Height += e.VerticalChange;
                    double newWidthBL = node.Width - e.HorizontalChange;
                    if (newWidthBL > 50)
                    {
                        node.X += e.HorizontalChange;
                        node.Width = newWidthBL;
                    }
                    break;
                case "TopLeft":
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
            e.Handled = true;
        }

        #endregion

        #region Panning Logic

        private void MindmapWorkspace_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == MindmapWorkspace || e.OriginalSource is Border)
            {
                if (_viewModel != null)
                {
                    _viewModel.IsCanvasSelected = true;
                }

                if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
                {
                    _isPanningMode = true;
                    _panStartMousePoint = e.GetPosition(this);
                    MindmapWorkspace.Cursor = Cursors.ScrollAll;
                    MindmapWorkspace.CaptureMouse();
                }
            }
        }

        private void MindmapWorkspace_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanningMode)
            {
                if (e.LeftButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released)
                {
                    _isPanningMode = false;
                    MindmapWorkspace.ReleaseMouseCapture();
                    MindmapWorkspace.Cursor = Cursors.Arrow;
                    return;
                }

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

        #region Export Logic

        private readonly ImageExportService _imageService = new ImageExportService();
        private readonly PdfExportService _pdfService = new PdfExportService();

        private void BtnExportImage_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Nodes.Count == 0)
            {
                MessageBox.Show("Chưa có nội dung để xuất!", "Thông báo");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg";

            string safeTitle = string.Join("_", _viewModel.Title.Split(System.IO.Path.GetInvalidFileNameChars()));
            dlg.FileName = safeTitle;

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _imageService.ExportImage(dlg.FileName, _viewModel.Nodes, _viewModel.Connections, _viewModel.CurrentUserDisplayName);
                    MessageBox.Show("Xuất ảnh thành công!", "Thông báo");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi xuất ảnh: " + ex.Message);
                }
            }
        }

        private bool ShowPdfExportOptionsWindow(out PdfExportOptionsWindow options)
        {
            options = new PdfExportOptionsWindow()
            {
                Owner = this
            };
            return options.ShowDialog() == true;
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!ShowPdfExportOptionsWindow(out var options))
                return;

            if (_viewModel.Nodes.Count == 0)
            {
                MessageBox.Show("Chưa có nội dung để xuất!", "Thông báo");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "PDF Document (*.pdf)|*.pdf";

            string safeTitle = string.Join("_", _viewModel.Title.Split(System.IO.Path.GetInvalidFileNameChars()));
            dlg.FileName = safeTitle;

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _pdfService.ExportPdf(
                        dlg.FileName,
                        _viewModel.Title,
                        _viewModel.Nodes,
                        _viewModel.Connections,
                        _viewModel.CurrentUserDisplayName,
                        options.IncludeWatermark,
                        options.Password
                    );

                    MessageBox.Show("Xuất PDF thành công!", "Thông báo");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi xuất PDF: " + ex.Message);
                }
            }
        }

        private void BtnRecent_Click(object sender, RoutedEventArgs e)
        {
             // Save current work logic if needed? Auto-save is on.
             var recentMapsWindow = new RecentMapsWindow(_viewModel.CurrentUser);
             recentMapsWindow.Show();
             this.Close();
        }

        private void OnFormattingPanelPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel != null)
            {
               _viewModel.RecordHistory();
            }
        }

        #endregion

        // Xử lý sự kiện click nút +/- để tránh bị Thumb chiếm quyền
        private void ExpandButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                // 1. Ngăn không cho Thumb (lớp dưới) bắt được sự kiện này
                e.Handled = true;

                // 2. Thực thi lệnh mở rộng ngay lập tức
                if (btn.DataContext is NodeViewModel node)
                {
                    // Gọi trực tiếp Command của Node
                    if (node.ToggleExpandCommand.CanExecute(null))
                    {
                        node.ToggleExpandCommand.Execute(null);
                    }
                }
            }
        }
    }
}
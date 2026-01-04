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
using System.Windows.Data; // Thêm thư viện này để xử lý Binding
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Collections.Specialized;
using MindmapApp.Helpers;

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
        private double _prePresentationZoomLevel = 1.0;
        private double _prePresentationOffsetX;
        private double _prePresentationOffsetY;
        private bool _isPresentationAnimating;

        private Guid? _pendingMapId;
        private readonly SignalRService _signalRService;
        private bool _suppressCollaborationEvents;

        public MainWindow(UserAccount account, Guid? mapId = null)
        {
            _pendingMapId = mapId ?? Guid.NewGuid();
            InitializeComponent();

            _signalRService = new SignalRService();

            _viewModel = new MainViewModel(
                App.SearchService,
                App.AiService,
                App.MindmapStorageService,
                App.MindmapCloudService,
                App.UserService,
                account
            );

            _viewModel.CurrentMapId = _pendingMapId.Value.ToString();

            _viewModel.GetViewportCenterHelper = () =>
            {
                if (MainScrollViewer == null) return new Point(MainViewModel.FixedCanvasWidth / 2, MainViewModel.FixedCanvasHeight / 2);
                double centerX = (MainScrollViewer.HorizontalOffset + MainScrollViewer.ViewportWidth / 2) / _viewModel.ZoomLevel;
                double centerY = (MainScrollViewer.VerticalOffset + MainScrollViewer.ViewportHeight / 2) / _viewModel.ZoomLevel;
                return new Point(centerX, centerY);
            };

            DataContext = _viewModel;
            _previousZoomLevel = _viewModel.ZoomLevel;

            if (Placeholder != null)
                Placeholder.Visibility = string.IsNullOrEmpty(_viewModel.SearchText) ? Visibility.Visible : Visibility.Collapsed;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.Nodes.CollectionChanged += NodesOnCollectionChanged;
            _viewModel.Connections.CollectionChanged += ConnectionsOnCollectionChanged;

            Loaded += OnLoaded;
            Closing += OnClosing;

            _viewModel.RequestCenterView += (s, e) => CenterOnRootNode();
            _viewModel.LogoutRequested += ViewModel_LogoutRequested;
            _viewModel.NodeLockRequested += ViewModel_NodeLockRequested;
            _viewModel.NodeUnlockRequested += ViewModel_NodeUnlockRequested;

            if (MindmapWorkspace != null)
                MindmapWorkspace.MouseWheel += MindmapWorkspace_MouseWheel;

            InitializeSignalR();
        }

        private async void InitializeSignalR()
        {
            try
            {
                await _signalRService.StartAsync();
                await JoinSignalRGroupAsync();

                _signalRService.OnNodeMovedEvent += (nodeId, x, y) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var node = _viewModel.Nodes.FirstOrDefault(n => n.Id.ToString() == nodeId);
                        if (node != null)
                        {
                            node.X = x;
                            node.Y = y;
                        }
                    });
                };

                _signalRService.OnNodeLockedEvent += (nodeId, email, color) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var node = _viewModel.Nodes.FirstOrDefault(n => n.Id.ToString() == nodeId);
                        if (node != null)
                        {
                            node.IsLockedByRemote = true;
                            node.LockedByEmail = email;
                            node.RemoteLockColor = color;
                        }
                    });
                };

                _signalRService.OnNodeUnlockedEvent += (nodeId) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var node = _viewModel.Nodes.FirstOrDefault(n => n.Id.ToString() == nodeId);
                        if (node != null)
                        {
                            node.IsLockedByRemote = false;
                            node.LockedByEmail = null;
                        }
                    });
                };

                _signalRService.OnUserJoinedEvent += (email) =>
                {
                    Dispatcher.Invoke(() => _viewModel.AddOnlineUser(email));
                };

                _signalRService.OnNodeAddedEvent += (nodeData) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _suppressCollaborationEvents = true;
                        try
                        {
                            if (_viewModel.Nodes.Any(n => n.Id == nodeData.Id)) return;
                            var node = new NodeViewModel(new NodeModel
                            {
                                Id = nodeData.Id,
                                Title = nodeData.Title,
                                ContentXaml = nodeData.ContentXaml,
                                X = nodeData.X,
                                Y = nodeData.Y,
                                Width = nodeData.Width,
                                Height = nodeData.Height,
                                Shape = nodeData.Shape,
                                BackgroundColor = ParseColor(nodeData.BackgroundColor),
                                BorderColor = ParseColor(nodeData.BorderColor),
                                TextColor = ParseColor(nodeData.TextColor),
                                BackgroundGridStyle = nodeData.BackgroundGridStyle,
                                FontSize = nodeData.FontSize,
                                FontFamily = nodeData.FontFamily,
                                IsBold = nodeData.IsBold,
                                IsItalic = nodeData.IsItalic,
                                IsUnderline = nodeData.IsUnderline,
                                IsStrikethrough = nodeData.IsStrikethrough
                            });
                            _viewModel.Nodes.Add(node);
                        }
                        finally
                        {
                            _suppressCollaborationEvents = false;
                        }
                    });
                };

                _signalRService.OnNodeUpdatedEvent += (nodeData) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _suppressCollaborationEvents = true;
                        try
                        {
                            var node = _viewModel.Nodes.FirstOrDefault(n => n.Id == nodeData.Id);
                            if (node == null) return;

                            ApplyNodeSync(node, nodeData);
                        }
                        finally
                        {
                            _suppressCollaborationEvents = false;
                        }
                    });
                };

                _signalRService.OnConnectionAddedEvent += (connData) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _suppressCollaborationEvents = true;
                        try
                        {
                            if (_viewModel.Connections.Any(c => c.Id == connData.Id)) return;
                            var connection = new ConnectionViewModel(new ConnectionModel
                            {
                                Id = connData.Id,
                                SourceId = connData.SourceId,
                                TargetId = connData.TargetId,
                                StrokeColor = ParseColor(connData.StrokeColor),
                                Thickness = connData.Thickness,
                                IsCurved = connData.IsCurved,
                                DashOffset = connData.DashOffset,
                                DashArray = connData.DashArray != null ? new System.Windows.Media.DoubleCollection(connData.DashArray) : null,
                                ArrowStyle = connData.ArrowStyle
                            });
                            connection.Source = _viewModel.Nodes.FirstOrDefault(n => n.Id == connData.SourceId);
                            connection.Target = _viewModel.Nodes.FirstOrDefault(n => n.Id == connData.TargetId);
                            if (connection.Source != null && connection.Target != null)
                            {
                                connection.Target.Parent = connection.Source;
                                _viewModel.Connections.Add(connection);
                            }
                        }
                        finally
                        {
                            _suppressCollaborationEvents = false;
                        }
                    });
                };

                _signalRService.OnConnectionUpdatedEvent += (connData) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _suppressCollaborationEvents = true;
                        try
                        {
                            var connection = _viewModel.Connections.FirstOrDefault(c => c.Id == connData.Id);
                            if (connection == null) return;
                            ApplyConnectionSync(connection, connData);
                        }
                        finally
                        {
                            _suppressCollaborationEvents = false;
                        }
                    });
                };

                _signalRService.OnNodeTextUpdatedEvent += (nodeId, text) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _suppressCollaborationEvents = true;
                        try
                        {
                            var node = _viewModel.Nodes.FirstOrDefault(n => n.Id.ToString() == nodeId);
                            if (node != null)
                            {
                                node.ContentXaml = text;
                            }
                        }
                        finally
                        {
                            _suppressCollaborationEvents = false;
                        }
                    });
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SignalR Error: " + ex.Message);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.ZoomLevel))
            {
                if (!_isMouseWheelZooming) HandleZoomToViewportCenter();
            }

            if (e.PropertyName == nameof(MainViewModel.CurrentMapId))
            {
                _ = JoinSignalRGroupAsync();
            }

            if (e.PropertyName == nameof(MainViewModel.IsPresentationMode))
            {
                HandlePresentationModeChanged(_viewModel.IsPresentationMode);
            }
        }

        private async Task JoinSignalRGroupAsync()
        {
            if (_viewModel.CurrentUser != null && !string.IsNullOrWhiteSpace(_viewModel.CurrentMapId))
            {
                await _signalRService.JoinMapGroup(_viewModel.CurrentMapId, _viewModel.CurrentUser.Email);
            }
        }

        private void ViewModel_LogoutRequested(object? sender, EventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private async void ViewModel_NodeLockRequested(object? sender, NodeViewModel node)
        {
            if (_viewModel.CurrentUser != null && !node.IsLockedByRemote)
            {
                await _signalRService.LockNode(_viewModel.CurrentMapId, node.Id.ToString(),
                    _viewModel.CurrentUser.Email, _viewModel.CurrentUser.UserColor ?? "#000000");
            }
        }

        private async void ViewModel_NodeUnlockRequested(object? sender, NodeViewModel node)
        {
            if (!node.IsLockedByRemote)
            {
                await _signalRService.UnlockNode(_viewModel.CurrentMapId, node.Id.ToString());
            }
        }

        #endregion

        #region Zoom Logic & Center Lock

        private void MindmapWorkspace_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool allowZoom = Keyboard.Modifiers == ModifierKeys.Control || _viewModel.IsPresentationMode;
            if (!allowZoom) return;

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

        private void HandlePresentationModeChanged(bool isPresentation)
        {
            if (MainScrollViewer == null) return;

            if (isPresentation)
            {
                _prePresentationZoomLevel = _viewModel.ZoomLevel;
                _prePresentationOffsetX = MainScrollViewer.HorizontalOffset;
                _prePresentationOffsetY = MainScrollViewer.VerticalOffset;

                WindowState = WindowState.Maximized;

                Dispatcher.InvokeAsync(CenterOnRootNode, System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                Dispatcher.InvokeAsync(async () => await AnimateZoomAndOffsets(_prePresentationZoomLevel, _prePresentationOffsetX, _prePresentationOffsetY),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private async Task AnimatePresentationFocus(NodeViewModel node)
        {
            if (MainScrollViewer == null) return;

            double viewportSize = Math.Min(MainScrollViewer.ViewportWidth, MainScrollViewer.ViewportHeight);
            double nodeSize = Math.Max(node.Width, node.Height);
            double targetZoom = Math.Clamp((viewportSize * 0.55) / Math.Max(1, nodeSize), 0.6, 2.5);

            double centerX = node.X + (node.Width / 2);
            double centerY = node.Y + (node.Height / 2);
            await AnimateZoomAndCenter(targetZoom, centerX, centerY);
        }

        private async Task AnimateZoomAndCenter(double targetZoom, double centerX, double centerY)
        {
            if (MainScrollViewer == null) return;

            double targetOffsetX = (centerX * targetZoom) - (MainScrollViewer.ViewportWidth / 2);
            double targetOffsetY = (centerY * targetZoom) - (MainScrollViewer.ViewportHeight / 2);

            await AnimateZoomAndOffsets(targetZoom, targetOffsetX, targetOffsetY);
        }

        private async Task AnimateZoomAndOffsets(double targetZoom, double targetOffsetX, double targetOffsetY)
        {
            if (MainScrollViewer == null) return;

            _isPresentationAnimating = true;
            double startZoom = _viewModel.ZoomLevel;
            double startOffsetX = MainScrollViewer.HorizontalOffset;
            double startOffsetY = MainScrollViewer.VerticalOffset;

            const int steps = 12;
            const int delayMs = 16;

            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                double eased = t * t * (3 - 2 * t);

                double currentZoom = startZoom + (targetZoom - startZoom) * eased;
                double currentOffsetX = startOffsetX + (targetOffsetX - startOffsetX) * eased;
                double currentOffsetY = startOffsetY + (targetOffsetY - startOffsetY) * eased;

                _isMouseWheelZooming = true;
                _viewModel.ZoomLevel = currentZoom;
                _isMouseWheelZooming = false;

                MainScrollViewer.ScrollToHorizontalOffset(currentOffsetX);
                MainScrollViewer.ScrollToVerticalOffset(currentOffsetY);

                await Task.Delay(delayMs);
            }

            _previousZoomLevel = targetZoom;
            _isPresentationAnimating = false;
        }

        #endregion

        #region Window Lifecycle

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _suppressCollaborationEvents = true;
                await _viewModel.InitializeAsync(_pendingMapId);
                CenterOnRootNode();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _suppressCollaborationEvents = false;
            }
        }

        private async void OnClosing(object? sender, CancelEventArgs e)
        {
            try
            {
                await _viewModel.FlushAutoSaveAsync();
                if (_signalRService != null) await _signalRService.StopAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e) => this.Close();
        private void Image_PreviewMouseDown_1(object sender, MouseButtonEventArgs e) => WindowState = WindowState.Minimized;

        #endregion

        #region UI Logic

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            Storyboard storyboard = isSidebarVisible ? (Storyboard)FindResource("HideSidebar") : (Storyboard)FindResource("ShowSidebar");
            storyboard.Begin(this);
            isSidebarVisible = !isSidebarVisible;
            if (ToggleButton != null) ToggleButton.Content = isSidebarVisible ? "◀" : "▶";
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

        private bool isHeaderVisible = true;
        private double headerTopHeight = 60;
        private double headerToolbarHeight = 60;

        private void ToggleHeader_Click(object sender, RoutedEventArgs e)
        {
            if (isHeaderVisible)
            {
                headerTopHeight = HeaderContentGrid.ActualHeight;
                headerToolbarHeight = HeaderToolbarBorder.ActualHeight;
                HeaderContentGrid.Height = headerTopHeight;
                HeaderToolbarBorder.Height = headerToolbarHeight;

                DoubleAnimation animTop = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.25)));
                DoubleAnimation animToolbar = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.25)));

                HeaderContentGrid.BeginAnimation(FrameworkElement.HeightProperty, animTop);
                HeaderToolbarBorder.BeginAnimation(FrameworkElement.HeightProperty, animToolbar);
                ToggleHeaderButton.Content = "▼";
            }
            else
            {
                DoubleAnimation animTop = new DoubleAnimation(headerTopHeight, new Duration(TimeSpan.FromSeconds(0.25)));
                DoubleAnimation animToolbar = new DoubleAnimation(headerToolbarHeight, new Duration(TimeSpan.FromSeconds(0.25)));

                animTop.Completed += (s, args) => { HeaderContentGrid.Height = double.NaN; };
                animToolbar.Completed += (s, args) => { HeaderToolbarBorder.Height = double.NaN; };

                HeaderContentGrid.BeginAnimation(FrameworkElement.HeightProperty, animTop);
                HeaderToolbarBorder.BeginAnimation(FrameworkElement.HeightProperty, animToolbar);
                ToggleHeaderButton.Content = "▲";
            }
            isHeaderVisible = !isHeaderVisible;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Placeholder != null && SearchBox != null)
                Placeholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (NodeViewModel node in e.OldItems)
                {
                    node.PropertyChanged -= NodeOnPropertyChanged;
                }
            }

            if (e.NewItems == null) return;

            foreach (NodeViewModel node in e.NewItems)
            {
                node.PropertyChanged += NodeOnPropertyChanged;
                if (_viewModel.IsLoading || _suppressCollaborationEvents) continue;
                _ = _signalRService.SendAddNode(_viewModel.CurrentMapId, ToNodeSyncDto(node));
            }
        }

        private void ConnectionsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ConnectionViewModel conn in e.OldItems)
                {
                    conn.PropertyChanged -= ConnectionOnPropertyChanged;
                }
            }

            if (e.NewItems == null) return;

            foreach (ConnectionViewModel conn in e.NewItems)
            {
                conn.PropertyChanged += ConnectionOnPropertyChanged;
                if (_viewModel.IsLoading || _suppressCollaborationEvents) continue;
                _ = _signalRService.SendAddConnection(_viewModel.CurrentMapId, ToConnectionSyncDto(conn));
            }
        }

        private async void NodeOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not NodeViewModel node) return;
            if (_viewModel.IsLoading || _suppressCollaborationEvents) return;
            if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

            if (e.PropertyName == nameof(NodeViewModel.ContentXaml))
            {
                await _signalRService.SendNodeTextUpdate(_viewModel.CurrentMapId, node.Id.ToString(), node.ContentXaml);
                return;
            }

            if (e.PropertyName == nameof(NodeViewModel.X)
                || e.PropertyName == nameof(NodeViewModel.Y)
                || e.PropertyName == nameof(NodeViewModel.IsSelected)
                || e.PropertyName == nameof(NodeViewModel.IsVisible)
                || e.PropertyName == nameof(NodeViewModel.IsExpanded)
                || e.PropertyName == nameof(NodeViewModel.IsLockedByRemote)
                || e.PropertyName == nameof(NodeViewModel.LockedByEmail)
                || e.PropertyName == nameof(NodeViewModel.RemoteLockColor))
            {
                return;
            }

            await _signalRService.SendUpdateNode(_viewModel.CurrentMapId, ToNodeSyncDto(node));
        }

        private async void ConnectionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ConnectionViewModel connection) return;
            if (_viewModel.IsLoading || _suppressCollaborationEvents) return;
            if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

            if (e.PropertyName == nameof(ConnectionViewModel.IsSelected)
                || e.PropertyName == nameof(ConnectionViewModel.IsHighlighted)
                || e.PropertyName == nameof(ConnectionViewModel.Source)
                || e.PropertyName == nameof(ConnectionViewModel.Target))
            {
                return;
            }

            await _signalRService.SendUpdateConnection(_viewModel.CurrentMapId, ToConnectionSyncDto(connection));
        }

        private static NodeSyncDto ToNodeSyncDto(NodeViewModel node)
        {
            return new NodeSyncDto(
                node.Id,
                node.Title,
                node.ContentXaml,
                node.X,
                node.Y,
                node.Width,
                node.Height,
                node.Shape,
                node.BackgroundColor.ToString(),
                node.BorderColor.ToString(),
                node.TextColor.ToString(),
                node.BackgroundGridStyle,
                node.FontSize,
                node.FontFamily,
                node.IsBold,
                node.IsItalic,
                node.IsUnderline,
                node.IsStrikethrough);
        }

        private static ConnectionSyncDto ToConnectionSyncDto(ConnectionViewModel connection)
        {
            var model = connection.ToModel();
            return new ConnectionSyncDto(
                model.Id,
                model.SourceId,
                model.TargetId,
                model.StrokeColor.ToString(),
                model.Thickness,
                model.IsCurved,
                model.DashOffset,
                model.DashArray?.ToArray(),
                model.ArrowStyle ?? "None");
        }

        private static System.Windows.Media.Color ParseColor(string color)
        {
            if (System.Windows.Media.ColorConverter.ConvertFromString(color) is System.Windows.Media.Color parsed)
            {
                return parsed;
            }
            return System.Windows.Media.Colors.Transparent;
        }

        private static void ApplyNodeSync(NodeViewModel node, NodeSyncDto data)
        {
            node.Title = data.Title;
            node.ContentXaml = data.ContentXaml;
            node.X = data.X;
            node.Y = data.Y;
            node.Width = data.Width;
            node.Height = data.Height;
            node.Shape = data.Shape;
            node.BackgroundColor = ParseColor(data.BackgroundColor);
            node.BorderColor = ParseColor(data.BorderColor);
            node.TextColor = ParseColor(data.TextColor);
            node.BackgroundGridStyle = data.BackgroundGridStyle;
            node.FontSize = data.FontSize;
            node.FontFamily = data.FontFamily;
            node.IsBold = data.IsBold;
            node.IsItalic = data.IsItalic;
            node.IsUnderline = data.IsUnderline;
            node.IsStrikethrough = data.IsStrikethrough;
        }

        private static void ApplyConnectionSync(ConnectionViewModel connection, ConnectionSyncDto data)
        {
            connection.StrokeColor = ParseColor(data.StrokeColor);
            connection.Thickness = data.Thickness;
            connection.DashArray = data.DashArray != null ? new System.Windows.Media.DoubleCollection(data.DashArray) : null;
            connection.LineStyle = data.DashArray != null ? "Dashed" : "Solid";
            connection.ArrowStyle = data.ArrowStyle;
            connection.ConnectionStyle = data.IsCurved ? ConnectionStyle.Bezier : ConnectionStyle.Straight;
        }

        #endregion

        #region Node Interaction

        private Point _lastNodeDragPoint;

        private async void NodeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb || thumb.Tag is not NodeViewModel node) return;

            if (node.IsLockedByRemote) return;
            if (!node.IsDraggable) return;

            var currentPoint = Mouse.GetPosition(MindmapWorkspace);
            double deltaX = currentPoint.X - _lastNodeDragPoint.X;
            double deltaY = currentPoint.Y - _lastNodeDragPoint.Y;

            double newX = node.X + deltaX;
            double newY = node.Y + deltaY;

            newX = Math.Max(0, Math.Min(newX, MainViewModel.FixedCanvasWidth - node.Width));
            newY = Math.Max(0, Math.Min(newY, MainViewModel.FixedCanvasHeight - node.Height));

            node.X = newX;
            node.Y = newY;

            _lastNodeDragPoint = currentPoint;

            await _signalRService.SendMoveNode(_viewModel.CurrentMapId, node.Id.ToString(), newX, newY);
        }

        private async void Thumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is NodeViewModel node)
            {
                if (_viewModel.SelectedNode == node) _viewModel.SelectedNode = null;
                _viewModel.SelectedNode = node;
                _viewModel.CompleteConnectionCommand.Execute(node);

                if (_viewModel.IsPresentationMode)
                {
                    FocusPresentationNode(node);
                }

                if (_viewModel.CurrentUser != null && !node.IsLockedByRemote)
                {
                    await _signalRService.LockNode(_viewModel.CurrentMapId, node.Id.ToString(),
                                                   _viewModel.CurrentUser.Email,
                                                   _viewModel.CurrentUser.UserColor ?? "#000000");
                }
                e.Handled = false;
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

        private async void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is NodeViewModel node)
            {
                await _signalRService.UnlockNode(_viewModel.CurrentMapId, node.Id.ToString());
            }
        }

        private async void FocusPresentationNode(NodeViewModel node)
        {
            if (MainScrollViewer == null || _isPresentationAnimating) return;
            await AnimatePresentationFocus(node);
        }

        private void CenterOnNode(NodeViewModel node)
        {
            // Focus node vào giữa màn hình.
            double currentZoom = _viewModel.ZoomLevel;
            double centerX = (node.X + node.Width / 2) * currentZoom;
            double centerY = (node.Y + node.Height / 2) * currentZoom;

            double offsetX = centerX - (MainScrollViewer.ViewportWidth / 2);
            double offsetY = centerY - (MainScrollViewer.ViewportHeight / 2);

            MainScrollViewer.ScrollToHorizontalOffset(offsetX);
            MainScrollViewer.ScrollToVerticalOffset(offsetY);
            _previousZoomLevel = currentZoom;
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

        private void StartConnectionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedNode != null) _viewModel.StartConnectionCommand.Execute(_viewModel.SelectedNode);
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
                if (_viewModel.DeleteNodeCommand.CanExecute(null)) _viewModel.DeleteNodeCommand.Execute(null);
            }
        }

        #endregion

        #region Panning Logic

        private void MindmapWorkspace_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == MindmapWorkspace || e.OriginalSource is Border)
            {
                if (_viewModel != null) _viewModel.IsCanvasSelected = true;

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

        // 🔥 HÀM MỚI: Ép buộc cập nhật dữ liệu từ Control đang Focus về ViewModel
        private void ForceUpdateFocusedElement()
        {
            var focusedElement = Keyboard.FocusedElement as FrameworkElement;

            // Nếu là TextBox, ép cập nhật Binding ngay lập tức
            if (focusedElement is TextBox textBox)
            {
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }

            if (focusedElement is RichTextBox richTextBox)
            {
                var binding = richTextBox.GetBindingExpression(RichTextBoxHelper.DocumentXamlProperty);
                binding?.UpdateSource();
            }

            // Đồng thời ép Focus ra khỏi Control hiện tại (về Window chính) để chắc chắn sự kiện LostFocus xảy ra
            // Điều này hỗ trợ các control khác (như RichTextBox custom) nếu có
            Keyboard.ClearFocus();
            this.Focus();
        }

        private void BtnExportImage_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsProAccount)
            {
                if (MessageBox.Show("Tính năng Xuất Ảnh chỉ dành cho tài khoản Pro.\n\nBạn có muốn nâng cấp ngay (10k trọn đời) không?",
                    "Tính năng Pro", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _viewModel.OpenUpgradeWindow();
                }
                return;
            }

            if (_viewModel.Nodes.Count == 0)
            {
                MessageBox.Show("Chưa có nội dung để xuất!", "Thông báo");
                return;
            }

            // GỌI HÀM CẬP NHẬT TRƯỚC KHI LƯU
            ForceUpdateFocusedElement();

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
            options = new PdfExportOptionsWindow() { Owner = this };
            return options.ShowDialog() == true;
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsProAccount)
            {
                if (MessageBox.Show("Tính năng Xuất PDF chỉ dành cho tài khoản Pro.\n\nBạn có muốn nâng cấp ngay (10k trọn đời) không?",
                    "Tính năng Pro", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _viewModel.OpenUpgradeWindow();
                }
                return;
            }

            if (!ShowPdfExportOptionsWindow(out var options)) return;

            if (_viewModel.Nodes.Count == 0)
            {
                MessageBox.Show("Chưa có nội dung để xuất!", "Thông báo");
                return;
            }

            // 🔥 QUAN TRỌNG: Gọi hàm này để lưu text đang gõ dở
            ForceUpdateFocusedElement();

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "PDF Document (*.pdf)|*.pdf";
            string safeTitle = string.Join("_", _viewModel.Title.Split(System.IO.Path.GetInvalidFileNameChars()));
            dlg.FileName = safeTitle;

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _pdfService.ExportPdf(dlg.FileName, _viewModel.Title, _viewModel.Nodes, _viewModel.Connections, _viewModel.CurrentUserDisplayName, options.IncludeWatermark, options.Password);
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
            var recentMapsWindow = new RecentMapsWindow(_viewModel.CurrentUser);
            recentMapsWindow.Show();
            this.Close();
        }

        private void OnFormattingPanelPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel != null) _viewModel.RecordHistory();
        }

        #endregion

        private void ExpandButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                e.Handled = true;
                if (btn.DataContext is NodeViewModel node)
                {
                    if (node.ToggleExpandCommand.CanExecute(null))
                    {
                        node.ToggleExpandCommand.Execute(null);
                    }
                }
            }
        }
    }
}

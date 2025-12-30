using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindmapApp.Models;
using MindmapApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MindmapApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        #region Fields
        private readonly MindmapSearchService _searchService;
        private readonly MindmapAiService _aiService;
        private readonly MindmapStorageService _storageService;
        private readonly UserService _userService;
        private readonly UserAccount _currentUser;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly string[] _palette = { "#E3F2FD", "#FCE4EC", "#E8F5E9", "#FFF3E0", "#F3E5F5", "#E0F7FA" };

        public Func<Point>? GetViewportCenterHelper { get; set; }

        [ObservableProperty] private string _title = "Mindmap của tôi";
        [ObservableProperty] private NodeViewModel? _selectedNode;
        [ObservableProperty] private ConnectionViewModel? _selectedConnection;
        [ObservableProperty] private bool _isPresentationMode; // Biến mới: Trạng thái trình chiếu
        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                double clampedValue = Math.Clamp(value, 0.2, 5.0);
                if (Math.Abs(_zoomLevel - clampedValue) > 0.001)
                {
                    SetProperty(ref _zoomLevel, clampedValue);
                }
            }
        }

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isCanvasSelected;
        [ObservableProperty] private Color _canvasBackgroundColor = Colors.Transparent;
        [ObservableProperty] private string _canvasGridStyle = "Light";
        [ObservableProperty] private bool _isProfileDialogOpen;
        [ObservableProperty] private string _editDisplayName = string.Empty;
        [ObservableProperty] private string _nodeHexColor;
        [ObservableProperty] private string _connectionHexColor;

        private NodeViewModel? _pendingConnectionNode;
        private NodeViewModel? _pendingDisconnectNode;
        private bool _isLoading;
        private bool _isSaving;
        private Guid _currentDocumentId;

        public event EventHandler? RequestCenterView;
        public event EventHandler? LogoutRequested;
        #endregion

        public const double FixedCanvasWidth = 8000;
        public const double FixedCanvasHeight = 6000;
        public double CanvasWidth => FixedCanvasWidth;
        public double CanvasHeight => FixedCanvasHeight;
        public UserAccount CurrentUser => _currentUser;
        public string CurrentUserDisplayName => _currentUser?.DisplayName ?? "User";

        public ObservableCollection<NodeViewModel> Nodes { get; } = new();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new();
        public ObservableCollection<NodeViewModel> SearchResults { get; } = new();
        public ObservableCollection<string> ShapeOptions { get; }
        public ObservableCollection<string> FontOptions { get; }
        public ObservableCollection<double> FontSizeOptions { get; }
        public ObservableCollection<Color> ColorPalette { get; }
        public ObservableCollection<Color> TextColorPalette { get; }

        private readonly UndoRedoService _undoRedoService;

        public MainViewModel(MindmapSearchService searchService, MindmapAiService aiService, MindmapStorageService storageService, UserService userService, UserAccount currentUser)
        {
            _searchService = searchService;
            _aiService = aiService;
            _storageService = storageService;
            _userService = userService;
            _currentUser = currentUser;

            // Use the singleton instance
            _undoRedoService = App.UndoRedoService;

            ShapeOptions = new ObservableCollection<string>(new[] { "RoundedRectangle", "Rectangle", "Ellipse", "Diamond", "Parallelogram", "Hexagon" });

            Nodes.CollectionChanged += NodesOnCollectionChanged;
            Connections.CollectionChanged += ConnectionsOnCollectionChanged;

            FontOptions = new ObservableCollection<string>(Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(name => name));
            FontSizeOptions = new ObservableCollection<double>(new[] { 10d, 11d, 12d, 14d, 16d, 18d, 20d, 24d, 28d, 32d });

            ColorPalette = new ObservableCollection<Color>(new[] {
                (Color)ColorConverter.ConvertFromString("#FF4E89AE")!, (Color)ColorConverter.ConvertFromString("#FF42A5F5")!,
                (Color)ColorConverter.ConvertFromString("#FFF9A620")!, (Color)ColorConverter.ConvertFromString("#FF81C784")!,
                (Color)ColorConverter.ConvertFromString("#FFBA68C8")!, (Color)ColorConverter.ConvertFromString("#FFFF8A65")!,
                (Color)ColorConverter.ConvertFromString("#FF4DD0E1")!
            });

            TextColorPalette = new ObservableCollection<Color>(new[] {
                Colors.Black, Colors.White, (Color)ColorConverter.ConvertFromString("#FF333333"),
                (Color)ColorConverter.ConvertFromString("#FFD32F2F"), (Color)ColorConverter.ConvertFromString("#FF1976D2"),
                (Color)ColorConverter.ConvertFromString("#FF388E3C"), (Color)ColorConverter.ConvertFromString("#FF7B1FA2")
            });

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autoSaveTimer.Tick += async (_, _) => await AutoSaveAsync();
        }

        private bool CanUndoOp() => _undoRedoService.CanUndo;
        private bool CanRedoOp() => _undoRedoService.CanRedo;

        [RelayCommand(CanExecute = nameof(CanUndoOp))]
        private void Undo()
        {
            if (!_undoRedoService.CanUndo) return;
            var snapshot = BuildDocumentSnapshot();
            var prev = _undoRedoService.Undo(snapshot);
            if (prev != null)
            {
                LoadMindmap(prev, keepHistory: true);
                StatusMessage = "Đã hoàn tác";
                NotifyUndoRedoState();
            }
        }

        [RelayCommand]
        private void StartPresentation()
        {
            // 1. KIỂM TRA: Đếm số lượng Node gốc (Node không có cha)
            var rootNodes = Nodes.Where(n => n.Parent == null).ToList();

            if (rootNodes.Count == 0)
            {
                MessageBox.Show("Sơ đồ trống, không thể trình chiếu!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (rootNodes.Count > 1)
            {
                MessageBox.Show("Sơ đồ đang bị rời rạc (có nhiều chủ đề chính). Vui lòng nối chúng lại thành 1 cây duy nhất để trình chiếu.",
                                "Không thể trình chiếu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. XÁC NHẬN
            var result = MessageBox.Show("Sẵn sàng vào chế độ trình chiếu?\n\nChế độ này sẽ ẩn các công cụ và bắt đầu từ chủ đề trung tâm.",
                                         "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // 3. THIẾT LẬP TRẠNG THÁI
            IsPresentationMode = true;
            SelectedNode = null;      // Bỏ chọn Node để giao diện sạch sẽ
            SelectedConnection = null; // Bỏ chọn dây nối
            IsCanvasSelected = true;   // Focus vào nền

            // 4. RESET NODE: Gọi hàm ResetForPresentation ta vừa viết ở Bước 1
            var root = rootNodes.First();
            foreach (var node in Nodes)
            {
                // Chỉ hiện node nếu nó là Root, còn lại ẩn hết
                node.ResetForPresentation(isRoot: node == root);
            }

            // (Tuỳ chọn) Nếu bạn có hàm CenterView, hãy gọi ở đây để đưa root ra giữa
            RequestCenterView?.Invoke(this, EventArgs.Empty);
        }

        // Thoát trình chiếu
        [RelayCommand]
        private void ExitPresentation()
        {
            IsPresentationMode = false;

            // Khôi phục hiển thị cho tất cả node
            foreach (var node in Nodes)
            {
                node.ResetToNormal();
            }
        }

        [RelayCommand(CanExecute = nameof(CanRedoOp))]
        private void Redo()
        {
            if (!_undoRedoService.CanRedo) return;
            var snapshot = BuildDocumentSnapshot();
            var next = _undoRedoService.Redo(snapshot);
            if (next != null)
            {
                LoadMindmap(next, keepHistory: true);
                LoadMindmap(next, keepHistory: true);
                StatusMessage = "Đã làm lại";
                NotifyUndoRedoState();
            }
        }

        public void RecordHistory()
        {
            _undoRedoService.RecordState(BuildDocumentSnapshot());
            NotifyUndoRedoState();
        }

        private void NotifyUndoRedoState()
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedNodeChanged(NodeViewModel? value)
        {
            if (value != null)
            {
                foreach (var node in Nodes)
                {
                    if (node != value && node.IsSelected)
                    {
                        node.IsSelected = false;
                    }
                }

                value.IsSelected = true;
                NodeHexColor = value.BackgroundColor.ToString();

                foreach (var c in Connections) c.IsSelected = false;
                SelectedConnection = null;
                IsCanvasSelected = false;
            }
            CommandManager.InvalidateRequerySuggested();
        }

        partial void OnSelectedConnectionChanged(ConnectionViewModel? value)
        {
            if (value != null)
            {
                value.IsSelected = true;
                ConnectionHexColor = value.StrokeColor.ToString();

                foreach (var n in Nodes) n.IsSelected = false;
                SelectedNode = null;
                IsCanvasSelected = false;
            }
            CommandManager.InvalidateRequerySuggested();
        }

        partial void OnIsCanvasSelectedChanged(bool value)
        {
            if (value)
            {
                SelectedNode = null;
                SelectedConnection = null;
                foreach (var n in Nodes) n.IsSelected = false;
                foreach (var c in Connections) c.IsSelected = false;
            }
        }

        partial void OnTitleChanged(string value)
        {
            if (!_isLoading) QueueAutoSave();
        }

        [RelayCommand]
        private void AddNode()
        {
            RecordHistory(); // Record BEFORE change
            var baseColor = (Color)ColorConverter.ConvertFromString(_palette[Random.Shared.Next(_palette.Length)])!;
            double startX, startY;

            if (SelectedNode != null)
            {
                startX = SelectedNode.X + SelectedNode.Width + 50;
                startY = SelectedNode.Y + 50;
            }
            else if (GetViewportCenterHelper != null)
            {
                var center = GetViewportCenterHelper.Invoke();
                startX = center.X - 80;
                startY = center.Y - 30;
            }
            else
            {
                startX = FixedCanvasWidth / 2;
                startY = FixedCanvasHeight / 2;
            }

            double finalX = Math.Clamp(startX + Random.Shared.Next(-20, 20), 0, FixedCanvasWidth - 160);
            double finalY = Math.Clamp(startY + Random.Shared.Next(-20, 20), 0, FixedCanvasHeight - 60);

            string newTitle = GetNextNodeTitle("Ý tưởng");

            var vm = new NodeViewModel(new NodeModel
            {
                Title = newTitle,
                ContentXaml = CreateDefaultXaml(newTitle),
                X = finalX,
                Y = finalY,
                BackgroundColor = baseColor,
                BorderColor = Darken(baseColor),
                TextColor = Colors.Black
            });
            Nodes.Add(vm);
            SelectedNode = vm;
        }


        [RelayCommand]
        private void AddChildNode()
        {
            var parentNode = SelectedNode;
            if (parentNode == null) return;

            RecordHistory(); // Record BEFORE change

            int childCount = Nodes.Count(n => n.Parent == parentNode);
            double childX = parentNode.X + parentNode.Width + 80;
            double childY = parentNode.Y + (childCount * 80);
            if (childCount > 0)
            {
                var lastChild = Nodes.Where(n => n.Parent == parentNode).LastOrDefault();
                if (lastChild != null) childY = lastChild.Y + lastChild.Height + 20;
            }
            childX = Math.Clamp(childX, 0, FixedCanvasWidth - 160);
            childY = Math.Clamp(childY, 0, FixedCanvasHeight - 60);

            var childColor = (Color)ColorConverter.ConvertFromString(_palette[Random.Shared.Next(_palette.Length)])!;

            string nextTitle = GetNextNodeTitle("Nhánh con");

            var childNodeModel = new NodeModel
            {
                Title = nextTitle,
                ContentXaml = CreateDefaultXaml(nextTitle),
                X = childX,
                Y = childY,
                BackgroundColor = childColor,
                BorderColor = Darken(childColor),
                Width = 160,
                Height = 60,
                FontSize = 14,
                TextColor = Colors.Black
            };

            var childViewModel = new NodeViewModel(childNodeModel);
            childViewModel.Parent = parentNode;
            Nodes.Add(childViewModel);

            var connectionModel = new ConnectionModel
            {
                SourceId = parentNode.Id,
                TargetId = childViewModel.Id,
                StrokeColor = (Color)ColorConverter.ConvertFromString("#FF4E89AE"),
                Thickness = 3,
                ArrowStyle = "Arrow"
            };

            var connectionVM = new ConnectionViewModel(connectionModel);
            connectionVM.Source = parentNode;
            connectionVM.Target = childViewModel;
            Connections.Add(connectionVM);
            QueueAutoSave();

            SelectedNode = childViewModel;
        }

        [RelayCommand]
        private void DeleteNode()
        {
            if (SelectedNode == null) return;
            if (SelectedNode.ToModel().IsRoot)
            {
                StatusMessage = "Không thể xóa chủ đề chính!";
                return;
            }

            RecordHistory(); // Record BEFORE change

            var node = SelectedNode;
            Nodes.Remove(node);
            var rms = Connections.Where(c => c.SourceId == node.Id || c.TargetId == node.Id).ToList();
            foreach (var c in rms) Connections.Remove(c);
            SelectedNode = null;
        }

        [RelayCommand]
        private void DeleteConnection()
        {
            if (SelectedConnection != null)
            {
                RecordHistory(); // Record BEFORE change
                Connections.Remove(SelectedConnection);
                SelectedConnection = null;
                QueueAutoSave();
            }
        }

        [RelayCommand]
        private void ClearAll()
        {
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ Node và Liên kết không?", "Xác nhận xóa tất cả", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                RecordHistory(); // Record BEFORE change
                Connections.Clear();
                Nodes.Clear();
                SelectedNode = null;
                SelectedConnection = null;
                _pendingConnectionNode = null;
                _pendingDisconnectNode = null;
                StatusMessage = "Đã làm mới trang vẽ";
                ZoomLevel = 1.0;
                CreateCentralNode();
                QueueAutoSave();
            }
        }

        [RelayCommand]
        private void SelectConnection(ConnectionViewModel? conn)
        {
            if (conn == null) return;
            foreach (var n in Nodes) n.IsSelected = false;
            foreach (var c in Connections) c.IsSelected = false;

            conn.IsSelected = true;
            SelectedConnection = conn;
            IsCanvasSelected = false;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            IsCanvasSelected = true;
        }

        [RelayCommand]
        private void TogglePresentation() => IsPresentationMode = !IsPresentationMode;

        [RelayCommand]
        private void ZoomIn() => ZoomLevel += 0.1;

        [RelayCommand]
        private void ZoomOut() => ZoomLevel -= 0.1;

        [RelayCommand]
        private void Search() => PerformSearch();

        [RelayCommand]
        private void StartConnection(NodeViewModel? node)
        {
            if (node == null) return;
            _pendingConnectionNode = node;
            StatusMessage = $"Chọn node để kết nối từ '{node.Title}'";
        }

        [RelayCommand]
        private void CompleteConnection(NodeViewModel? node)
        {
            if (node == null) { StatusMessage = ""; _pendingConnectionNode = null; _pendingDisconnectNode = null; return; }

            if (_pendingConnectionNode != null)
            {
                if (node == _pendingConnectionNode) { _pendingConnectionNode = null; return; }

                RecordHistory(); // Record BEFORE change

                NodeViewModel source = _pendingConnectionNode;
                NodeViewModel target = node;
                bool swap = false;

                if (target.ToModel().IsRoot) swap = true;
                else if (!source.ToModel().IsRoot && (target.Width * target.Height > source.Width * source.Height)) swap = true;

                if (swap) { var t = source; source = target; target = t; }

                var connection = new ConnectionModel
                {
                    SourceId = source.Id,
                    TargetId = target.Id,
                    StrokeColor = (Color)ColorConverter.ConvertFromString("#FF4E89AE"),
                    Thickness = 3,
                    ArrowStyle = "Arrow"
                };
                var vm = new ConnectionViewModel(connection) { Source = source, Target = target };
                target.Parent = source;
                Connections.Add(vm);
                StatusMessage = "Đã tạo liên kết";
                _pendingConnectionNode = null;
                return;
            }

            if (_pendingDisconnectNode != null)
            {
                if (node == _pendingDisconnectNode) { _pendingDisconnectNode = null; return; }

                RecordHistory(); // Record BEFORE change

                var rm = Connections.Where(c => (c.SourceId == _pendingDisconnectNode.Id && c.TargetId == node.Id) || (c.SourceId == node.Id && c.TargetId == _pendingDisconnectNode.Id)).ToList();
                foreach (var c in rm) Connections.Remove(c);
                StatusMessage = rm.Count > 0 ? "Đã hủy liên kết" : "Không có liên kết";
                _pendingDisconnectNode = null;
            }
        }

        [RelayCommand]
        private void ClearConnection()
        {
            if (_pendingConnectionNode != null || _pendingDisconnectNode != null) { _pendingConnectionNode = null; _pendingDisconnectNode = null; StatusMessage = ""; }
            else if (SelectedConnection != null) { DeleteConnection(); StatusMessage = "Đã xóa liên kết"; }
            else if (SelectedNode != null) { _pendingDisconnectNode = SelectedNode; StatusMessage = $"Chọn node để hủy kết nối với '{SelectedNode.Title}'"; }
        }

        [RelayCommand]
        private async Task GenerateByAi(object? parameter)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Đang gọi AI...";
                var topic = parameter as string ?? Title;
                if (string.IsNullOrWhiteSpace(App.GoogleAiApiKey)) { StatusMessage = "Chưa cấu hình API Key"; return; }

                RecordHistory(); // Record BEFORE change

                var document = await _aiService.GenerateMindmapAsync(topic, App.GoogleAiApiKey);
                if (document == null) { StatusMessage = "AI không trả về mindmap phù hợp"; return; }
                document.OwnerId = _currentUser.Id;
                if (_currentDocumentId != Guid.Empty) document.Id = _currentDocumentId;
                LoadMindmap(document, keepHistory: true);
                await FlushAutoSaveAsync();
                StatusMessage = "Đã tạo mindmap từ AI";
            }
            catch (Exception ex) { StatusMessage = ex.Message; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void ApplyColor(object? parameter)
        {
            Color? color = ParseColor(parameter);
            if (!color.HasValue) return;

            RecordHistory(); // Record BEFORE change

            if (SelectedNode != null) { SelectedNode.BackgroundColor = color.Value; SelectedNode.BorderColor = Darken(color.Value); }
            else if (SelectedConnection != null) { SelectedConnection.StrokeColor = color.Value; }
        }

        [RelayCommand]
        private void ApplyTextColor(object? parameter)
        {
            if (SelectedNode == null) return;

            RecordHistory(); // Record BEFORE change

            Color? color = ParseColor(parameter);
            if (color.HasValue) SelectedNode.TextColor = color.Value;
        }

        [RelayCommand]
        private void ToggleTheme() { if (Application.Current is App app) app.ToggleTheme(); }

        [RelayCommand]
        private void Logout() => LogoutRequested?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        private void OpenProfile() { EditDisplayName = _currentUser.DisplayName ?? "Người dùng"; IsProfileDialogOpen = true; StatusMessage = "Đang xem cài đặt tài khoản..."; }

        [RelayCommand]
        private void CloseProfile() => IsProfileDialogOpen = false;

        private void NodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) { foreach (var n in Nodes) { n.PropertyChanged -= OnNodePropertyChanged; n.PropertyChanged += OnNodePropertyChanged; } }
            else { if (e.OldItems != null) foreach (NodeViewModel n in e.OldItems) n.PropertyChanged -= OnNodePropertyChanged; if (e.NewItems != null) foreach (NodeViewModel n in e.NewItems) n.PropertyChanged += OnNodePropertyChanged; }
            if (!_isLoading) QueueAutoSave();
        }

        private void ConnectionsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) { foreach (var c in Connections) { c.PropertyChanged -= OnConnectionPropertyChanged; c.PropertyChanged += OnConnectionPropertyChanged; } }
            else { if (e.OldItems != null) foreach (ConnectionViewModel c in e.OldItems) c.PropertyChanged -= OnConnectionPropertyChanged; if (e.NewItems != null) foreach (ConnectionViewModel c in e.NewItems) c.PropertyChanged += OnConnectionPropertyChanged; }
            if (!_isLoading) QueueAutoSave();
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e) { if (!_isLoading && e.PropertyName != nameof(NodeViewModel.IsSelected)) QueueAutoSave(); }
        private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (!_isLoading && e.PropertyName != nameof(ConnectionViewModel.IsHighlighted)) QueueAutoSave(); }

        public async Task FlushAutoSaveAsync() { if (_isLoading) return; _autoSaveTimer.Stop(); await SaveMindmapAsync(); }
        private async Task AutoSaveAsync() { _autoSaveTimer.Stop(); await SaveMindmapAsync(); }
        private async Task SaveMindmapAsync()
        {
            if (_isSaving || _isLoading) return;

            try
            {
                _isSaving = true;
                var doc = BuildDocumentSnapshot();

                // 1. Kiểm tra xem Map này đã có trong Database chưa?
                var existingMap = await _storageService.GetMapAsync(doc.Id);
                bool isNewMap = (existingMap == null);

                // 2. Nếu là Map Mới -> Kiểm tra giới hạn số lượng
                if (isNewMap)
                {
                    int count = await _storageService.GetMapCountAsync(_currentUser.Id);

                    if (count >= 5)
                    {
                        // Tạm dừng AutoSave để không bị hiện popup liên tục
                        _autoSaveTimer.Stop();

                        var result = MessageBox.Show(
                            "Bạn đã đạt giới hạn lưu trữ 5 Mindmap.\n\n" +
                            "Bạn có muốn XÓA Mindmap cũ nhất để lưu bản đồ mới này không?",
                            "Cảnh báo dung lượng",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Tìm và xóa map cũ nhất
                            var oldestId = await _storageService.GetOldestMapIdAsync(_currentUser.Id);
                            if (oldestId != null)
                            {
                                await _storageService.DeleteMapAsync(oldestId.Value);
                            }

                            // Bật lại AutoSave sau khi xử lý xong
                            _autoSaveTimer.Start();
                        }
                        else
                        {
                            // Người dùng chọn No -> Hủy lưu
                            // (Map này sẽ chỉ nằm trên RAM, không xuống DB)
                            return;
                        }
                    }
                }

                // 3. Tiến hành lưu xuống DB
                await _storageService.SaveDocumentAsync(doc);
                // StatusMessage = "Đã lưu"; // (Bật dòng này nếu muốn hiện chữ Đã lưu)
            }
            catch (Exception ex)
            {
                StatusMessage = "Lỗi lưu: " + ex.Message;
            }
            finally
            {
                _isSaving = false;
            }
        }
        private void QueueAutoSave() { if (_isLoading) return; _autoSaveTimer.Stop(); _autoSaveTimer.Start(); }

        private MindmapDocument BuildDocumentSnapshot()
        {
            var d = new MindmapDocument { Id = _currentDocumentId == Guid.Empty ? Guid.NewGuid() : _currentDocumentId, OwnerId = _currentUser.Id, Title = Title, CanvasBackgroundColor = CanvasBackgroundColor, CanvasGridStyle = CanvasGridStyle };
            foreach (var n in Nodes) d.Nodes.Add(CloneNode(n.ToModel()));
            foreach (var c in Connections) d.Connections.Add(CloneConnection(c.ToModel()));
            return d;
        }

        public async Task InitializeAsync(Guid? documentId = null)
        {
            MindmapDocument doc;
            if (documentId.HasValue)
            {
                doc = await _storageService.GetMapAsync(documentId.Value);
                if (doc == null)
                {
                    // If not found, fall back to default or empty
                     doc = new MindmapDocument { OwnerId = _currentUser.Id, Title = "Mindmap không tên" };
                }
            }
            else
            {
                // New Map or Default load logic (For "New Map" from Recent page, we likely passed NULL or came here with empty. 
                // But RecentsPage passes NULL for "New Map". 
                // If NULL, create new empty map. 
                // BUT current logic was "LoadOrCreateAsync" (Load LAST map).
                // If checking "History", we want "New Map" to be NEW.
                // So if documentId is NULL, we create NEW.
                // But wait, what if existing calls expect loading last map?
                // MainWindow is ONLY called from RecentsPage now (once Login is updated).
                // So NULL means NEW MAP.
                doc = new MindmapDocument
                {
                    Id = Guid.NewGuid(),
                    OwnerId = _currentUser.Id,
                    Title = "Mindmap mới",
                    UpdatedAt = DateTime.UtcNow
                };
            }
            
            LoadMindmap(doc);
            if (Nodes.Count == 0) { CreateCentralNode(); await FlushAutoSaveAsync(); }
            else RequestCenterView?.Invoke(this, EventArgs.Empty);
        }

        public void LoadMindmap(MindmapDocument document, bool keepHistory = false)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            _isLoading = true;

            // Save selection state to restore after reload if possible
            var previousSelectedNodeId = SelectedNode?.Id;
            var previousSelectedConnId = SelectedConnection?.Id;

            if (!keepHistory) _undoRedoService.Reset();

            foreach (var node in Nodes) node.PropertyChanged -= OnNodePropertyChanged;
            foreach (var conn in Connections) conn.PropertyChanged -= OnConnectionPropertyChanged;
            Nodes.Clear(); Connections.Clear();
            _currentDocumentId = document.Id != Guid.Empty ? document.Id : Guid.NewGuid();
            Title = document.Title;
            CanvasBackgroundColor = document.CanvasBackgroundColor;
            CanvasGridStyle = string.IsNullOrWhiteSpace(document.CanvasGridStyle) ? "Light" : document.CanvasGridStyle;

            foreach (var n in document.Nodes)
            {
                var vm = new NodeViewModel(CloneNode(n));

                if (n.IsRoot)
                {
                    vm.IsDraggable = true; // [UPDATED] Cho phép di chuyển node gốc
                    vm.IsDeletable = false;
                }

                vm.PropertyChanged += OnNodePropertyChanged;
                Nodes.Add(vm);
            }
            foreach (var c in document.Connections)
            {
                var vm = new ConnectionViewModel(CloneConnection(c));
                vm.Source = Nodes.FirstOrDefault(x => x.Id == vm.SourceId);
                vm.Target = Nodes.FirstOrDefault(x => x.Id == vm.TargetId);
                if (vm.Source != null && vm.Target != null) { vm.PropertyChanged += OnConnectionPropertyChanged; Connections.Add(vm); }
            }
            foreach (var c in Connections) if (c.Source != null && c.Target != null) c.Target.Parent = c.Source;

            // Restore selection
            if (previousSelectedNodeId.HasValue) SelectedNode = Nodes.FirstOrDefault(n => n.Id == previousSelectedNodeId.Value);
            if (previousSelectedConnId.HasValue) SelectedConnection = Connections.FirstOrDefault(c => c.Id == previousSelectedConnId.Value);

            _isLoading = false;
            NotifyUndoRedoState();
        }

        public async Task SaveProfileAsync(string currentPass, string newPass, string confirmPass)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(EditDisplayName)) { MessageBox.Show("Tên hiển thị không được để trống."); return; }
                if (!string.IsNullOrEmpty(newPass))
                {
                    if (!_userService.VerifyUserPassword(currentPass, _currentUser.PasswordHash, _currentUser.PasswordSalt)) { MessageBox.Show("Mật khẩu hiện tại sai."); return; }
                    if (newPass != confirmPass) { MessageBox.Show("Mật khẩu xác nhận không khớp."); return; }
                    var (newHash, newSalt) = _userService.ComputeHash(newPass);
                    _currentUser.PasswordHash = newHash; _currentUser.PasswordSalt = newSalt;
                }
                _currentUser.DisplayName = EditDisplayName;
                if (await _userService.UpdateUserAsync(_currentUser)) { Title = $"Mindmap của {EditDisplayName}"; IsProfileDialogOpen = false; MessageBox.Show("Cập nhật thành công!"); }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void PerformSearch() { SearchResults.Clear(); foreach (var n in _searchService.SearchNodes(Nodes, SearchText)) SearchResults.Add(n); foreach (var n in Nodes) n.IsSelected = SearchResults.Contains(n); }
        private void ClearPendingConnection() { _pendingConnectionNode = null; _pendingDisconnectNode = null; StatusMessage = ""; }
        private string GetNextNodeTitle(string prefix) { int i = 1; while (Nodes.Any(n => n.Title != null && n.Title.Equals($"{prefix} {i}", StringComparison.OrdinalIgnoreCase))) i++; return $"{prefix} {i}"; }
        private string CreateDefaultXaml(string text) => $"<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph TextAlignment=\"Center\"><Run>{text}</Run></Paragraph></FlowDocument>";

        private void CreateCentralNode()
        {
            Nodes.Clear();
            Connections.Clear();
            double w = 180, h = 60;

            var root = new NodeModel
            {
                IsRoot = true,
                Id = Guid.NewGuid(),
                Title = "CHỦ ĐỀ CHÍNH",
                ContentXaml = CreateDefaultXaml("CHỦ ĐỀ CHÍNH"),
                X = (FixedCanvasWidth - w) / 2,
                Y = (FixedCanvasHeight - h) / 2,
                Width = w,
                Height = h,
                Shape = "RoundedRectangle",
                BackgroundColor = Color.FromRgb(0x0D, 0x1B, 0x2A),
                BorderColor = Color.FromRgb(0x41, 0x5A, 0x77),
                TextColor = Colors.White,
                FontSize = 18,
                FontWeight = "Bold",
                IsDraggable = true, // [UPDATED] Cho phép di chuyển node gốc
                IsDeletable = false
            };

            var vm = new NodeViewModel(root);
            Nodes.Add(vm);
            SelectedNode = vm;
            ZoomLevel = 1.0;
            RequestCenterView?.Invoke(this, EventArgs.Empty);
        }

        private Color? ParseColor(object? p) => p is Color c ? c : p is SolidColorBrush b ? b.Color : p is string s ? (Color?)ColorConverter.ConvertFromString(s) : null;
        private static Color Darken(Color c) => Color.FromArgb(c.A, (byte)(c.R * 0.8), (byte)(c.G * 0.8), (byte)(c.B * 0.8));

        // --- QUAN TRỌNG: Cập nhật hàm CloneNode để copy đủ thuộc tính mới ---
        private static NodeModel CloneNode(NodeModel m) => new NodeModel
        {
            Id = m.Id,
            IsRoot = m.IsRoot,
            Title = m.Title,
            ContentXaml = m.ContentXaml,
            X = m.X,
            Y = m.Y,
            Width = m.Width,
            Height = m.Height,
            Shape = m.Shape,
            BackgroundColor = m.BackgroundColor,
            BorderColor = m.BorderColor,
            TextColor = m.TextColor,
            BackgroundGridStyle = m.BackgroundGridStyle,
            FontSize = m.FontSize,
            FontFamily = m.FontFamily,
            IsDraggable = m.IsDraggable,
            IsDeletable = m.IsDeletable,

            // Các thuộc tính mới
            IsBold = m.IsBold,
            IsItalic = m.IsItalic,
            IsUnderline = m.IsUnderline,
            IsStrikethrough = m.IsStrikethrough,
            FontWeight = m.FontWeight,

            Tags = new ObservableCollection<string>(m.Tags)
        };

        private static ConnectionModel CloneConnection(ConnectionModel m) => new ConnectionModel { Id = m.Id, SourceId = m.SourceId, TargetId = m.TargetId, StrokeColor = m.StrokeColor, Thickness = m.Thickness, IsCurved = m.IsCurved, DashOffset = m.DashOffset, DashArray = m.DashArray != null ? new DoubleCollection(m.DashArray) : null, ArrowStyle = m.ArrowStyle };
    }
}
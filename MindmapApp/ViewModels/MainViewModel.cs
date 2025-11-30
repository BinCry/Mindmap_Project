
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
using MindmapApp.Commands;
using MindmapApp.Models;
using MindmapApp.Services;

namespace MindmapApp.ViewModels;

public class MainViewModel : BaseViewModel
{
    #region Fields (Dependencies & State)

    private readonly MindmapExportService _exportService;
    private readonly MindmapSearchService _searchService;
    private readonly MindmapAiService _aiService;
    private readonly MindmapStorageService _storageService;
    private readonly UserAccount _currentUser;
    private readonly DispatcherTimer _autoSaveTimer;

    private readonly string[] _palette =
    {
        "#E3F2FD", "#FCE4EC", "#E8F5E9", "#FFF3E0", "#F3E5F5", "#E0F7FA"
    };

    private string _title = "Mindmap của tôi";
    private NodeViewModel? _selectedNode;
    private NodeViewModel? _pendingConnectionNode;
    private bool _isPresentationMode;
    private double _zoomLevel = 1.0; // Mặc định 1.0 để tránh lỗi scale = 0
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isLoading;
    private bool _isSaving;
    private Guid _currentDocumentId;

    // Định nghĩa kích thước vùng làm việc (khớp với XAML)
    public const double CanvasWidth = 3000;
    public const double CanvasHeight = 2000;
    // Sự kiện: Nhờ View cuộn ra giữa dùng
    public event EventHandler? RequestCenterView;

    #endregion

    #region Constructor

    public MainViewModel(MindmapExportService exportService, MindmapSearchService searchService, MindmapAiService aiService, MindmapStorageService storageService, UserAccount currentUser)
    {
        _exportService = exportService;
        _searchService = searchService;
        _aiService = aiService;
        _storageService = storageService;
        _currentUser = currentUser;

        // 1. Initialize Collections
        Nodes = new ObservableCollection<NodeViewModel>();
        Connections = new ObservableCollection<ConnectionViewModel>();
        SearchResults = new ObservableCollection<NodeViewModel>();

        Nodes.CollectionChanged += NodesOnCollectionChanged;
        Connections.CollectionChanged += ConnectionsOnCollectionChanged;

        // 2. Initialize Options & Palette
        ShapeOptions = new ObservableCollection<string>(new[] { "RoundedRectangle", "Rectangle", "Ellipse", "Diamond" });
        FontOptions = new ObservableCollection<string>(new[] { "Segoe UI", "Calibri", "Arial", "Roboto", "Open Sans" });
        FontSizeOptions = new ObservableCollection<double>(new[] { 12d, 14d, 16d, 18d, 20d, 24d, 28d });
        ColorPalette = new ObservableCollection<Color>(new[]
        {
            (Color)ColorConverter.ConvertFromString("#FF4E89AE")!,
            (Color)ColorConverter.ConvertFromString("#FF42A5F5")!,
            (Color)ColorConverter.ConvertFromString("#FFF9A620")!,
            (Color)ColorConverter.ConvertFromString("#FF81C784")!,
            (Color)ColorConverter.ConvertFromString("#FFBA68C8")!,
            (Color)ColorConverter.ConvertFromString("#FFFF8A65")!,
            (Color)ColorConverter.ConvertFromString("#FF4DD0E1")!
        });

        // 3. Initialize Commands
        InitializeCommands();

        // 4. Auto Save Timer
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _autoSaveTimer.Tick += async (_, _) => await AutoSaveAsync();
    }

    private void InitializeCommands()
    {
        AddNodeCommand = new RelayCommand(_ => AddNode());
        DeleteNodeCommand = new RelayCommand(
            _ => DeleteSelectedNode(),
            _ => SelectedNode != null && SelectedNode.IsDeletable
        ); 
        TogglePresentationCommand = new RelayCommand(_ => IsPresentationMode = !IsPresentationMode);
        StartConnectionCommand = new RelayCommand(node => StartConnection(node as NodeViewModel));
        CompleteConnectionCommand = new RelayCommand(node => CompleteConnection(node as NodeViewModel));
        ClearConnectionCommand = new RelayCommand(_ => ClearPendingConnection());
        ZoomInCommand = new RelayCommand(_ => ZoomLevel += 0.1);
        ZoomOutCommand = new RelayCommand(_ => ZoomLevel = Math.Max(0.2, ZoomLevel - 0.1));
        SearchCommand = new RelayCommand(_ => PerformSearch());
        ExportImageCommand = new AsyncRelayCommand(ExecuteExportImageAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExecuteExportPdfAsync);
        GenerateByAiCommand = new AsyncRelayCommand(ExecuteGenerateByAiAsync, _ => !IsBusy);
        ApplyColorCommand = new RelayCommand(ApplyNodeColor, _ => SelectedNode != null);
        ClearAllCommand = new RelayCommand(_ => ClearAllNodes(), _ => Nodes.Count > 0);
        // Chỉ cho phép thêm con khi ĐANG CHỌN một node (SelectedNode != null)
        AddChildNodeCommand = new RelayCommand(_ => AddChildNode(), _ => SelectedNode != null);
    }

    #endregion

    #region Properties (Binding Sources)
   
    public ObservableCollection<NodeViewModel> Nodes { get; }
    public ObservableCollection<ConnectionViewModel> Connections { get; }
    public ObservableCollection<NodeViewModel> SearchResults { get; }

    public ObservableCollection<string> ShapeOptions { get; }
    public ObservableCollection<string> FontOptions { get; }
    public ObservableCollection<double> FontSizeOptions { get; }
    public ObservableCollection<Color> ColorPalette { get; }

  

    public string Title
    {
        get => _title;
        set { if (SetProperty(ref _title, value) && !_isLoading) QueueAutoSave(); }
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            // 1. Nếu đang có node cũ được chọn -> Tắt trạng thái chọn của nó đi (để nó hết sáng)
            if (_selectedNode != null)
            {
                _selectedNode.IsSelected = false;
            }

            // 2. Gán giá trị mới cho biến _selectedNode
            if (SetProperty(ref _selectedNode, value))
            {
                // 3. Nếu node mới không phải null -> Bật trạng thái chọn lên (để nó phát sáng)
                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = true;
                }

                // Cập nhật lại trạng thái các nút bấm (VD: nút Xóa sẽ sáng lên)
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    public bool IsPresentationMode
    {
        get => _isPresentationMode;
        set => SetProperty(ref _isPresentationMode, value);
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetProperty(ref _zoomLevel, Math.Max(0.2, Math.Min(3.0, value)));
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                (GenerateByAiCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region Commands

    public ICommand AddNodeCommand { get; private set; }
    public ICommand DeleteNodeCommand { get; private set; }
    public ICommand TogglePresentationCommand { get; private set; }
    public ICommand StartConnectionCommand { get; private set; }
    public ICommand CompleteConnectionCommand { get; private set; }
    public ICommand ClearConnectionCommand { get; private set; }
    public ICommand ZoomInCommand { get; private set; }
    public ICommand ZoomOutCommand { get; private set; }
    public ICommand SearchCommand { get; private set; }
    public ICommand ExportImageCommand { get; private set; }
    public ICommand ExportPdfCommand { get; private set; }
    public ICommand GenerateByAiCommand { get; private set; }

    public ICommand AddChildNodeCommand { get; private set; }
    public ICommand ApplyColorCommand { get; private set; }

    public ICommand ClearAllCommand { get; private set; }
    public event EventHandler? RequestExportImage;
    public event EventHandler? RequestExportPdf;

    #endregion

    #region Main Logic (Mindmap Operations)

    private void CreateCentralNode()
    {
        Nodes.Clear();
        Connections.Clear();

        double nodeWidth = 180;
        double nodeHeight = 60;

        // TÍNH TOÁN VỊ TRÍ GIỮA CANVAS
        // Công thức: (Rộng Canvas / 2) - (Rộng Node / 2)
        double centerX = (CanvasWidth / 2) - (nodeWidth / 2);
        double centerY = (CanvasHeight / 2) - (nodeHeight / 2);

        var rootNode = new NodeModel
        {
            IsRoot = true,
            Id = Guid.NewGuid(),
            Title = "Central Topic",
            Description = "Nhập ý tưởng chính",
            X = centerX,
            Y = centerY,
            Width = nodeWidth,
            Height = nodeHeight,
            Shape = "RoundedRectangle",
            BackgroundColor = Color.FromRgb(0x0D, 0x1B, 0x2A),
            BorderColor = Color.FromRgb(0x41, 0x5A, 0x77),
            TextColor = Colors.White,
            FontSize = 18,
            FontWeight = "Bold",
            // Không được di chuyển !!!
            IsDraggable = false,
            // Không được xóa node gốc: 
            IsDeletable = false
        }; 

        var rootVM = new NodeViewModel(rootNode);
        Nodes.Add(rootVM);
        SelectedNode = rootVM;

        // YÊU CẦU VIEW CUỘN RA GIỮA
        RequestCenterView?.Invoke(this, EventArgs.Empty);
    }


    private void AddChildNode()
    {
        // 1. Lấy Node cha (Node đang được chọn)
        var parentNode = SelectedNode;
        if (parentNode == null) return;

        // --- [SỬA ĐOẠN TÍNH TOÁN VỊ TRÍ] ---

        // Đếm xem cha này hiện tại đã có bao nhiêu con rồi
        // (Để con thứ 2 nằm dưới con thứ 1, con thứ 3 nằm dưới con thứ 2...)
        int childCount = Nodes.Count(n => n.Parent == parentNode);

        // Khoảng cách: X cách 200, Y cách nhau 80 (Chiều cao 60 + 20 hở)
        double childX = parentNode.X + parentNode.Width + 50;
        double childY = parentNode.Y + (childCount * 80);

        // ------------------------------------

        // 3. Tạo màu sắc (Lấy cùng tông màu cha hoặc random tùy bạn)
        var childColor = (Color)ColorConverter.ConvertFromString(_palette[Random.Shared.Next(_palette.Length)])!;

        // 4. Tạo Node Con
        var childNodeModel = new NodeModel
        {
            Title = $"Nhánh con {Nodes.Count + 1}", // Đánh số theo tổng số node để không trùng
            X = childX,
            Y = childY,
            BackgroundColor = childColor,
            BorderColor = Darken(childColor),
            Width = 160,
            Height = 60,
            FontSize = 14
        };

        var childViewModel = new NodeViewModel(childNodeModel);

        // Thiết lập quan hệ logic
        childViewModel.Parent = parentNode;

        // Thêm Node con vào danh sách
        Nodes.Add(childViewModel);

        // 5. Tạo Dây kết nối
        var connectionModel = new ConnectionModel
        {
            SourceId = childViewModel.Id,
            TargetId = parentNode.Id,
            StrokeColor = Colors.Gray,
            Thickness = 2
        };

        var connectionVM = new ConnectionViewModel(connectionModel);

        // Gán tham chiếu để vẽ hình
        connectionVM.Source = childViewModel;
        connectionVM.Target = parentNode;

        Connections.Add(connectionVM);

        // 6. Lưu lại
        QueueAutoSave();
    }
    public async Task InitializeAsync()
    {
        var defaultTitle = $"Mindmap của {(_currentUser.DisplayName ?? _currentUser.Email)}";
        var document = await _storageService.LoadOrCreateAsync(_currentUser.Id, defaultTitle);
        LoadMindmap(document);

        if (Nodes.Count == 0)
        {
            CreateCentralNode(); 
            await FlushAutoSaveAsync();
        }
    }

    public void LoadMindmap(MindmapDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));

        _isLoading = true;

        // Unsubscribe old events
        foreach (var node in Nodes) node.PropertyChanged -= OnNodePropertyChanged;
        foreach (var conn in Connections) conn.PropertyChanged -= OnConnectionPropertyChanged;

        Nodes.Clear();
        Connections.Clear();

        _currentDocumentId = document.Id != Guid.Empty ? document.Id : (_currentDocumentId != Guid.Empty ? _currentDocumentId : Guid.NewGuid());
        Title = document.Title;

        foreach (var rawNode in document.Nodes)
        {
            // 1. Clone ra một bản sao mới để không ảnh hưởng dữ liệu gốc ngay lập tức
            var nodeModel = CloneNode(rawNode);

            // 2. Logic xác định Node Gốc (Ưu tiên IsRoot, dự phòng bằng Title)
            bool isRootNode = nodeModel.IsRoot;

            // [Cơ chế Tự sửa lỗi]: Nếu dữ liệu cũ chưa có IsRoot nhưng tên là "Central Topic"
            // -> Tự động gán lại thành Root để lần sau load lên sẽ chuẩn.
            if (!isRootNode && nodeModel.Title == "Central Topic")
            {
                isRootNode = true;
                nodeModel.IsRoot = true; // Cập nhật lại vào model
            }

            // 3. Nếu là Node Gốc -> Cưỡng chế Khóa vị trí và Khóa xóa
            if (isRootNode)
            {
                nodeModel.IsDraggable = false;
                nodeModel.IsDeletable = false;
            }
            else
            {
                // Đảm bảo các node thường luôn được phép thao tác (đề phòng dữ liệu lỗi)
                nodeModel.IsDraggable = true;
                nodeModel.IsDeletable = true;
            }

            // 4. Tạo ViewModel và thêm vào danh sách
            Nodes.Add(new NodeViewModel(nodeModel));
        }

        foreach (var connectionData in document.Connections)
        {
            var connectionVM = new ConnectionViewModel(CloneConnection(connectionData));

            // 1. QUAN TRỌNG: Tìm và gán Node thực tế vào Connection
            // Bước này giúp dây nối biết nó đang nối vào ai để vẽ hình và bám dính
            connectionVM.Source = Nodes.FirstOrDefault(n => n.Id == connectionVM.SourceId);
            connectionVM.Target = Nodes.FirstOrDefault(n => n.Id == connectionVM.TargetId);

            // Chỉ thêm vào danh sách nếu tìm thấy đủ 2 đầu mút (tránh lỗi dữ liệu rác)
            if (connectionVM.Source != null && connectionVM.Target != null)
            {
                Connections.Add(connectionVM);
            }
        }

        // 2. THIẾT LẬP QUAN HỆ CHA - CON (Logic mới bạn yêu cầu)
        // Duyệt lại danh sách dây nối đã tạo để gán Parent cho Node con
        foreach (var conn in Connections)
        {
            // Logic: Mũi tên đi từ Nguồn (Cha) -> Đích (Con)
            // conn.Source đã được gán ở bước 1, nên chắc chắn không null
            // conn.Target đã được gán ở bước 1

            if (conn.Source != null && conn.Target != null)
            {
                conn.Target.Parent = conn.Source;
            }
        }
        _isLoading = false;
    }

    private void AddNode()
    {
        var baseColor = (Color)ColorConverter.ConvertFromString(_palette[Random.Shared.Next(_palette.Length)])!;
        var node = new NodeModel
        {
            Title = $"Ý tưởng {Nodes.Count + 1}",
            X = 100 + Nodes.Count * 60,
            Y = 100 + Nodes.Count * 40,
            BackgroundColor = baseColor,
            BorderColor = Darken(baseColor)
        };

        var viewModel = new NodeViewModel(node);
        Nodes.Add(viewModel);
        SelectedNode = viewModel;
    }

    private void DeleteSelectedNode()
    {
        if (SelectedNode == null) return;

        var node = SelectedNode;
        Nodes.Remove(node);

        var connectionsToRemove = Connections.Where(c => c.SourceId == node.Id || c.TargetId == node.Id).ToList();
        foreach (var connection in connectionsToRemove)
        {
            Connections.Remove(connection);
        }

        SelectedNode = null;
    }

    private void StartConnection(NodeViewModel? node)
    {
        if (node == null) return;
        _pendingConnectionNode = node;
        StatusMessage = $"Chọn node để kết nối từ '{node.Title}'";
    }

    private void CompleteConnection(NodeViewModel? node)
    {
        if (node == null || _pendingConnectionNode == null || node == _pendingConnectionNode)
        {
            StatusMessage = string.Empty;
            _pendingConnectionNode = null;
            return;
        }

        var connection = new ConnectionModel
        {
            SourceId = _pendingConnectionNode.Id,
            TargetId = node.Id,
            StrokeColor = Colors.SlateGray,
            Thickness = 2
        };

        Connections.Add(new ConnectionViewModel(connection));
        StatusMessage = "Đã tạo liên kết";
        _pendingConnectionNode = null;
    }

    private void ClearPendingConnection()
    {
        _pendingConnectionNode = null;
        StatusMessage = string.Empty;
    }

    private void PerformSearch()
    {
        SearchResults.Clear();
        foreach (var node in _searchService.SearchNodes(Nodes, SearchText))
        {
            SearchResults.Add(node);
        }

        foreach (var node in Nodes)
        {
            node.IsSelected = SearchResults.Contains(node);
        }
    }

    private void ApplyNodeColor(object? parameter)
    {
        if (SelectedNode == null) return;

        Color? color = parameter switch
        {
            Color c => c,
            SolidColorBrush brush => brush.Color,
            string hex when !string.IsNullOrWhiteSpace(hex) => (Color?)ColorConverter.ConvertFromString(hex),
            _ => null
        };

        if (color.HasValue)
        {
            SelectedNode.BackgroundColor = color.Value;
            SelectedNode.BorderColor = Darken(color.Value);
        }
    }

    private static Color Darken(Color color)
    {
        byte Clamp(double value) => (byte)Math.Max(0, Math.Min(255, value));
        return Color.FromArgb(color.A,
            Clamp(color.R * 0.8),
            Clamp(color.G * 0.8),
            Clamp(color.B * 0.8));
    }

    private void ClearAllNodes()
    {
        // 1. Hỏi xác nhận người dùng trước khi xóa
        var result = MessageBox.Show(
            "Bạn có chắc chắn muốn xóa toàn bộ Node và Liên kết không?\nHành động này không thể hoàn tác.",
            "Xác nhận xóa tất cả",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // 2. Xóa dữ liệu
            Connections.Clear(); // Xóa dây trước
            Nodes.Clear();       // Xóa node sau

            // 3. Reset các trạng thái
            SelectedNode = null;
            _pendingConnectionNode = null;
            StatusMessage = "Đã làm mới trang vẽ";

            // 4. Lưu lại trạng thái trống (nếu muốn auto save ngay lập tức)
            QueueAutoSave();
        }
    }

    #endregion

    #region Export & AI Logic

    private Task ExecuteExportImageAsync(object? parameter)
    {
        RequestExportImage?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private Task ExecuteExportPdfAsync(object? parameter)
    {
        RequestExportPdf?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task ExportToImageAsync(FrameworkElement surface, string filePath)
        => _exportService.SaveAsImageAsync(surface, filePath);

    public Task ExportToPdfAsync(FrameworkElement surface, string filePath)
        => _exportService.SaveAsPdfAsync(surface, filePath);

    private async Task ExecuteGenerateByAiAsync(object? parameter)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Đang gọi AI để xây dựng mindmap...";
            var topic = parameter as string ?? Title;

            if (string.IsNullOrWhiteSpace(App.GoogleAiApiKey))
            {
                StatusMessage = "Chưa cấu hình Google AI API Key";
                return;
            }

            var document = await _aiService.GenerateMindmapAsync(topic, App.GoogleAiApiKey);
            if (document == null)
            {
                StatusMessage = "AI không trả về mindmap phù hợp";
                return;
            }

            document.OwnerId = _currentUser.Id;
            if (_currentDocumentId != Guid.Empty)
            {
                document.Id = _currentDocumentId;
            }
            LoadMindmap(document);
            await FlushAutoSaveAsync();
            StatusMessage = "Đã tạo mindmap từ AI";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Persistence (Auto Save)

    public async Task FlushAutoSaveAsync()
    {
        if (_isLoading) return;
        _autoSaveTimer.Stop();
        await SaveMindmapAsync();
    }

    private async Task AutoSaveAsync()
    {
        _autoSaveTimer.Stop();
        await SaveMindmapAsync();
    }

    private async Task SaveMindmapAsync()
    {
        if (_isSaving || _isLoading) return;

        try
        {
            _isSaving = true;
            var document = BuildDocumentSnapshot();
            await _storageService.SaveDocumentAsync(document);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể lưu mindmap: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private MindmapDocument BuildDocumentSnapshot()
    {
        var document = new MindmapDocument
        {
            Id = _currentDocumentId == Guid.Empty ? Guid.NewGuid() : _currentDocumentId,
            OwnerId = _currentUser.Id,
            Title = Title
        };

        foreach (var node in Nodes)
            document.Nodes.Add(CloneNode(node.ToModel()));

        foreach (var connection in Connections)
            document.Connections.Add(CloneConnection(connection.ToModel()));

        _currentDocumentId = document.Id;
        return document;
    }

    // --- Change Tracking Helpers ---

    private void NodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var node in Nodes)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                node.PropertyChanged += OnNodePropertyChanged;
            }
        }
        else
        {
            if (e.OldItems != null)
                foreach (NodeViewModel node in e.OldItems)
                    node.PropertyChanged -= OnNodePropertyChanged;

            if (e.NewItems != null)
                foreach (NodeViewModel node in e.NewItems)
                    node.PropertyChanged += OnNodePropertyChanged;
        }

        if (!_isLoading) QueueAutoSave();
    }

    private void ConnectionsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Logic tương tự NodesOnCollectionChanged nhưng cho Connections
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var conn in Connections)
            {
                conn.PropertyChanged -= OnConnectionPropertyChanged;
                conn.PropertyChanged += OnConnectionPropertyChanged;
            }
        }
        else
        {
            if (e.OldItems != null)
                foreach (ConnectionViewModel conn in e.OldItems)
                    conn.PropertyChanged -= OnConnectionPropertyChanged;

            if (e.NewItems != null)
                foreach (ConnectionViewModel conn in e.NewItems)
                    conn.PropertyChanged += OnConnectionPropertyChanged;
        }

        if (!_isLoading) QueueAutoSave();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading) return;
        // Không cần AutoSave khi chỉ click chọn node
        if (string.Equals(e.PropertyName, nameof(NodeViewModel.IsSelected), StringComparison.Ordinal)) return;
        QueueAutoSave();
    }

    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading) return;
        if (string.Equals(e.PropertyName, nameof(ConnectionViewModel.IsHighlighted), StringComparison.Ordinal)) return;
        QueueAutoSave();
    }

    private void QueueAutoSave()
    {
        if (_isLoading) return;
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    // --- Cloning Helpers ---

    private static NodeModel CloneNode(NodeModel model)
    {
        return new NodeModel
        {
            Id = model.Id,
            Title = model.Title,
            Description = model.Description,
            X = model.X,
            Y = model.Y,
            Width = model.Width,
            Height = model.Height,
            Shape = model.Shape,
            BackgroundColor = model.BackgroundColor,
            BorderColor = model.BorderColor,
            TextColor = model.TextColor,
            FontSize = model.FontSize,
            FontFamily = model.FontFamily,
            Tags = new ObservableCollection<string>(model.Tags)
        };
    }

    private static ConnectionModel CloneConnection(ConnectionModel model)
    {
        return new ConnectionModel
        {
            Id = model.Id,
            SourceId = model.SourceId,
            TargetId = model.TargetId,
            StrokeColor = model.StrokeColor,
            Thickness = model.Thickness,
            IsCurved = model.IsCurved,
            DashOffset = model.DashOffset,
            DashArray = model.DashArray != null ? new DoubleCollection(model.DashArray) : null
        };
    }

    #endregion
}

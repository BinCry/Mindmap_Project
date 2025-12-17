using MindmapApp.Commands;
using MindmapApp.Models;
using MindmapApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MindmapApp.ViewModels;

// [ĐÃ XÓA] Class CanvasSizeOption không còn cần thiết

public class MainViewModel : BaseViewModel
{
    #region Fields
    private readonly MindmapSearchService _searchService;
    private readonly MindmapAiService _aiService;
    private readonly MindmapStorageService _storageService;
    private readonly UserService _userService;
    private readonly UserAccount _currentUser;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly string[] _palette = { "#E3F2FD", "#FCE4EC", "#E8F5E9", "#FFF3E0", "#F3E5F5", "#E0F7FA" };

    private string _title = "Mindmap của tôi";
    private NodeViewModel? _selectedNode;
    private ConnectionViewModel? _selectedConnection;
    private NodeViewModel? _pendingConnectionNode;
    private NodeViewModel? _pendingDisconnectNode;
    private bool _isPresentationMode;
    private double _zoomLevel = 1.0;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isLoading;
    private bool _isSaving;
    private Guid _currentDocumentId;

    private bool _isProfileDialogOpen;
    private string _editDisplayName = string.Empty;

    private string _nodeHexColor;
    public string NodeHexColor
    {
        get => _nodeHexColor;
        set => SetProperty(ref _nodeHexColor, value);
    }

    private string _connectionHexColor;
    public string ConnectionHexColor
    {
        get => _connectionHexColor;
        set => SetProperty(ref _connectionHexColor, value);
    }

    public event EventHandler? RequestCenterView;
    public event EventHandler? LogoutRequested;
    #endregion

    // 1. THIẾT LẬP KÍCH THƯỚC CỐ ĐỊNH (Rất lớn để thoải mái vẽ)
    public const double FixedCanvasWidth = 8000;
    public const double FixedCanvasHeight = 6000;

    // Giữ property này để Binding ngoài XAML không bị lỗi, nhưng trả về số cố định
    public double CanvasWidth => FixedCanvasWidth;
    public double CanvasHeight => FixedCanvasHeight;

    // [ĐÃ XÓA] SizeOptions và SelectedSize

    public MainViewModel(MindmapSearchService searchService, MindmapAiService aiService, MindmapStorageService storageService, UserService userService, UserAccount currentUser)
    {
        _searchService = searchService;
        _aiService = aiService;
        _storageService = storageService;
        _userService = userService;
        _currentUser = currentUser;

        Nodes = new ObservableCollection<NodeViewModel>();
        Connections = new ObservableCollection<ConnectionViewModel>();
        SearchResults = new ObservableCollection<NodeViewModel>();

        Nodes.CollectionChanged += NodesOnCollectionChanged;
        Connections.CollectionChanged += ConnectionsOnCollectionChanged;

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

        TextColorPalette = new ObservableCollection<Color>(new[]
        {
            Colors.Black,
            Colors.White,
            (Color)ColorConverter.ConvertFromString("#FF333333"),
            (Color)ColorConverter.ConvertFromString("#FFD32F2F"),
            (Color)ColorConverter.ConvertFromString("#FF1976D2"),
            (Color)ColorConverter.ConvertFromString("#FF388E3C"),
            (Color)ColorConverter.ConvertFromString("#FF7B1FA2")
        });

        InitializeCommands();

        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _autoSaveTimer.Tick += async (_, _) => await AutoSaveAsync();

        // [ĐÃ XÓA] Dòng set SelectedSize mặc định
    }

    private void InitializeCommands()
    {
        AddNodeCommand = new RelayCommand(_ => AddNode());
        DeleteNodeCommand = new RelayCommand(_ => DeleteSelectedNode(), _ => SelectedNode != null && SelectedNode.IsDeletable);
        TogglePresentationCommand = new RelayCommand(_ => IsPresentationMode = !IsPresentationMode);
        StartConnectionCommand = new RelayCommand(node => StartConnection(node as NodeViewModel));
        CompleteConnectionCommand = new RelayCommand(node => CompleteConnection(node as NodeViewModel));

        SelectConnectionCommand = new RelayCommand(param =>
        {
            if (param is ConnectionViewModel conn)
                SelectedConnection = conn;
        });

        ClearConnectionCommand = new RelayCommand(_ =>
        {
            if (_pendingConnectionNode != null || _pendingDisconnectNode != null)
                ClearPendingConnection();
            else if (SelectedConnection != null)
            {
                DeleteSelectedConnection();
                StatusMessage = "Đã xóa liên kết";
            }
            else
                ClearPendingConnection();
        });

        ZoomInCommand = new RelayCommand(_ => ZoomLevel += 0.1);
        ZoomOutCommand = new RelayCommand(_ => ZoomLevel -= 0.1);
        SearchCommand = new RelayCommand(_ => PerformSearch());

        GenerateByAiCommand = new AsyncRelayCommand(ExecuteGenerateByAiAsync, _ => !IsBusy);

        ApplyColorCommand = new RelayCommand(ApplyColor, _ => SelectedNode != null || SelectedConnection != null);
        ApplyTextColorCommand = new RelayCommand(ApplyTextColor, _ => SelectedNode != null);

        ClearAllCommand = new RelayCommand(_ => ClearAllNodes(), _ => Nodes.Count > 0);
        AddChildNodeCommand = new RelayCommand(_ => AddChildNode(), _ => SelectedNode != null);
        DeleteConnectionCommand = new RelayCommand(_ => DeleteSelectedConnection());

        ToggleThemeCommand = new RelayCommand(_ =>
        {
            if (Application.Current is App app) app.ToggleTheme();
        });

        LogoutCommand = new RelayCommand(_ => Logout());

        OpenProfileCommand = new RelayCommand(_ => OpenProfileDialog());
        CloseProfileCommand = new RelayCommand(_ => IsProfileDialogOpen = false);
    }

    #region Properties
    public ObservableCollection<NodeViewModel> Nodes { get; }
    public ObservableCollection<ConnectionViewModel> Connections { get; }
    public ObservableCollection<NodeViewModel> SearchResults { get; }
    public ObservableCollection<string> ShapeOptions { get; }
    public ObservableCollection<string> FontOptions { get; }
    public ObservableCollection<double> FontSizeOptions { get; }
    public ObservableCollection<Color> ColorPalette { get; }
    public ObservableCollection<Color> TextColorPalette { get; }

    public bool IsProfileDialogOpen
    {
        get => _isProfileDialogOpen;
        set => SetProperty(ref _isProfileDialogOpen, value);
    }

    public string EditDisplayName
    {
        get => _editDisplayName;
        set => SetProperty(ref _editDisplayName, value);
    }

    public string Title
    {
        get => _title;
        set { if (SetProperty(ref _title, value) && !_isLoading) QueueAutoSave(); }
    }

    public ConnectionViewModel? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (_selectedConnection != null) _selectedConnection.IsSelected = false;
            if (SetProperty(ref _selectedConnection, value))
            {
                if (_selectedConnection != null)
                {
                    _selectedConnection.IsSelected = true;
                    ConnectionHexColor = _selectedConnection.StrokeColor.ToString();
                    SelectedNode = null;
                }
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(SelectedConnection));
            }
        }
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != null) _selectedNode.IsSelected = false;
            if (SetProperty(ref _selectedNode, value))
            {
                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = true;
                    NodeHexColor = _selectedNode.BackgroundColor.ToString();
                    SelectedConnection = null;
                }
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    public bool IsPresentationMode { get => _isPresentationMode; set => SetProperty(ref _isPresentationMode, value); }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetProperty(ref _zoomLevel, Math.Clamp(value, 0.2, 3.0));
    }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) (GenerateByAiCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); }
    }
    #endregion

    #region Commands Definition
    public ICommand AddNodeCommand { get; private set; }
    public ICommand DeleteNodeCommand { get; private set; }
    public ICommand TogglePresentationCommand { get; private set; }
    public ICommand StartConnectionCommand { get; private set; }
    public ICommand CompleteConnectionCommand { get; private set; }
    public ICommand ClearConnectionCommand { get; private set; }
    public ICommand ZoomInCommand { get; private set; }
    public ICommand ZoomOutCommand { get; private set; }
    public ICommand SearchCommand { get; private set; }

    public ICommand GenerateByAiCommand { get; private set; }
    public ICommand AddChildNodeCommand { get; private set; }
    public ICommand ApplyColorCommand { get; private set; }
    public ICommand ApplyTextColorCommand { get; private set; }
    public ICommand ClearAllCommand { get; private set; }
    public ICommand DeleteConnectionCommand { get; private set; }
    public ICommand ToggleThemeCommand { get; private set; }
    public ICommand LogoutCommand { get; private set; }
    public ICommand SelectConnectionCommand { get; private set; }
    public ICommand OpenProfileCommand { get; private set; }
    public ICommand CloseProfileCommand { get; private set; }
    #endregion

    #region Main Logic

    private string GetNextNodeTitle(string prefix)
    {
        int i = 1;
        while (Nodes.Any(n => n.Title != null && n.Title.Equals($"{prefix} {i}", StringComparison.OrdinalIgnoreCase)))
        {
            i++;
        }
        return $"{prefix} {i}";
    }

    private void CreateCentralNode()
    {
        Nodes.Clear();
        Connections.Clear();
        double nodeWidth = 180;
        double nodeHeight = 60;

        // CẬP NHẬT: Tính tâm dựa trên kích thước cố định
        double centerX = (FixedCanvasWidth / 2) - (nodeWidth / 2);
        double centerY = (FixedCanvasHeight / 2) - (nodeHeight / 2);

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
            IsDraggable = false,
            IsDeletable = false
        };

        var rootVM = new NodeViewModel(rootNode);
        Nodes.Add(rootVM);
        SelectedNode = rootVM;

        ZoomLevel = 1.0;
        RequestCenterView?.Invoke(this, EventArgs.Empty);
    }

    private void AddChildNode()
    {
        var parentNode = SelectedNode;
        if (parentNode == null) return;

        int childCount = Nodes.Count(n => n.Parent == parentNode);

        double childX = parentNode.X + parentNode.Width + 80;
        double childY = parentNode.Y + (childCount * 80);

        // CẬP NHẬT: Dùng FixedCanvasWidth/Height thay vì CanvasWidth/Height
        childX = Math.Clamp(childX, 0, FixedCanvasWidth - 160);
        childY = Math.Clamp(childY, 0, FixedCanvasHeight - 60);

        var childColor = (Color)ColorConverter.ConvertFromString(_palette[Random.Shared.Next(_palette.Length)])!;
        var childNodeModel = new NodeModel
        {
            Title = GetNextNodeTitle("Nhánh con"),
            X = childX,
            Y = childY,
            BackgroundColor = childColor,
            BorderColor = Darken(childColor),
            Width = 160,
            Height = 60,
            FontSize = 14
        };

        var childViewModel = new NodeViewModel(childNodeModel);
        childViewModel.Parent = parentNode;
        Nodes.Add(childViewModel);

        var connectionModel = new ConnectionModel
        {
            SourceId = childViewModel.Id,
            TargetId = parentNode.Id,
            StrokeColor = (Color)ColorConverter.ConvertFromString("#FF4E89AE"),
            Thickness = 3
        };

        var connectionVM = new ConnectionViewModel(connectionModel);
        connectionVM.Source = childViewModel;
        connectionVM.Target = parentNode;
        Connections.Add(connectionVM);
        QueueAutoSave();

        SelectedNode = childViewModel;
    }

    private void AddNode()
    {
        var baseColor = (Color)ColorConverter.ConvertFromString(_palette[Random.Shared.Next(_palette.Length)])!;
        double startX;
        double startY;

        if (SelectedNode != null)
        {
            startX = SelectedNode.X + SelectedNode.Width + 50;
            startY = SelectedNode.Y + 50;
        }
        else
        {
            var centralTopic = Nodes.FirstOrDefault(n => n.ToModel().IsRoot);
            if (centralTopic != null)
            {
                startX = centralTopic.X + centralTopic.Width + 50;
                startY = centralTopic.Y + 50;
            }
            else
            {
                // CẬP NHẬT: Dùng FixedCanvasWidth/Height
                startX = FixedCanvasWidth / 2;
                startY = FixedCanvasHeight / 2;
            }
        }

        double offsetX = Random.Shared.Next(-20, 20);
        double offsetY = Random.Shared.Next(-20, 20);

        // CẬP NHẬT: Dùng FixedCanvasWidth/Height
        double finalX = Math.Clamp(startX + offsetX, 0, FixedCanvasWidth - 160);
        double finalY = Math.Clamp(startY + offsetY, 0, FixedCanvasHeight - 60);

        var node = new NodeModel
        {
            Title = GetNextNodeTitle("Ý tưởng"),
            X = finalX,
            Y = finalY,
            BackgroundColor = baseColor,
            BorderColor = Darken(baseColor)
        };

        var viewModel = new NodeViewModel(node);
        Nodes.Add(viewModel);
        SelectedNode = viewModel;
    }

    private void OpenProfileDialog()
    {
        EditDisplayName = _currentUser.DisplayName ?? "Người dùng";
        IsProfileDialogOpen = true;
        StatusMessage = "Đang xem cài đặt tài khoản...";
    }

    public async Task SaveProfileAsync(string currentPass, string newPass, string confirmPass)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(EditDisplayName))
            {
                MessageBox.Show("Tên hiển thị không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(newPass))
            {
                if (!_userService.VerifyUserPassword(currentPass, _currentUser.PasswordHash, _currentUser.PasswordSalt))
                {
                    MessageBox.Show("Mật khẩu hiện tại không chính xác.", "Lỗi xác thực", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$";
                if (!Regex.IsMatch(newPass, pattern))
                {
                    MessageBox.Show("Mật khẩu mới không đủ mạnh!\nYêu cầu: ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.",
                                    "Mật khẩu yếu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (newPass != confirmPass)
                {
                    MessageBox.Show("Mật khẩu xác nhận không khớp.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var (newHash, newSalt) = _userService.ComputeHash(newPass);
                _currentUser.PasswordHash = newHash;
                _currentUser.PasswordSalt = newSalt;
                StatusMessage = "Đã cập nhật mật khẩu.";
            }

            _currentUser.DisplayName = EditDisplayName;
            bool success = await _userService.UpdateUserAsync(_currentUser);

            if (success)
            {
                Title = $"Mindmap của {EditDisplayName}";
                IsProfileDialogOpen = false;
                StatusMessage = "Đã cập nhật hồ sơ thành công.";
                MessageBox.Show("Cập nhật thông tin thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Không thể lưu vào cơ sở dữ liệu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cập nhật: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Logout()
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
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
        else
        {
            RequestCenterView?.Invoke(this, EventArgs.Empty);
        }
    }

    public void LoadMindmap(MindmapDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        _isLoading = true;

        foreach (var node in Nodes) node.PropertyChanged -= OnNodePropertyChanged;
        foreach (var conn in Connections) conn.PropertyChanged -= OnConnectionPropertyChanged;

        Nodes.Clear();
        Connections.Clear();

        _currentDocumentId = document.Id != Guid.Empty ? document.Id : (_currentDocumentId != Guid.Empty ? _currentDocumentId : Guid.NewGuid());
        Title = document.Title;

        foreach (var rawNode in document.Nodes)
        {
            var nodeModel = CloneNode(rawNode);
            bool isRootNode = nodeModel.IsRoot;
            if (!isRootNode && nodeModel.Title == "Central Topic")
            {
                isRootNode = true;
                nodeModel.IsRoot = true;
            }

            if (isRootNode)
            {
                nodeModel.IsDraggable = false;
                nodeModel.IsDeletable = false;
            }
            else
            {
                nodeModel.IsDraggable = true;
                nodeModel.IsDeletable = true;
            }
            Nodes.Add(new NodeViewModel(nodeModel));
        }

        foreach (var connectionData in document.Connections)
        {
            var connectionVM = new ConnectionViewModel(CloneConnection(connectionData));
            connectionVM.Source = Nodes.FirstOrDefault(n => n.Id == connectionVM.SourceId);
            connectionVM.Target = Nodes.FirstOrDefault(n => n.Id == connectionVM.TargetId);

            if (connectionVM.Source != null && connectionVM.Target != null)
            {
                Connections.Add(connectionVM);
            }
        }

        foreach (var conn in Connections)
        {
            if (conn.Source != null && conn.Target != null)
            {
                conn.Target.Parent = conn.Source;
            }
        }
        _isLoading = false;
    }

    private void DeleteSelectedNode()
    {
        if (SelectedNode == null) return;
        var node = SelectedNode;
        Nodes.Remove(node);
        var connectionsToRemove = Connections.Where(c => c.SourceId == node.Id || c.TargetId == node.Id).ToList();
        foreach (var connection in connectionsToRemove) Connections.Remove(connection);
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
        if (node == null)
        {
            StatusMessage = string.Empty;
            _pendingConnectionNode = null;
            _pendingDisconnectNode = null;
            return;
        }

        if (_pendingConnectionNode != null)
        {
            if (node == _pendingConnectionNode)
            {
                StatusMessage = string.Empty;
                _pendingConnectionNode = null;
                return;
            }

            var connection = new ConnectionModel
            {
                SourceId = _pendingConnectionNode.Id,
                TargetId = node.Id,
                StrokeColor = (Color)ColorConverter.ConvertFromString("#FF4E89AE"),
                Thickness = 3
            };

            var connectionVm = new ConnectionViewModel(connection)
            {
                Source = _pendingConnectionNode,
                Target = node
            };

            node.Parent = _pendingConnectionNode;

            Connections.Add(connectionVm);
            StatusMessage = "Đã tạo liên kết";
            _pendingConnectionNode = null;
            return;
        }

        if (_pendingDisconnectNode != null)
        {
            if (node == _pendingDisconnectNode)
            {
                StatusMessage = string.Empty;
                _pendingDisconnectNode = null;
                return;
            }

            var toRemove = Connections
                .Where(c => (c.SourceId == _pendingDisconnectNode.Id && c.TargetId == node.Id)
                         || (c.SourceId == node.Id && c.TargetId == _pendingDisconnectNode.Id))
                .ToList();

            if (toRemove.Count > 0)
            {
                foreach (var conn in toRemove)
                {
                    Connections.Remove(conn);
                }

                if (node.Parent == _pendingDisconnectNode)
                    node.Parent = null;
                if (_pendingDisconnectNode.Parent == node)
                    _pendingDisconnectNode.Parent = null;

                StatusMessage = $"Đã hủy {toRemove.Count} liên kết giữa '{_pendingDisconnectNode.Title}' và '{node.Title}'";
            }
            else
            {
                StatusMessage = $"Không tìm thấy liên kết giữa '{_pendingDisconnectNode.Title}' và '{node.Title}'";
            }

            _pendingDisconnectNode = null;
            return;
        }
    }

    private void ClearPendingConnection()
    {
        if (_pendingConnectionNode != null || _pendingDisconnectNode != null)
        {
            _pendingConnectionNode = null;
            _pendingDisconnectNode = null;
            StatusMessage = string.Empty;
            return;
        }

        if (SelectedNode != null)
        {
            _pendingDisconnectNode = SelectedNode;
            StatusMessage = $"Chọn node để hủy kết nối với '{SelectedNode.Title}'";
        }
    }

    private void PerformSearch()
    {
        SearchResults.Clear();
        foreach (var node in _searchService.SearchNodes(Nodes, SearchText)) SearchResults.Add(node);
        foreach (var node in Nodes) node.IsSelected = SearchResults.Contains(node);
    }

    private void ApplyColor(object? parameter)
    {
        Color? color = ParseColor(parameter);
        if (!color.HasValue) return;

        if (SelectedNode != null)
        {
            SelectedNode.BackgroundColor = color.Value;
            SelectedNode.BorderColor = Darken(color.Value);
        }
        else if (SelectedConnection != null)
        {
            SelectedConnection.StrokeColor = color.Value;
        }
    }

    private void ApplyTextColor(object? parameter)
    {
        if (SelectedNode == null) return;
        Color? color = ParseColor(parameter);

        if (color.HasValue)
        {
            SelectedNode.TextColor = color.Value;
        }
    }

    private Color? ParseColor(object? parameter)
    {
        return parameter switch
        {
            Color c => c,
            SolidColorBrush brush => brush.Color,
            string hex when !string.IsNullOrWhiteSpace(hex) => (Color?)ColorConverter.ConvertFromString(hex),
            _ => null
        };
    }

    private static Color Darken(Color color)
    {
        byte Clamp(double value) => (byte)Math.Max(0, Math.Min(255, value));
        return Color.FromArgb(color.A, Clamp(color.R * 0.8), Clamp(color.G * 0.8), Clamp(color.B * 0.8));
    }

    private void ClearAllNodes()
    {
        var result = MessageBox.Show(
            "Bạn có chắc chắn muốn xóa toàn bộ Node và Liên kết không?\nHành động này không thể hoàn tác.",
            "Xác nhận xóa tất cả",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
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

    private void DeleteSelectedConnection()
    {
        if (SelectedConnection != null)
        {
            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
            QueueAutoSave();
        }
    }

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
            if (_currentDocumentId != Guid.Empty) document.Id = _currentDocumentId;
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

        foreach (var node in Nodes) document.Nodes.Add(CloneNode(node.ToModel()));
        foreach (var connection in Connections) document.Connections.Add(CloneConnection(connection.ToModel()));
        _currentDocumentId = document.Id;
        return document;
    }

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
            if (e.OldItems != null) foreach (NodeViewModel node in e.OldItems) node.PropertyChanged -= OnNodePropertyChanged;
            if (e.NewItems != null) foreach (NodeViewModel node in e.NewItems) node.PropertyChanged += OnNodePropertyChanged;
        }
        if (!_isLoading) QueueAutoSave();
    }

    private void ConnectionsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
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
            if (e.OldItems != null) foreach (ConnectionViewModel conn in e.OldItems) conn.PropertyChanged -= OnConnectionPropertyChanged;
            if (e.NewItems != null) foreach (ConnectionViewModel conn in e.NewItems) conn.PropertyChanged += OnConnectionPropertyChanged;
        }
        if (!_isLoading) QueueAutoSave();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading) return;
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
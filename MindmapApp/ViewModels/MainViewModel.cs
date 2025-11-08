
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
    private double _zoomLevel = 1.0;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isLoading;
    private bool _isSaving;
    private Guid _currentDocumentId;

    public MainViewModel(MindmapExportService exportService, MindmapSearchService searchService, MindmapAiService aiService, MindmapStorageService storageService, UserAccount currentUser)
    {
        _exportService = exportService;
        _searchService = searchService;
        _aiService = aiService;
        _storageService = storageService;
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

        AddNodeCommand = new RelayCommand(_ => AddNode());
        DeleteNodeCommand = new RelayCommand(_ => DeleteSelectedNode(), _ => SelectedNode != null);
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

        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _autoSaveTimer.Tick += async (_, _) => await AutoSaveAsync();
    }

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
        set
        {
            if (SetProperty(ref _title, value) && !_isLoading)
            {
                QueueAutoSave();
            }
        }
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
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
        set => SetProperty(ref _zoomLevel, Math.Max(0.1, Math.Min(3.0, value)));
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
            {
                (GenerateByAiCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand AddNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand TogglePresentationCommand { get; }
    public ICommand StartConnectionCommand { get; }
    public ICommand CompleteConnectionCommand { get; }
    public ICommand ClearConnectionCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ExportImageCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand GenerateByAiCommand { get; }
    public ICommand ApplyColorCommand { get; }

    public event EventHandler? RequestExportImage;
    public event EventHandler? RequestExportPdf;

    public async Task InitializeAsync()
    {
        var defaultTitle = $"Mindmap của {(_currentUser.DisplayName ?? _currentUser.Email)}";
        var document = await _storageService.LoadOrCreateAsync(_currentUser.Id, defaultTitle);
        LoadMindmap(document);

        if (Nodes.Count == 0)
        {
            AddNode();
            await FlushAutoSaveAsync();
        }
    }

    public void LoadMindmap(MindmapDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _isLoading = true;

        foreach (var node in Nodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
        }

        foreach (var connection in Connections)
        {
            connection.PropertyChanged -= OnConnectionPropertyChanged;
        }

        Nodes.Clear();
        Connections.Clear();

        _currentDocumentId = document.Id != Guid.Empty ? document.Id : (_currentDocumentId != Guid.Empty ? _currentDocumentId : Guid.NewGuid());
        Title = document.Title;

        foreach (var node in document.Nodes)
        {
            Nodes.Add(new NodeViewModel(CloneNode(node)));
        }

        foreach (var connection in document.Connections)
        {
            Connections.Add(new ConnectionViewModel(CloneConnection(connection)));
        }

        _isLoading = false;
    }

    public Task ExportToImageAsync(FrameworkElement surface, string filePath)
        => _exportService.SaveAsImageAsync(surface, filePath);

    public Task ExportToPdfAsync(FrameworkElement surface, string filePath)
        => _exportService.SaveAsPdfAsync(surface, filePath);

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
        if (SelectedNode == null)
        {
            return;
        }

        var node = SelectedNode;
        Nodes.Remove(node);
        var connections = Connections.Where(c => c.SourceId == node.Id || c.TargetId == node.Id).ToList();
        foreach (var connection in connections)
        {
            Connections.Remove(connection);
        }
        SelectedNode = null;
    }

    private void StartConnection(NodeViewModel? node)
    {
        if (node == null)
        {
            return;
        }

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

    private void ApplyNodeColor(object? parameter)
    {
        if (SelectedNode == null)
        {
            return;
        }

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

    public async Task FlushAutoSaveAsync()
    {
        if (_isLoading)
        {
            return;
        }

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
        if (_isSaving || _isLoading)
        {
            return;
        }

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
        {
            document.Nodes.Add(CloneNode(node.ToModel()));
        }

        foreach (var connection in Connections)
        {
            document.Connections.Add(CloneConnection(connection.ToModel()));
        }

        _currentDocumentId = document.Id;
        return document;
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
            {
                foreach (NodeViewModel node in e.OldItems)
                {
                    node.PropertyChanged -= OnNodePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (NodeViewModel node in e.NewItems)
                {
                    node.PropertyChanged += OnNodePropertyChanged;
                }
            }
        }

        if (!_isLoading)
        {
            QueueAutoSave();
        }
    }

    private void ConnectionsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var connection in Connections)
            {
                connection.PropertyChanged -= OnConnectionPropertyChanged;
                connection.PropertyChanged += OnConnectionPropertyChanged;
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (ConnectionViewModel connection in e.OldItems)
                {
                    connection.PropertyChanged -= OnConnectionPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (ConnectionViewModel connection in e.NewItems)
                {
                    connection.PropertyChanged += OnConnectionPropertyChanged;
                }
            }
        }

        if (!_isLoading)
        {
            QueueAutoSave();
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(NodeViewModel.IsSelected), StringComparison.Ordinal))
        {
            return;
        }

        QueueAutoSave();
    }

    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(ConnectionViewModel.IsHighlighted), StringComparison.Ordinal))
        {
            return;
        }

        QueueAutoSave();
    }

    private void QueueAutoSave()
    {
        if (_isLoading)
        {
            return;
        }

        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }
}

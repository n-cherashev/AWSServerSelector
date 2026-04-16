using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Domain;

namespace PingByDaylight.Application.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServerCatalogService _serverCatalogService;
    private readonly IConnectionProbeService _probeService;
    private readonly IHostsManagementService _hostsService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ServerGroupViewModel> _serverGroups = [];

    [ObservableProperty]
    private ObservableCollection<ServerItemViewModel> _allServers = [];

    [ObservableProperty]
    private ApplyMode _applyMode = ApplyMode.Gatekeep;

    [ObservableProperty]
    private BlockMode _blockMode = BlockMode.Both;

    [ObservableProperty]
    private bool _mergeUnstable = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public string WindowTitle => "PingByDaylight";
    public string SettingsMenuText => "Настройки";
    public string AboutMenuText => "О программе";
    public string CheckUpdatesMenuText => "Проверить обновления";
    public string OpenHostsMenuText => "Открыть hosts";
    public string ConnectionInfoMenuText => "Информация о соединении";
    public string SelectServersText => "Выберите серверы";

    public MainWindowViewModel(
        IServerCatalogService serverCatalogService,
        IConnectionProbeService probeService,
        IHostsManagementService hostsService,
        ILogger<MainWindowViewModel> logger)
    {
        _serverCatalogService = serverCatalogService;
        _probeService = probeService;
        _hostsService = hostsService;
        _logger = logger;

        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var serversResult = await _serverCatalogService.GetServersAsync();
            if (serversResult.IsSuccess)
            {
                await LoadServersAsync(serversResult.Value);
            }
            else
            {
                StatusMessage = $"Ошибка загрузки серверов: {serversResult.Error}";
                _logger.LogError("Failed to load servers: {Error}", serversResult.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadServersAsync(IReadOnlyList<ServerInfo> servers)
    {
        AllServers.Clear();
        ServerGroups.Clear();

        var groups = servers.GroupBy(s => s.GroupKey).ToList();

        foreach (var group in groups)
        {
            var groupVm = new ServerGroupViewModel(group.Key, group.First().GroupDisplayName);

            foreach (var server in group)
            {
                var serverVm = new ServerItemViewModel(server, _probeService);
                AllServers.Add(serverVm);
                groupVm.Servers.Add(serverVm);
            }

            ServerGroups.Add(groupVm);
        }

        StatusMessage = SelectServersText;
    }

    [RelayCommand]
    private async Task ApplySelectionAsync()
    {
        IsBusy = true;
        try
        {
            var selectedServers = AllServers.Where(s => s.IsSelected).Select(s => s.ServerInfo.Key).ToList();

            var selection = new ServerSelection(
                "default",
                ApplyMode,
                BlockMode,
                MergeUnstable
            );

            var result = await _hostsService.ApplySelectionAsync(selection);

            if (result.IsSuccess)
            {
                StatusMessage = "Конфигурация применена успешно";
                _logger.LogInformation("Applied selection for {Count} servers", selectedServers.Count);
            }
            else
            {
                StatusMessage = $"Ошибка применения: {result.Error}";
                _logger.LogError("Failed to apply selection: {Error}", result.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetSelectionAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _hostsService.ResetToDefaultAsync();

            if (result.IsSuccess)
            {
                foreach (var server in AllServers)
                {
                    server.IsSelected = false;
                }
                StatusMessage = "Сброшено к конфигурации по умолчанию";
                _logger.LogInformation("Reset to default");
            }
            else
            {
                StatusMessage = $"Ошибка сброса: {result.Error}";
                _logger.LogError("Failed to reset: {Error}", result.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ProbeAllAsync()
    {
        IsBusy = true;
        try
        {
            var serverKeys = AllServers.Select(s => s.ServerInfo.Key);
            var results = await _probeService.ProbeAllAsync(serverKeys);

            if (results.IsSuccess)
            {
                foreach (var status in results.Value)
                {
                    var serverVm = AllServers.FirstOrDefault(s => s.ServerInfo.Key == status.ServerKey);
                    if (serverVm != null)
                    {
                        serverVm.UpdateStatus(status);
                    }
                }
                StatusMessage = "Проверка завершена";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class ServerGroupViewModel : ObservableObject
{
    public string GroupKey { get; }
    public string DisplayName { get; }

    public ObservableCollection<ServerItemViewModel> Servers { get; } = [];

    public ServerGroupViewModel(string groupKey, string displayName)
    {
        GroupKey = groupKey;
        DisplayName = displayName;
    }
}

public partial class ServerItemViewModel : ObservableObject
{
    private readonly IConnectionProbeService _probeService;

    public ServerInfo ServerInfo { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _latencyMs;

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Unknown;

    [ObservableProperty]
    private DateTime _lastChecked;

    [ObservableProperty]
    private int _jitterMs;

    [ObservableProperty]
    private double _packetLossPercent;

    public string DisplayLatency => ConnectionState switch
    {
        ConnectionState.Connected => LatencyMs < 50
            ? $"{LatencyMs}ms"
            : $"{LatencyMs}ms ±{JitterMs}ms",
        ConnectionState.Timeout => "Timeout",
        ConnectionState.Error => "Error",
        _ => "---"
    };

    public string DisplayPacketLoss => PacketLossPercent > 0
        ? $"{PacketLossPercent:F1}% loss"
        : string.Empty;

    public string StatusColor => ConnectionState switch
    {
        ConnectionState.Connected when LatencyMs < 50 && JitterMs < 10 => "#FF28A745",
        ConnectionState.Connected when LatencyMs < 100 && JitterMs < 30 => "#FFFFC107",
        ConnectionState.Connected => "#FFDC143C",
        ConnectionState.Timeout or ConnectionState.Error => "#FF666666",
        _ => "#FFB0B0B0"
    };

    public string QualityBadge => ConnectionState switch
    {
        ConnectionState.Connected when LatencyMs < 50 && JitterMs < 10 => "Excellent",
        ConnectionState.Connected when LatencyMs < 100 && JitterMs < 30 => "Good",
        ConnectionState.Connected when LatencyMs < 200 => "Fair",
        ConnectionState.Connected => "Poor",
        ConnectionState.Timeout => "Timeout",
        ConnectionState.Error => "Error",
        _ => "Unknown"
    };

    public ServerItemViewModel(ServerInfo serverInfo, IConnectionProbeService probeService)
    {
        ServerInfo = serverInfo;
        _probeService = probeService;
    }

    [RelayCommand]
    private async Task ProbeAsync()
    {
        var result = await _probeService.ProbeAsync(ServerInfo.Key);
        if (result.IsSuccess)
        {
            UpdateStatus(result.Value);
        }
    }

    public void UpdateStatus(ConnectionStatus status)
    {
        LatencyMs = status.LatencyMs;
        ConnectionState = status.State;
        LastChecked = status.LastChecked;
        JitterMs = status.JitterMs;
        PacketLossPercent = status.PacketLossPercent;
        OnPropertyChanged(nameof(DisplayLatency));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(QualityBadge));
        OnPropertyChanged(nameof(DisplayPacketLoss));
    }
}

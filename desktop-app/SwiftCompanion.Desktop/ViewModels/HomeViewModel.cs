using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwiftCompanion.Desktop.Services;

namespace SwiftCompanion.Desktop.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly BridgeManager _bridge;
    private readonly TunnelService _tunnel;
    private readonly DispatcherTimer _timer;

    public HomeViewModel(BridgeManager bridge, TunnelService tunnel)
    {
        _bridge = bridge;
        _tunnel = tunnel;
        _bridge.StateChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(Refresh);
        _tunnel.StateChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(Refresh);
        LocalizationService.Instance.LanguageChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(Refresh);

        // Poll lightweight derived state (uptime label, etc.) every 2s.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    [ObservableProperty] private string _statusText = "Starting…";
    [ObservableProperty] private string _statusColor = "#FF8A93A6";
    [ObservableProperty] private string _localUrl = "";
    [ObservableProperty] private string _lanUrl = "";
    [ObservableProperty] private string _publicUrl = "";
    /// <summary>Public URL row is only shown while the tunnel is up.</summary>
    [ObservableProperty] private Visibility _publicUrlVisibility = Visibility.Collapsed;
    [ObservableProperty] private bool _canStart;
    [ObservableProperty] private bool _canStop;

    private void Refresh()
    {
        var loc = LocalizationService.Instance;
        StatusText = _bridge.State switch
        {
            BridgeState.Running => loc.Format("Status.Running", _bridge.Port),
            BridgeState.Starting => loc["Status.Starting"],
            BridgeState.Stopped => loc["Status.Stopped"],
            BridgeState.Faulted => loc.Format("Status.Error", _bridge.LastError ?? ""),
            _ => "Unknown"
        };
        StatusColor = _bridge.State switch
        {
            BridgeState.Running => "#FF22C55E",
            BridgeState.Faulted => "#FFEF4444",
            _ => "#FF8A93A6"
        };
        LocalUrl = _bridge.State == BridgeState.Running ? _bridge.LocalUrl : "—";
        LanUrl = _bridge.State == BridgeState.Running ? (_bridge.LanUrl ?? "—") : "—";

        // Show the live tunnel URL under the LAN address whenever the tunnel is up.
        if (_tunnel.State == TunnelState.Running && !string.IsNullOrEmpty(_tunnel.PublicUrl))
        {
            PublicUrl = _tunnel.PublicUrl!;
            PublicUrlVisibility = Visibility.Visible;
        }
        else
        {
            PublicUrl = "";
            PublicUrlVisibility = Visibility.Collapsed;
        }

        CanStart = _bridge.State is BridgeState.Stopped or BridgeState.Faulted;
        CanStop = _bridge.State == BridgeState.Running;
    }

    [RelayCommand]
    private async Task Start() => await _bridge.StartAsync();

    [RelayCommand]
    private async Task Stop() => await _bridge.StopAsync();

    [RelayCommand]
    private async Task Restart() => await _bridge.RestartAsync();
}

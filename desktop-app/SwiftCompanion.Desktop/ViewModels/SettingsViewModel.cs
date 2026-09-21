using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwiftCompanion.Desktop.Services;

namespace SwiftCompanion.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel()
    {
        var s = AppSettings.Instance;
        _vpilotPort = s.VpilotPort.ToString();
        _vpilotPath = s.VpilotExePath;
        // Reflect the actual registry state, not just the stored flag.
        _runAtStartup = StartupRegistration.IsEnabled();
        _closeBehaviorIndex = (int)s.CloseAction; // Ask=0, HideToTray=1, Exit=2

        _relayEnabled = s.RelayEnabled;
        _relayServerAddr = s.RelayServerAddr;
        _relayServerPort = s.RelayServerPort.ToString();
        _relayToken = s.RelayToken;
        _relayRemotePort = s.RelayRemotePort.ToString();
        _relayAvailable = RelayService.IsFrpcAvailable();

        App.Relay.StateChanged += (_, _) =>
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                OnPropertyChanged(nameof(RelayStatus));
                SaveCommand.NotifyCanExecuteChanged();
            });
        };

        LocalizationService.Instance.LanguageChanged += (_, _) => Hint = "";
    }

    [ObservableProperty] private string _vpilotPort;
    [ObservableProperty] private string _vpilotPath;
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private int _closeBehaviorIndex;
    [ObservableProperty] private string _hint = "";
    [ObservableProperty] private Brush _hintColor = Brushes.Transparent;

    // Relay (frp)
    [ObservableProperty] private bool _relayEnabled;
    [ObservableProperty] private string _relayServerAddr;
    [ObservableProperty] private string _relayServerPort;
    [ObservableProperty] private string _relayToken;
    [ObservableProperty] private string _relayRemotePort;
    [ObservableProperty] private bool _relayAvailable;

    private static App App => (App)System.Windows.Application.Current;

    /// <summary>Human-readable relay state for the settings page.</summary>
    public string RelayStatus => App.Relay.State switch
    {
        RelayState.Running => string.Format(
            LocalizationService.Instance["Settings.Relay.RunningFmt"],
            App.Relay.PublicEndpoint ?? ""),
        RelayState.Starting => LocalizationService.Instance["Settings.Relay.Starting"],
        RelayState.Faulted => string.Format(
            LocalizationService.Instance["Settings.Relay.ErrorFmt"],
            App.Relay.LastError ?? ""),
        _ => LocalizationService.Instance["Settings.Relay.Stopped"],
    };

    [RelayCommand]
    private void Browse()
    {
        var loc = LocalizationService.Instance;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = loc["Settings.PickVpilot"],
            Filter = "vPilot|vPilot.exe|Executable (*.exe)|*.exe",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() == true)
            VpilotPath = dlg.FileName;
    }

    [RelayCommand]
    private void Save()
    {
        var loc = LocalizationService.Instance;
        if (!int.TryParse(VpilotPort, out var port) || port < 1 || port > 65535)
        {
            SetHint(loc["Settings.PortBad"], isError: true);
            return;
        }

        // Path is optional (empty = auto-detect). If set, it must be a real vPilot.exe.
        var path = (VpilotPath ?? "").Trim();
        if (path.Length > 0 &&
            !(System.IO.File.Exists(path) &&
              string.Equals(System.IO.Path.GetFileName(path), "vPilot.exe", StringComparison.OrdinalIgnoreCase)))
        {
            SetHint(loc["Settings.PathBad"], isError: true);
            return;
        }

        var s = AppSettings.Instance;
        s.VpilotPort = port;
        s.VpilotExePath = path;
        s.Save();

        // swift 版直连 DBus，无 vPilot 插件端口概念。

        SetHint(loc["Settings.Saved"], isError: false);
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        StartupRegistration.Set(value);
        AppSettings.Instance.RunAtStartup = value;
        AppSettings.Instance.Save();
    }

    partial void OnCloseBehaviorIndexChanged(int value)
    {
        if (value < 0) return; // ComboBox clears to -1 mid-rebind; ignore.
        AppSettings.Instance.CloseAction = (CloseAction)value;
        AppSettings.Instance.Save();
    }

    /// <summary>Apply relay settings and start/stop the frp tunnel to match the toggle.</summary>
    [RelayCommand]
    private async Task ApplyRelayAsync()
    {
        var loc = LocalizationService.Instance;
        var s = AppSettings.Instance;

        if (!int.TryParse(RelayServerPort, out var serverPort) || serverPort is < 1 or > 65535 ||
            !int.TryParse(RelayRemotePort, out var remotePort) || remotePort is < 1 or > 65535)
        {
            SetHint(loc["Settings.PortBad"], isError: true);
            return;
        }

        s.RelayEnabled = RelayEnabled;
        s.RelayServerAddr = (RelayServerAddr ?? "").Trim();
        s.RelayServerPort = serverPort;
        s.RelayToken = (RelayToken ?? "").Trim();
        s.RelayRemotePort = remotePort;
        s.Save();

        if (s.RelayEnabled)
        {
            if (string.IsNullOrEmpty(s.RelayServerAddr))
            {
                SetHint(loc["Settings.Relay.NeedAddr"], isError: true);
                return;
            }

            await App.Relay.StopAsync();
            var ep = await App.Relay.StartAsync(App.Bridge.Port);
            OnPropertyChanged(nameof(RelayStatus));
            SetHint(ep is null
                ? string.Format(loc["Settings.Relay.ErrorFmt"], App.Relay.LastError ?? "")
                : string.Format(loc["Settings.Relay.RunningFmt"], ep), isError: ep is null);
        }
        else
        {
            await App.Relay.StopAsync();
            OnPropertyChanged(nameof(RelayStatus));
            SetHint(loc["Settings.Relay.Stopped"], isError: false);
        }
    }

    private void SetHint(string text, bool isError)
    {
        Hint = text;
        HintColor = (Brush)System.Windows.Application.Current.Resources[isError ? "ErrBrush" : "OkBrush"];
    }
}

using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwiftCompanion.Desktop.Services;

namespace SwiftCompanion.Desktop.ViewModels;

public partial class PairViewModel : ObservableObject
{
    private readonly BridgeManager _bridge;
    private readonly TunnelService _tunnel;

    /// <summary>
    /// 直连本机 Bridge：禁用系统代理（代理工具可能不放行 localhost 导致请求无限挂起），
    /// 并用 127.0.0.1 避免 localhost 的 IPv6 解析问题。
    /// </summary>
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        UseProxy = false,
        Proxy = null,
    })
    { Timeout = TimeSpan.FromSeconds(5) };

    public PairViewModel(BridgeManager bridge, TunnelService tunnel)
    {
        _bridge = bridge;
        _tunnel = tunnel;
        _tunnel.StateChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(RefreshTunnel);
        LocalizationService.Instance.LanguageChanged += (_, _) =>
            Application.Current?.Dispatcher.BeginInvoke(() => { RefreshTunnel(); RefreshHint(); });

        RefreshTunnel();
        _ = GenerateAsync();
    }

    [ObservableProperty] private BitmapImage? _qrImage;
    [ObservableProperty] private string _pairingCode = "";
    [ObservableProperty] private string _hint = "";
    [ObservableProperty] private bool _tunnelEnabled;
    [ObservableProperty] private string _tunnelStatus = "";
    [ObservableProperty] private string _tunnelStatusColor = "#FF7C8699";
    [ObservableProperty] private bool _canCopyTunnel;
    [ObservableProperty] private string _copyButtonText = "";

    /// <summary>True while a regenerate / new-bridge operation is in flight; disables both buttons.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewBridgeCommand))]
    private bool _isBusy;

    private bool CanRun() => !IsBusy;

    private void RefreshHint()
    {
        var loc = LocalizationService.Instance;
        if (_bridge.State != BridgeState.Running)
            Hint = loc["Pair.NeedRunning"];
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task Generate()
    {
        IsBusy = true;
        try { await GenerateAsync(); }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Spin up a fresh Bridge: restart it on a new port (which mints a new JWT-signing context
    /// and invalidates old paired sessions), then — if the Cloudflare tunnel is enabled — tear it
    /// down and start a brand-new one (also the way to recover from a faulted/stale CF tunnel),
    /// and finally generate a new pairing code + QR. Phones must re-pair afterwards.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task NewBridge()
    {
        IsBusy = true;
        var loc = LocalizationService.Instance;
        try
        {
            PairingCode = loc["Pair.Waiting"];
            QrImage = null;

            // Restart on a different port so this is genuinely a new bridge instance.
            var newPort = _bridge.Port + 1;
            await _bridge.RestartAsync(newPort).WaitAsync(TimeSpan.FromSeconds(20));
            if (_bridge.State != BridgeState.Running)
            {
                Hint = _bridge.LastError ?? loc["Pair.NeedRunning"];
                return;
            }

            // Tunnel is enabled (running, starting, OR faulted) — give it a clean restart on the
            // new port. Keying off TunnelEnabled (user intent) rather than live state means a
            // broken/errored CF tunnel gets regenerated instead of being left as-is.
            if (TunnelEnabled)
            {
                await _tunnel.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
                var url = await _tunnel.StartAsync(_bridge.LocalUrl).WaitAsync(TimeSpan.FromSeconds(45));
                await _bridge.SetPublicUrlAsync(url).WaitAsync(TimeSpan.FromSeconds(20)); // null if the tunnel failed; QR falls back to LAN
                if (url is null) SetTunnelEnabledQuiet(false);
            }

            await GenerateAsync();
        }
        catch (Exception ex)
        {
            Hint = ex.Message;
            PairingCode = "—"; // 明确显示失败态，避免界面看起来永远卡在"生成中"
        }
        finally { IsBusy = false; }
    }

    private async Task GenerateAsync()
    {
        var loc = LocalizationService.Instance;
        if (_bridge.State != BridgeState.Running)
        {
            Hint = loc["Pair.NeedRunning"];
            return;
        }

        PairingCode = loc["Pair.Waiting"];
        try
        {
            // Local API call; PairingController returns code + base64 QR (+ bridgeUrl/PublicUrl).
            var resp = await _http.PostAsync(_bridge.LocalApiUrl + "/api/pairing/start", null)
                .WaitAsync(TimeSpan.FromSeconds(8));
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            PairingCode = root.GetProperty("pairingCode").GetString() ?? "";
            var qrB64 = root.GetProperty("qrCode").GetString() ?? "";
            QrImage = DecodeQr(qrB64);
            Hint = "";
        }
        catch (Exception ex)
        {
            Hint = ex.Message;
            PairingCode = "—"; // 明确显示失败态，避免界面看起来永远卡在"生成中"
        }
    }

    /// <summary>QR comes back as "data:image/png;base64,..." or bare base64; handle both.</summary>
    private static BitmapImage? DecodeQr(string data)
    {
        if (string.IsNullOrEmpty(data)) return null;
        var comma = data.IndexOf(',');
        var b64 = comma >= 0 ? data[(comma + 1)..] : data;
        var bytes = Convert.FromBase64String(b64);

        var img = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private bool _isTogglingTunnel;

    /// <summary>程序内部改开关状态时用，不触发隧道启停（避免重入）。</summary>
    private void SetTunnelEnabledQuiet(bool value)
    {
        if (TunnelEnabled == value) return;
        _isTogglingTunnel = true;
        try { TunnelEnabled = value; }
        finally { _isTogglingTunnel = false; }
    }

    partial void OnTunnelEnabledChanged(bool value)
    {
        if (_isTogglingTunnel) return;
        _ = ToggleTunnelAsync(value);
    }

    private async Task ToggleTunnelAsync(bool enable)
    {
        _isTogglingTunnel = true;
        try
        {
            if (enable)
            {
                if (_bridge.State != BridgeState.Running) { SetTunnelEnabledQuiet(false); return; }
                var url = await _tunnel.StartAsync(_bridge.LocalUrl).WaitAsync(TimeSpan.FromSeconds(45));
                if (url is not null)
                {
                    // Point the Bridge's pairing QR at the public URL, then regenerate.
                    await _bridge.SetPublicUrlAsync(url).WaitAsync(TimeSpan.FromSeconds(20));
                    await GenerateAsync();
                }
                else
                {
                    SetTunnelEnabledQuiet(false);
                }
            }
            else
            {
                await _tunnel.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
                await _bridge.SetPublicUrlAsync(null).WaitAsync(TimeSpan.FromSeconds(20));
                await GenerateAsync();
            }
        }
        catch (Exception ex)
        {
            Hint = ex.Message;
        }
        finally
        {
            _isTogglingTunnel = false;
        }
    }

    private void RefreshTunnel()
    {
        var loc = LocalizationService.Instance;
        (TunnelStatus, TunnelStatusColor) = _tunnel.State switch
        {
            TunnelState.Running => (loc["Tunnel.Running"] + "  " + (_tunnel.PublicUrl ?? ""), "#FF34D399"),
            TunnelState.Starting => (loc["Tunnel.Starting"], "#FF7C8699"),
            TunnelState.Faulted => (loc.Format("Tunnel.Error", _tunnel.LastError ?? ""), "#FFFB7185"),
            _ => (loc["Tunnel.Stopped"], "#FF7C8699"),
        };
        CanCopyTunnel = _tunnel.State == TunnelState.Running && !string.IsNullOrEmpty(_tunnel.PublicUrl);
        CopyButtonText = loc["Tunnel.Copy"];
    }

    [RelayCommand]
    private async Task CopyTunnel()
    {
        if (string.IsNullOrEmpty(_tunnel.PublicUrl)) return;
        try
        {
            Clipboard.SetText(_tunnel.PublicUrl);
            // Brief "Copied!" confirmation, then revert the label.
            CopyButtonText = LocalizationService.Instance["Tunnel.Copied"];
            await Task.Delay(1500);
            CopyButtonText = LocalizationService.Instance["Tunnel.Copy"];
        }
        catch { /* clipboard may be locked by another app; ignore */ }
    }
}

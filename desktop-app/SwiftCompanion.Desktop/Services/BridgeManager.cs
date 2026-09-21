using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using SwiftBridge;

namespace SwiftCompanion.Desktop.Services;

public enum BridgeState { Stopped, Starting, Running, Faulted }

/// <summary>
/// Owns the in-process Bridge (ASP.NET Core) lifecycle: picks a free port, starts and
/// stops the <see cref="WebApplication"/> without blocking the UI thread.
/// </summary>
public sealed class BridgeManager : IAsyncDisposable
{
    private WebApplication? _app;

    public BridgeState State { get; private set; } = BridgeState.Stopped;
    public int Port { get; private set; }
    public string? PublicUrl { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Raised whenever <see cref="State"/> changes (marshal to UI thread by caller).</summary>
    public event EventHandler? StateChanged;

    public string LocalUrl => $"http://localhost:{Port}";

    /// <summary>本机 UI 调 Bridge 用：127.0.0.1 直连，绕开 localhost 的 IPv6/系统代理问题。</summary>
    public string LocalApiUrl => $"http://127.0.0.1:{Port}";

    /// <summary>Best-effort LAN IPv4 address phones on the same network use to reach the Bridge.</summary>
    public string? LanUrl
    {
        get
        {
            var ip = GetLanIPv4();
            return ip is null ? null : $"http://{ip}:{Port}";
        }
    }

    public async Task StartAsync(int preferredPort = 5000, string? publicUrl = null)
    {
        if (State is BridgeState.Running or BridgeState.Starting) return;

        SetState(BridgeState.Starting);
        try
        {
            Port = FindFreePort(preferredPort);
            PublicUrl = publicUrl;

            var app = BridgeHost.BuildApp(Port, publicUrl);
            if (app is null)
            {
                LastError = "Bridge configuration invalid (check Jwt:SecretKey in appsettings.json).";
                SetState(BridgeState.Faulted);
                return;
            }

            await app.StartAsync();
            _app = app;
            LastError = null;
            SetState(BridgeState.Running);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            try
            {
                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "bridge-startup-error.log"),
                    ex.ToString());
            }
            catch { /* best effort */ }
            SetState(BridgeState.Faulted);
        }
    }

    public async Task StopAsync()
    {
        if (_app is null) { SetState(BridgeState.Stopped); return; }
        try
        {
            await _app.StopAsync(TimeSpan.FromSeconds(5));
            await _app.DisposeAsync();
        }
        catch { /* shutting down; ignore */ }
        finally
        {
            _app = null;
            SetState(BridgeState.Stopped);
        }
    }

    public async Task RestartAsync(int preferredPort = 5000, string? publicUrl = null)
    {
        await StopAsync();
        await StartAsync(preferredPort, publicUrl);
    }

    /// <summary>
    /// Update the public (tunnel) URL the pairing QR embeds. Restarts the Bridge on the same
    /// port so PairingController picks up the new "PublicUrl" config value. Pass null to clear.
    /// </summary>
    public async Task SetPublicUrlAsync(string? publicUrl)
    {
        if (PublicUrl == publicUrl) return;
        if (State != BridgeState.Running)
        {
            PublicUrl = publicUrl;
            return;
        }
        var samePort = Port;
        await StopAsync();
        await StartAsync(samePort, publicUrl);
    }

    private void SetState(BridgeState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns <paramref name="preferred"/> if free, otherwise the next free port.</summary>
    private static int FindFreePort(int preferred)
    {
        var used = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(e => e.Port)
            .ToHashSet();

        for (var p = preferred; p < preferred + 100; p++)
        {
            if (!used.Contains(p)) return p;
        }
        // Fall back to an OS-assigned ephemeral port.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string? GetLanIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch { /* ignore */ }
        return null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

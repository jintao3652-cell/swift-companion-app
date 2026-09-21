using System.Diagnostics;
using System.IO;

namespace SwiftCompanion.Desktop.Services;

public enum RelayState { Stopped, Starting, Running, Faulted }

/// <summary>
/// Runs frpc (frp client) as a child process to expose the local Bridge through the
/// user's own server (frps) — the China-friendly alternative to the Cloudflare tunnel.
/// frpc reconnects to the server by itself; we additionally watch the process and
/// restart it (with backoff) if it ever dies, for as long as the relay is enabled.
/// </summary>
public sealed class RelayService : IAsyncDisposable
{
    private Process? _proc;
    private CancellationTokenSource? _restartCts;
    private bool _desiredRunning;
    private int _restartAttempts;

    public RelayState State { get; private set; } = RelayState.Stopped;
    /// <summary>"host:remotePort" the phone dials once the relay is up.</summary>
    public string? PublicEndpoint { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler? StateChanged;

    /// <summary>Locate the bundled frpc.exe, falling back to PATH.</summary>
    public static string? ResolveFrpc()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "relay", "frpc.exe"),
            Path.Combine(AppContext.BaseDirectory, "frpc.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return "frpc.exe";
    }

    /// <summary>Whether the bundled frpc.exe is present.</summary>
    public static bool IsFrpcAvailable()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "relay", "frpc.exe"),
            Path.Combine(AppContext.BaseDirectory, "frpc.exe"),
        };
        return candidates.Any(File.Exists);
    }

    /// <summary>Start the relay toward the configured server. Returns the public endpoint.</summary>
    public async Task<string?> StartAsync(int localBridgePort)
    {
        if (State is RelayState.Running or RelayState.Starting) return PublicEndpoint;
        if (!IsFrpcAvailable())
        {
            LastError = "frpc.exe not found.";
            SetState(RelayState.Faulted);
            return null;
        }

        var s = AppSettings.Instance;
        var serverAddr = s.RelayServerAddr.Trim();
        if (serverAddr.Length == 0 || s.RelayServerPort is < 1 or > 65535 ||
            s.RelayRemotePort is < 1 or > 65535)
        {
            LastError = "Relay server settings are incomplete.";
            SetState(RelayState.Faulted);
            return null;
        }

        _desiredRunning = true;
        _restartAttempts = 0;
        SetState(RelayState.Starting);
        PublicEndpoint = null;
        LastError = null;

        try
        {
            // frp v0.52+ TOML config.
            var toml = $"""
                serverAddr = "{serverAddr}"
                serverPort = {s.RelayServerPort}
                auth.token = "{s.RelayToken}"

                [[proxies]]
                name = "vatsim-bridge"
                type = "tcp"
                localIP = "127.0.0.1"
                localPort = {localBridgePort}
                remotePort = {s.RelayRemotePort}
                """;

            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SwiftCompanion", "frpc.toml");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await File.WriteAllTextAsync(configPath, toml);

            var psi = new ProcessStartInfo
            {
                FileName = ResolveFrpc()!,
                Arguments = $"-c \"{configPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var started = new TaskCompletionSource<bool>();

            _proc.OutputDataReceived += (_, ev) =>
            {
                // "login to server success" / "start proxy success" both mean we're up.
                if (ev.Data is not null &&
                    (ev.Data.Contains("success") || ev.Data.Contains("start proxy")))
                {
                    started.TrySetResult(true);
                }
            };
            _proc.ErrorDataReceived += (_, ev) => { /* frpc logs to stdout */ };
            _proc.Exited += (_, _) => _ = HandleExitedAsync();

            if (!_proc.Start())
            {
                LastError = "Failed to start frpc.";
                SetState(RelayState.Faulted);
                return null;
            }
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            var winner = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(20)));
            if (winner != started.Task || !started.Task.Result)
            {
                // frpc keeps retrying by itself; don't kill it — but flag not-ready.
                // If it exited, HandleExitedAsync will restart it.
                if (_proc.HasExited)
                {
                    LastError = "frpc exited during startup (check server address/token).";
                    SetState(RelayState.Faulted);
                    return null;
                }
                LastError = "frpc started but did not confirm login within 20s (will keep retrying).";
            }

            PublicEndpoint = $"{serverAddr}:{s.RelayRemotePort}";
            SetState(RelayState.Running);
            return PublicEndpoint;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(RelayState.Faulted);
            return null;
        }
    }

    public async Task StopAsync()
    {
        _desiredRunning = false;
        _restartCts?.Cancel();
        var p = _proc;
        _proc = null;
        if (p is not null)
        {
            try
            {
                if (!p.HasExited) p.Kill(entireProcessTree: true);
                await p.WaitForExitAsync();
            }
            catch { /* best effort */ }
            finally { p.Dispose(); }
        }
        PublicEndpoint = null;
        SetState(RelayState.Stopped);
    }

    private async Task HandleExitedAsync()
    {
        if (!_desiredRunning) return; // manual stop; nothing to do.

        // Auto-restart with capped backoff: 3s, 6s, 12s, ... max 60s.
        var delay = TimeSpan.FromSeconds(Math.Min(3 * Math.Pow(2, _restartAttempts++), 60));
        _restartCts = new CancellationTokenSource();
        var ct = _restartCts.Token;

        try
        {
            await Task.Delay(delay, ct);
        }
        catch (TaskCanceledException) { return; }

        if (!_desiredRunning || ct.IsCancellationRequested) return;
        if (State != RelayState.Running && State != RelayState.Starting)
        {
            SetState(RelayState.Starting);
            await StartAsync(GetBridgePort());
        }
    }

    private static int GetBridgePort()
    {
        var app = System.Windows.Application.Current as App;
        var port = app?.Bridge.Port;
        return port is > 0 ? port.Value : 5000;
    }

    private void SetState(RelayState s)
    {
        State = s;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

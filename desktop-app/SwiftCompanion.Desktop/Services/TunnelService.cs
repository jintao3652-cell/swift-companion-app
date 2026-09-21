using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace SwiftCompanion.Desktop.Services;

public enum TunnelState { Stopped, Starting, Running, Faulted }

/// <summary>
/// Runs a Cloudflare quick tunnel (cloudflared.exe) as a child process and extracts the
/// public https://*.trycloudflare.com URL from its stderr output.
/// </summary>
public sealed class TunnelService : IAsyncDisposable
{
    private Process? _proc;

    // cloudflared prints the assigned URL on stderr, e.g. "https://foo-bar.trycloudflare.com"
    private static readonly Regex UrlRegex =
        new(@"https://[a-z0-9-]+\.trycloudflare\.com", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public TunnelState State { get; private set; } = TunnelState.Stopped;
    public string? PublicUrl { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler? StateChanged;

    /// <summary>Locate the bundled cloudflared.exe, falling back to PATH.</summary>
    public static string? ResolveCloudflared()
    {
        // Bundled next to the exe (installer copies it to .\tunnel\), or repo layout during dev.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tunnel", "cloudflared.exe"),
            Path.Combine(AppContext.BaseDirectory, "cloudflared.exe"),
            Path.Combine(AppContext.BaseDirectory, "tunnel", "cloudflared-windows-amd64.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // Fall back to PATH.
        return "cloudflared.exe";
    }

    /// <summary>Start a quick tunnel to the given local URL. Resolves PublicUrl when ready.</summary>
    public async Task<string?> StartAsync(string localUrl)
    {
        if (State is TunnelState.Running or TunnelState.Starting) return PublicUrl;

        SetState(TunnelState.Starting);
        PublicUrl = null;
        LastError = null;

        var exe = ResolveCloudflared();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"tunnel --no-autoupdate --url {localUrl}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var tcs = new TaskCompletionSource<string?>();

            void Scan(string? line)
            {
                if (string.IsNullOrEmpty(line)) return;
                var m = UrlRegex.Match(line);
                if (m.Success && !tcs.Task.IsCompleted)
                    tcs.TrySetResult(m.Value);
            }

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, ev) => Scan(ev.Data);
            _proc.ErrorDataReceived += (_, ev) => Scan(ev.Data);
            _proc.Exited += (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
                if (State != TunnelState.Stopped)
                {
                    LastError = "cloudflared exited unexpectedly.";
                    SetState(TunnelState.Faulted);
                }
            };

            if (!_proc.Start())
            {
                LastError = "Failed to start cloudflared.";
                SetState(TunnelState.Faulted);
                return null;
            }
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            // Wait up to 30s for the URL to appear.
            var winner = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            if (winner == tcs.Task && tcs.Task.Result is { } url)
            {
                PublicUrl = url;
                SetState(TunnelState.Running);
                return url;
            }

            LastError = "Timed out waiting for tunnel URL.";
            await StopAsync();
            SetState(TunnelState.Faulted);
            return null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(TunnelState.Faulted);
            return null;
        }
    }

    public async Task StopAsync()
    {
        var p = _proc;
        _proc = null;
        if (p is not null)
        {
            try
            {
                if (!p.HasExited) p.Kill(entireProcessTree: true);
                await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch { /* best effort */ }
            finally { p.Dispose(); }
        }
        PublicUrl = null;
        SetState(TunnelState.Stopped);
    }

    private void SetState(TunnelState s)
    {
        State = s;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

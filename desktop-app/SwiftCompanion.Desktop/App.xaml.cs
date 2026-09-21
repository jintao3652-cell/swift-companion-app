using System.Threading;
using System.Windows;
using SwiftCompanion.Desktop.Services;

namespace SwiftCompanion.Desktop;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    /// <summary>App-wide Bridge lifecycle owner, shared with view models.</summary>
    public BridgeManager Bridge { get; } = new();

    /// <summary>Cloudflare quick-tunnel manager, shared with the Pair view.</summary>
    public TunnelService Tunnel { get; } = new();

    /// <summary>Self-hosted frp relay (China-friendly), managed from Settings.</summary>
    public RelayService Relay { get; } = new();

    /// <summary>Real-time chat client for the in-process Bridge (Messages view).</summary>
    public ChatService Chat { get; } = new();

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance guard so a second launch doesn't fight over the port.
        _singleInstanceMutex = new Mutex(true, "SwiftCompanion.Desktop.SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("Swift Companion is already running.", "SwiftCompanion",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        AppSettings.Load();

        // Seed localized strings into app resources before any window binds them.
        LocalizationService.Instance.Apply();

        // Start the Bridge in the background; the UI shows progress via state changes.
        _ = Bridge.StartAsync();

        // Create the window after resources are seeded (no StartupUri, so ordering is ours).
        MainWindow = new MainWindow();
        MainWindow.Show();

    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        // WPF does NOT wait for async OnExit continuations — an async override lets the
        // dispatcher tear down before the Bridge / tunnel / relay are ever stopped, leaving
        // the process (and its cloudflared/frpc children) running in the background.
        // So: bounded synchronous cleanup + a hard Environment.Exit.
        try
        {
            Task.WhenAll(
                Tunnel.DisposeAsync().AsTask(),
                Relay.DisposeAsync().AsTask(),
                Chat.DisposeAsync().AsTask(),
                Bridge.DisposeAsync().AsTask())
                .Wait(TimeSpan.FromSeconds(6)); // bounded: never block exit indefinitely
        }
        catch { /* shutting down; best effort */ }

        try { _singleInstanceMutex?.Dispose(); } catch { /* abandoned is fine */ }

        // Hard guarantee: nothing (in-process ASP.NET Core host, orphaned threads,
        // tunnel children) may keep the process alive after an explicit exit.
        Environment.Exit(0);
    }
}

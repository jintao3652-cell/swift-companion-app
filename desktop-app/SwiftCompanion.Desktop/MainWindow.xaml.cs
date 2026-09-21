using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SwiftCompanion.Desktop.Services;
using SwiftCompanion.Desktop.Views;

namespace SwiftCompanion.Desktop;

public partial class MainWindow : Window
{
    private readonly HomeView _home = new();
    private PairView? _pair;
    private ChatView? _chat;
    private SettingsView? _settings;

    // Set true only when we really want to exit, so Window_Closing lets the close through
    // instead of re-prompting or hiding.
    private bool _exiting;
    // Show the "still running in the tray" balloon only the first time we hide.
    private bool _trayHintShown;

    public MainWindow()
    {
        InitializeComponent();
        ContentHost.Content = _home;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ContentHost is null) return; // during InitializeComponent
        var tag = (sender as RadioButton)?.Tag as string;
        ContentHost.Content = tag switch
        {
            "Home" => _home,
            "Pair" => _pair ??= new PairView(),
            "Chat" => _chat ??= new ChatView(),
            "Settings" => _settings ??= new SettingsView(),
            _ => new Placeholder(tag ?? "")
        };
    }

    private void LangButton_Click(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.Toggle();
    }

    // ===== Close / minimize / tray =====

    /// <summary>X button: decide between hide-to-tray and full exit, honoring a remembered
    /// choice or asking when set to <see cref="CloseAction.Ask"/>.</summary>
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_exiting) return; // an explicit exit is already in flight; let it close.

        switch (AppSettings.Instance.CloseAction)
        {
            case CloseAction.Exit:
                e.Cancel = true;
                ExitApp();
                break;

            case CloseAction.HideToTray:
                e.Cancel = true;
                HideToTray();
                break;

            default: // Ask
                e.Cancel = true;
                var dlg = new CloseConfirmDialog { Owner = this };
                if (dlg.ShowDialog() != true) return; // user dismissed the dialog: do nothing.

                if (dlg.Remember)
                {
                    AppSettings.Instance.CloseAction = dlg.Chosen;
                    AppSettings.Instance.Save();
                }

                if (dlg.Chosen == CloseAction.Exit) ExitApp();
                else HideToTray();
                break;
        }
    }

    /// <summary>Minimize (-) button: hide to the tray instead of a normal taskbar minimize.</summary>
    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    private void HideToTray()
    {
        // Reset to Normal first so a later restore doesn't come back minimized.
        WindowState = WindowState.Normal;
        Hide();

        if (!_trayHintShown)
        {
            _trayHintShown = true;
            var loc = LocalizationService.Instance;
            try
            {
                Tray.ShowBalloonTip(loc["Tray.Hidden.Title"], loc["Tray.Hidden.Body"],
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
            }
            catch
            {
                // Balloon tips are best-effort (can fail if notifications are disabled).
            }
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;  // nudge to front…
        Topmost = false; // …then drop back to normal z-order.
    }

    private void ExitApp()
    {
        _exiting = true;
        Tray.Dispose();        // remove the tray icon immediately
        Application.Current.Shutdown();
    }

    private void Tray_DoubleClick(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void TrayShow_Click(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void TrayExit_Click(object sender, RoutedEventArgs e) => ExitApp();
}

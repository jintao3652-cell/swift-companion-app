using System.Windows;
using SwiftCompanion.Desktop.Services;

namespace SwiftCompanion.Desktop.Views;

/// <summary>
/// Shown when the user clicks the window's close (X) button while
/// <see cref="AppSettings.CloseAction"/> is <see cref="CloseAction.Ask"/>. Offers
/// "minimize to tray" (keep the bridge running) or "exit completely", with an optional
/// "remember my choice" that persists the picked action so the dialog won't show again.
/// </summary>
public partial class CloseConfirmDialog : Window
{
    /// <summary>The action the user picked. Only meaningful when DialogResult == true.</summary>
    public CloseAction Chosen { get; private set; } = CloseAction.HideToTray;

    /// <summary>Whether the user asked us to remember <see cref="Chosen"/>.</summary>
    public bool Remember => RememberBox.IsChecked == true;

    public CloseConfirmDialog()
    {
        InitializeComponent();
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        Chosen = CloseAction.HideToTray;
        DialogResult = true;
        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Chosen = CloseAction.Exit;
        DialogResult = true;
        Close();
    }
}

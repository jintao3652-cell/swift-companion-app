using System.Windows.Controls;
using SwiftCompanion.Desktop.ViewModels;

namespace SwiftCompanion.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }
}

using System.Windows.Controls;
using SwiftCompanion.Desktop.ViewModels;

namespace SwiftCompanion.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        DataContext = new HomeViewModel(App.Current.Bridge, App.Current.Tunnel);
    }
}

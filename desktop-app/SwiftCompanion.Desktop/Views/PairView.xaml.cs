using System.Windows.Controls;
using SwiftCompanion.Desktop.ViewModels;

namespace SwiftCompanion.Desktop.Views;

public partial class PairView : UserControl
{
    public PairView()
    {
        InitializeComponent();
        DataContext = new PairViewModel(App.Current.Bridge, App.Current.Tunnel);
    }
}

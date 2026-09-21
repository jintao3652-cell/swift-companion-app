using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SwiftCompanion.Desktop.ViewModels;

namespace SwiftCompanion.Desktop.Views;

public partial class ChatView : UserControl
{
    private readonly ChatViewModel _vm;

    public ChatView()
    {
        InitializeComponent();
        _vm = new ChatViewModel(App.Current.Bridge, App.Current.Chat);
        DataContext = _vm;
        _vm.Messages.CollectionChanged += (_, _) => ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        if (MessagesList.Items.Count == 0) return;
        MessagesList.ScrollIntoView(MessagesList.Items[^1]);
    }

    /// <summary>ComboBox selection → private mode toggle.</summary>
    private void Mode_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _vm.IsPrivateMode = ModeCombo.SelectedIndex == 1;

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _vm.SendCommand.Execute(null);
    }
}

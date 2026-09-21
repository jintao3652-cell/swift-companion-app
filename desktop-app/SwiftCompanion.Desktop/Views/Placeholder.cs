using System.Windows.Controls;
using System.Windows;

namespace SwiftCompanion.Desktop.Views;

/// <summary>Temporary stand-in for pages built in later stages (Pair / Settings / Tools).</summary>
public sealed class Placeholder : UserControl
{
    public Placeholder(string name)
    {
        Content = new TextBlock
        {
            Text = $"{name} — coming soon",
            FontSize = 18,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}

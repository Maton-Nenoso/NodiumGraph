using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GettingStarted;

public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}

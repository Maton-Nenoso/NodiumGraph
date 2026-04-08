using Avalonia;
using Avalonia.Markup.Xaml;

namespace NodiumGraph.Tests;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

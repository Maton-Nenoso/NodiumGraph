using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NodiumGraph.Model;

namespace NodiumGraph.Tests.Helpers;

public partial class DeclarativePortsTestWindow : Window
{
    public DeclarativePortsTestWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}

public class DeclarativeNodeA : Node { }
public class DeclarativeNodeDerivedA : DeclarativeNodeA { }
public class DeclarativeNodeVisualOnly : Node { }

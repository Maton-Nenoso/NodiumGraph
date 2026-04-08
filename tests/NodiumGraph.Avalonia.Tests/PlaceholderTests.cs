using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace NodiumGraph.Avalonia.Tests;

public class PlaceholderTests
{
    [AvaloniaFact]
    public void NodiumGraphCanvas_can_be_created()
    {
        var canvas = new NodiumGraph.Avalonia.NodiumGraphCanvas();
        var window = new Window { Content = canvas };
        window.Show();

        Assert.NotNull(canvas);
    }
}

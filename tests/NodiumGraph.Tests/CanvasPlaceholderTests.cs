using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace NodiumGraph.Tests;

public class CanvasPlaceholderTests
{
    [AvaloniaFact]
    public void NodiumGraphCanvas_can_be_created()
    {
        var canvas = new NodiumGraphCanvas();
        var window = new Window { Content = canvas };
        window.Show();
        Assert.NotNull(canvas);
    }
}

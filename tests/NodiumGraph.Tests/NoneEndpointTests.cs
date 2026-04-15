using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Interactions;
using Xunit;

namespace NodiumGraph.Tests;

public class NoneEndpointTests
{
    [AvaloniaFact]
    public void GetInset_always_zero()
    {
        Assert.Equal(0, NoneEndpoint.Instance.GetInset(1));
        Assert.Equal(0, NoneEndpoint.Instance.GetInset(8));
    }

    [AvaloniaFact]
    public void IsFilled_false()
    {
        Assert.False(NoneEndpoint.Instance.IsFilled);
    }

    [AvaloniaFact]
    public void BuildGeometry_returns_empty()
    {
        var geo = NoneEndpoint.Instance.BuildGeometry(new Point(0, 0), new Vector(1, 0), 2);
        Assert.NotNull(geo);
        Assert.Equal(0, geo.Bounds.Width);
        Assert.Equal(0, geo.Bounds.Height);
    }
}

using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class PortAnchorTests
{
    [Fact]
    public void Throws_on_invalid_PortEdge_cast()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAnchor((PortEdge)999, 0.5));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Throws_on_invalid_Fraction(double f)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAnchor(PortEdge.Top, f));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Accepts_Fraction_in_unit_interval(double f)
    {
        var a = new PortAnchor(PortEdge.Top, f);
        Assert.Equal(f, a.Fraction);
        Assert.Equal(PortEdge.Top, a.Edge);
    }

    [Fact]
    public void Static_helpers_set_edge_correctly()
    {
        Assert.Equal(PortEdge.Left,   PortAnchor.Left(0.5).Edge);
        Assert.Equal(PortEdge.Top,    PortAnchor.Top(0.5).Edge);
        Assert.Equal(PortEdge.Right,  PortAnchor.Right(0.5).Edge);
        Assert.Equal(PortEdge.Bottom, PortAnchor.Bottom(0.5).Edge);
    }

    [Fact]
    public void Value_equality_by_components()
    {
        Assert.Equal(new PortAnchor(PortEdge.Top, 0.5), new PortAnchor(PortEdge.Top, 0.5));
        Assert.NotEqual(new PortAnchor(PortEdge.Top, 0.5), new PortAnchor(PortEdge.Bottom, 0.5));
        Assert.NotEqual(new PortAnchor(PortEdge.Top, 0.5), new PortAnchor(PortEdge.Top, 0.6));
    }
}

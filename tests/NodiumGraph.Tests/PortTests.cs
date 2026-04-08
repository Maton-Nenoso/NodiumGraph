using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class PortTests
{
    [Fact]
    public void New_port_has_unique_id()
    {
        var node = new Node();
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(10, 10));
        Assert.NotEqual(port1.Id, port2.Id);
    }

    [Fact]
    public void Port_stores_owner_and_position()
    {
        var node = new Node();
        var port = new Port(node, new Point(5, 10));
        Assert.Same(node, port.Owner);
        Assert.Equal(new Point(5, 10), port.Position);
    }

    [Fact]
    public void AbsolutePosition_adds_owner_position()
    {
        var node = new Node { X = 100, Y = 200 };
        var port = new Port(node, new Point(10, 20));
        Assert.Equal(new Point(110, 220), port.AbsolutePosition);
    }

    [Fact]
    public void AbsolutePosition_updates_when_node_moves()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(10, 10));
        Assert.Equal(new Point(10, 10), port.AbsolutePosition);

        node.X = 50;
        node.Y = 75;
        Assert.Equal(new Point(60, 85), port.AbsolutePosition);
    }

    [Fact]
    public void Port_requires_owner()
    {
        Assert.Throws<ArgumentNullException>(() => new Port(null!, new Point(0, 0)));
    }
}

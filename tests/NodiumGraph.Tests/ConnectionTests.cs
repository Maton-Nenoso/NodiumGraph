using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionTests
{
    [Fact]
    public void New_connection_has_unique_id()
    {
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 10));
        var conn1 = new Connection(source, target);
        var conn2 = new Connection(source, target);
        Assert.NotEqual(conn1.Id, conn2.Id);
    }

    [Fact]
    public void Connection_stores_source_and_target()
    {
        var nodeA = new Node();
        var nodeB = new Node();
        var source = new Port(nodeA, new Point(0, 0));
        var target = new Port(nodeB, new Point(0, 0));
        var conn = new Connection(source, target);
        Assert.Same(source, conn.SourcePort);
        Assert.Same(target, conn.TargetPort);
    }

    [Fact]
    public void Connection_requires_source()
    {
        var node = new Node();
        var target = new Port(node, new Point(0, 0));
        Assert.Throws<ArgumentNullException>(() => new Connection(null!, target));
    }

    [Fact]
    public void Connection_requires_target()
    {
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        Assert.Throws<ArgumentNullException>(() => new Connection(source, null!));
    }
}

using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests.Interactions;

public class DefaultConnectionValidatorTests
{
    private enum SampleKind { Number, Text }

    private static Node NewNode() => new();

    [Fact]
    public void Rejects_SamePort()
    {
        var node = NewNode();
        var port = new Port(node, "out", PortFlow.Output, new Point(0, 0));
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(port, port));
    }

    [Fact]
    public void Rejects_SameOwner()
    {
        var node = NewNode();
        var output = new Port(node, "out", PortFlow.Output, new Point(0, 0));
        var input = new Port(node, "in", PortFlow.Input, new Point(10, 0));
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(output, input));
    }

    [Fact]
    public void Rejects_SameFlow_Output()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var a = new Port(nodeA, "out", PortFlow.Output, new Point(0, 0));
        var b = new Port(nodeB, "out", PortFlow.Output, new Point(0, 0));
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(a, b));
    }

    [Fact]
    public void Rejects_SameFlow_Input()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var a = new Port(nodeA, "in", PortFlow.Input, new Point(0, 0));
        var b = new Port(nodeB, "in", PortFlow.Input, new Point(0, 0));
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(a, b));
    }

    [Fact]
    public void Accepts_OppositeFlow_BothNullDataType()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = new Port(nodeA, "out", PortFlow.Output, new Point(0, 0));
        var target = new Port(nodeB, "in", PortFlow.Input, new Point(0, 0));
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Accepts_OppositeFlow_MatchingString()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = new Port(nodeA, "out", PortFlow.Output, new Point(0, 0)) { DataType = "number" };
        var target = new Port(nodeB, "in", PortFlow.Input, new Point(0, 0)) { DataType = "number" };
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Accepts_OppositeFlow_MatchingEnum()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = new Port(nodeA, "out", PortFlow.Output, new Point(0, 0)) { DataType = SampleKind.Number };
        var target = new Port(nodeB, "in", PortFlow.Input, new Point(0, 0)) { DataType = SampleKind.Number };
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Accepts_OppositeFlow_MatchingType()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = new Port(nodeA, "out", PortFlow.Output, new Point(0, 0)) { DataType = typeof(int) };
        var target = new Port(nodeB, "in", PortFlow.Input, new Point(0, 0)) { DataType = typeof(int) };
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Rejects_OppositeFlow_MismatchedString()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = new Port(nodeA, "out", PortFlow.Output, new Point(0, 0)) { DataType = "number" };
        var target = new Port(nodeB, "in", PortFlow.Input, new Point(0, 0)) { DataType = "string" };
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Rejects_OppositeFlow_OneNullOneTyped()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = new Port(nodeA, "out", PortFlow.Output, new Point(0, 0));
        var target = new Port(nodeB, "in", PortFlow.Input, new Point(0, 0)) { DataType = "number" };
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }
}

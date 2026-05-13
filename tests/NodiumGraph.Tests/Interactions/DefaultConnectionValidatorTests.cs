using NodiumGraph.Interactions;
using NodiumGraph.Model;
using NodiumGraph.Tests.Helpers;
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
        var port = TestNodes.PortAt(node, 0, 0, "out", PortFlow.Output);
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(port, port));
    }

    [Fact]
    public void Rejects_SameOwner()
    {
        var node = NewNode();
        var output = TestNodes.PortAt(node, 0, 0, "out", PortFlow.Output);
        var input = TestNodes.PortAt(node, 10, 0, "in", PortFlow.Input);
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(output, input));
    }

    [Fact]
    public void Rejects_SameFlow_Output()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var a = TestNodes.PortAt(nodeA, 0, 0, "out", PortFlow.Output);
        var b = TestNodes.PortAt(nodeB, 0, 0, "out", PortFlow.Output);
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(a, b));
    }

    [Fact]
    public void Rejects_SameFlow_Input()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var a = TestNodes.PortAt(nodeA, 0, 0, "in", PortFlow.Input);
        var b = TestNodes.PortAt(nodeB, 0, 0, "in", PortFlow.Input);
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(a, b));
    }

    [Fact]
    public void Accepts_OppositeFlow_BothNullDataType()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = TestNodes.PortAt(nodeA, 0, 0, "out", PortFlow.Output);
        var target = TestNodes.PortAt(nodeB, 0, 0, "in", PortFlow.Input);
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Accepts_OppositeFlow_MatchingString()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = TestNodes.PortAt(nodeA, 0, 0, "out", PortFlow.Output);
        source.DataType = "number";
        var target = TestNodes.PortAt(nodeB, 0, 0, "in", PortFlow.Input);
        target.DataType = "number";
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Accepts_OppositeFlow_MatchingEnum()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = TestNodes.PortAt(nodeA, 0, 0, "out", PortFlow.Output);
        source.DataType = SampleKind.Number;
        var target = TestNodes.PortAt(nodeB, 0, 0, "in", PortFlow.Input);
        target.DataType = SampleKind.Number;
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Accepts_OppositeFlow_MatchingType()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = TestNodes.PortAt(nodeA, 0, 0, "out", PortFlow.Output);
        source.DataType = typeof(int);
        var target = TestNodes.PortAt(nodeB, 0, 0, "in", PortFlow.Input);
        target.DataType = typeof(int);
        Assert.True(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Rejects_OppositeFlow_MismatchedString()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = TestNodes.PortAt(nodeA, 0, 0, "out", PortFlow.Output);
        source.DataType = "number";
        var target = TestNodes.PortAt(nodeB, 0, 0, "in", PortFlow.Input);
        target.DataType = "string";
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }

    [Fact]
    public void Rejects_OppositeFlow_OneNullOneTyped()
    {
        var nodeA = NewNode();
        var nodeB = NewNode();
        var source = TestNodes.PortAt(nodeA, 0, 0, "out", PortFlow.Output);
        var target = TestNodes.PortAt(nodeB, 0, 0, "in", PortFlow.Input);
        target.DataType = "number";
        Assert.False(DefaultConnectionValidator.Instance.CanConnect(source, target));
    }
}

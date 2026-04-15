using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class IGraphElementTests
{
    [Fact]
    public void Node_implements_IGraphElement()
    {
        Assert.True(typeof(IGraphElement).IsAssignableFrom(typeof(Node)));
    }

    [Fact]
    public void Connection_implements_IGraphElement()
    {
        Assert.True(typeof(IGraphElement).IsAssignableFrom(typeof(Connection)));
    }
}

using NodiumGraph;
using Xunit;

namespace NodiumGraph.Tests;

public class PlaceholderTests
{
    [Fact]
    public void INode_interface_is_accessible()
    {
        var type = typeof(INode);
        Assert.NotNull(type);
    }
}

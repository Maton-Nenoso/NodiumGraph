using NodiumGraph.Core;
using Xunit;

namespace NodiumGraph.Core.Tests;

public class PlaceholderTests
{
    [Fact]
    public void INode_interface_is_accessible()
    {
        var type = typeof(INode);
        Assert.NotNull(type);
    }
}

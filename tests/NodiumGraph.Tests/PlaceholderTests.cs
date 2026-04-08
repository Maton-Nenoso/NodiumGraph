using NodiumGraph;
using Xunit;

namespace NodiumGraph.Tests;

public class PlaceholderTests
{
    [Fact]
    public void Node_class_is_accessible()
    {
        var type = typeof(Node);
        Assert.NotNull(type);
    }
}

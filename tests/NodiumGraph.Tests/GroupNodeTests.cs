using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class GroupNodeTests
{
    [Fact]
    public void GroupNode_extends_Node()
    {
        var group = new GroupNode();
        Assert.IsAssignableFrom<Node>(group);
    }

    [Fact]
    public void Children_starts_empty()
    {
        var group = new GroupNode();
        Assert.Empty(group.Children);
    }

    [Fact]
    public void Can_add_and_remove_children()
    {
        var group = new GroupNode();
        var child = new Node();
        group.AddChild(child);
        Assert.Contains(child, group.Children);

        group.RemoveChild(child);
        Assert.DoesNotContain(child, group.Children);
    }

    [Fact]
    public void AddChild_ignores_duplicate()
    {
        var group = new GroupNode();
        var child = new Node();
        group.AddChild(child);
        group.AddChild(child);
        Assert.Single(group.Children);
    }

    [Fact]
    public void Title_defaults_to_GroupNode()
    {
        var group = new GroupNode();
        Assert.Equal("GroupNode", group.Title);
    }

    [Fact]
    public void AddChild_null_throws()
    {
        var group = new GroupNode();
        Assert.Throws<ArgumentNullException>(() => group.AddChild(null!));
    }
}

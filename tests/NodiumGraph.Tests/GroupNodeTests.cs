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

    [Fact]
    public void Moving_group_moves_children()
    {
        var group = new GroupNode { X = 100, Y = 100 };
        var child = new Node { X = 150, Y = 150 };
        group.AddChild(child);

        group.X = 200;
        Assert.Equal(250, child.X); // moved by delta 100

        group.Y = 300;
        Assert.Equal(350, child.Y); // moved by delta 200
    }

    [Fact]
    public void Moving_group_moves_multiple_children()
    {
        var group = new GroupNode { X = 0, Y = 0 };
        var c1 = new Node { X = 10, Y = 20 };
        var c2 = new Node { X = 30, Y = 40 };
        group.AddChild(c1);
        group.AddChild(c2);

        group.X = 50;
        group.Y = 60;

        Assert.Equal(60, c1.X);
        Assert.Equal(80, c1.Y);
        Assert.Equal(80, c2.X);
        Assert.Equal(100, c2.Y);
    }
}

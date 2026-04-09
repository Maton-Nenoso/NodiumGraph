using NodiumGraph.Model;
using System.ComponentModel;
using Xunit;

namespace NodiumGraph.Tests;

public class CommentNodeTests
{
    [Fact]
    public void CommentNode_extends_Node()
    {
        var comment = new CommentNode();
        Assert.IsAssignableFrom<Node>(comment);
    }

    [Fact]
    public void Comment_defaults_to_empty()
    {
        var comment = new CommentNode();
        Assert.Equal(string.Empty, comment.Comment);
    }

    [Fact]
    public void Comment_fires_PropertyChanged()
    {
        var comment = new CommentNode();
        var fired = false;
        ((INotifyPropertyChanged)comment).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommentNode.Comment)) fired = true;
        };
        comment.Comment = "Hello";
        Assert.True(fired);
    }

    [Fact]
    public void Title_defaults_to_CommentNode()
    {
        var comment = new CommentNode();
        Assert.Equal("CommentNode", comment.Title);
    }
}

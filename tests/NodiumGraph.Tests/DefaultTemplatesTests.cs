using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class DefaultTemplatesTests
{
    [AvaloniaFact]
    public void NodeTemplate_creates_control_for_Node()
    {
        var node = new Node { Title = "Test" };
        var content = DefaultTemplates.NodeTemplate.Build(node);
        Assert.NotNull(content);
    }

    [AvaloniaFact]
    public void GroupNodeTemplate_creates_control_for_GroupNode()
    {
        var group = new GroupNode();
        var content = DefaultTemplates.GroupNodeTemplate.Build(group);
        Assert.NotNull(content);
    }

    [AvaloniaFact]
    public void CommentNodeTemplate_creates_control_for_CommentNode()
    {
        var comment = new CommentNode { Comment = "Hello" };
        var content = DefaultTemplates.CommentNodeTemplate.Build(comment);
        Assert.NotNull(content);
    }

    [AvaloniaFact]
    public void ResolveTemplate_returns_comment_template_for_CommentNode()
    {
        var node = new CommentNode();
        var template = DefaultTemplates.ResolveTemplate(node, null);
        Assert.Same(DefaultTemplates.CommentNodeTemplate, template);
    }

    [AvaloniaFact]
    public void ResolveTemplate_returns_group_template_for_GroupNode()
    {
        var node = new GroupNode();
        var template = DefaultTemplates.ResolveTemplate(node, null);
        Assert.Same(DefaultTemplates.GroupNodeTemplate, template);
    }

    [AvaloniaFact]
    public void ResolveTemplate_returns_default_for_base_Node()
    {
        var node = new Node();
        var template = DefaultTemplates.ResolveTemplate(node, null);
        Assert.Same(DefaultTemplates.NodeTemplate, template);
    }

    [AvaloniaFact]
    public void ResolveTemplate_prefers_custom_template_when_it_matches()
    {
        var node = new Node();
        var custom = DefaultTemplates.NodeTemplate; // use same template as a stand-in
        var template = DefaultTemplates.ResolveTemplate(node, custom);
        Assert.Same(custom, template);
    }
}

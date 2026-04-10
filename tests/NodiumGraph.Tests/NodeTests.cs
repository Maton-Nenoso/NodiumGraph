using NodiumGraph.Model;
using System.ComponentModel;
using Xunit;

namespace NodiumGraph.Tests;

public class NodeTests
{
    [Fact]
    public void New_node_has_unique_id()
    {
        var node1 = new Node();
        var node2 = new Node();
        Assert.NotEqual(node1.Id, node2.Id);
    }

    [Fact]
    public void Default_position_is_zero()
    {
        var node = new Node();
        Assert.Equal(0.0, node.X);
        Assert.Equal(0.0, node.Y);
    }

    [Fact]
    public void Setting_X_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        string? propertyName = null;

        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            fired = true;
            propertyName = e.PropertyName;
        };

        node.X = 100.0;

        Assert.True(fired);
        Assert.Equal(nameof(Node.X), propertyName);
    }

    [Fact]
    public void Setting_Y_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        string? propertyName = null;

        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            fired = true;
            propertyName = e.PropertyName;
        };

        node.Y = 200.0;

        Assert.True(fired);
        Assert.Equal(nameof(Node.Y), propertyName);
    }

    [Fact]
    public void Setting_same_X_value_does_not_fire_PropertyChanged()
    {
        var node = new Node { X = 50.0 };
        var fired = false;

        ((INotifyPropertyChanged)node).PropertyChanged += (_, _) => fired = true;

        node.X = 50.0;

        Assert.False(fired);
    }

    [Fact]
    public void Width_and_Height_default_to_zero()
    {
        var node = new Node();
        Assert.Equal(0.0, node.Width);
        Assert.Equal(0.0, node.Height);
    }

    [Fact]
    public void PortProvider_defaults_to_null()
    {
        var node = new Node();
        Assert.Null(node.PortProvider);
    }

    [Fact]
    public void Title_defaults_to_type_name()
    {
        var node = new Node();
        Assert.Equal("Node", node.Title);
    }

    [Fact]
    public void Title_can_be_set_and_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.Title)) fired = true;
        };

        node.Title = "My Node";
        Assert.Equal("My Node", node.Title);
        Assert.True(fired);
    }

    [Fact]
    public void IsSelected_defaults_to_false()
    {
        var node = new Node();
        Assert.False(node.IsSelected);
    }

    [Fact]
    public void IsSelected_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.IsSelected)) fired = true;
        };

        node.IsSelected = true;
        Assert.True(fired);
    }

    [Fact]
    public void Setting_Width_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.Width)) fired = true;
        };
        node.Width = 100.0;
        Assert.True(fired);
    }

    [Fact]
    public void Setting_Height_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.Height)) fired = true;
        };
        node.Height = 80.0;
        Assert.True(fired);
    }

    [Fact]
    public void Subclass_title_defaults_to_subclass_name()
    {
        var node = new TestNode();
        Assert.Equal("TestNode", node.Title);
    }

    [Fact]
    public void ShowHeader_defaults_to_true()
    {
        var node = new Node();
        Assert.True(node.ShowHeader);
    }

    [Fact]
    public void ShowHeader_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.ShowHeader)) fired = true;
        };

        node.ShowHeader = false;
        Assert.True(fired);
    }

    [Fact]
    public void ShowHeader_does_not_fire_when_set_to_same_value()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, _) => fired = true;

        node.ShowHeader = true; // same as default
        Assert.False(fired);
    }

    [Fact]
    public void ShowHeader_does_not_affect_Title()
    {
        var node = new Node { Title = "My Node" };
        node.ShowHeader = false;
        Assert.Equal("My Node", node.Title);
    }

    [Fact]
    public void IsCollapsed_defaults_to_false()
    {
        var node = new Node();
        Assert.False(node.IsCollapsed);
    }

    [Fact]
    public void IsCollapsed_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.IsCollapsed)) fired = true;
        };

        node.IsCollapsed = true;
        Assert.True(node.IsCollapsed);
        Assert.True(fired);
    }

    [Fact]
    public void IsCollapsed_does_not_fire_when_set_to_same_value()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, _) => fired = true;

        node.IsCollapsed = false; // same as default
        Assert.False(fired);
    }

    [Fact]
    public void IsCollapsed_can_be_toggled()
    {
        var node = new Node();

        node.IsCollapsed = true;
        Assert.True(node.IsCollapsed);

        node.IsCollapsed = false;
        Assert.False(node.IsCollapsed);
    }

    [Fact]
    public void IsCollapsed_does_not_affect_other_properties()
    {
        var node = new Node { Title = "Test", X = 10, Y = 20 };
        node.IsCollapsed = true;

        Assert.Equal("Test", node.Title);
        Assert.Equal(10.0, node.X);
        Assert.Equal(20.0, node.Y);
        Assert.True(node.ShowHeader);
    }

    private class TestNode : Node { }
}

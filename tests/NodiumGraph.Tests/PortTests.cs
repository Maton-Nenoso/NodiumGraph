using System.ComponentModel;
using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class PortTests
{
    [Fact]
    public void New_port_has_unique_id()
    {
        var node = new Node();
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(10, 10));
        Assert.NotEqual(port1.Id, port2.Id);
    }

    [Fact]
    public void Port_stores_owner_and_position()
    {
        var node = new Node();
        var port = new Port(node, new Point(5, 10));
        Assert.Same(node, port.Owner);
        Assert.Equal(new Point(5, 10), port.Position);
    }

    [Fact]
    public void AbsolutePosition_adds_owner_position()
    {
        var node = new Node { X = 100, Y = 200 };
        var port = new Port(node, new Point(10, 20));
        Assert.Equal(new Point(110, 220), port.AbsolutePosition);
    }

    [Fact]
    public void AbsolutePosition_updates_when_node_moves()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(10, 10));
        Assert.Equal(new Point(10, 10), port.AbsolutePosition);

        node.X = 50;
        node.Y = 75;
        Assert.Equal(new Point(60, 85), port.AbsolutePosition);
    }

    [Fact]
    public void Port_requires_owner()
    {
        Assert.Throws<ArgumentNullException>(() => new Port(null!, new Point(0, 0)));
    }

    [Fact]
    public void Port_stores_name()
    {
        var node = new Node();
        var port = new Port(node, "MyPort", PortFlow.Input, new Point(0, 0));
        Assert.Equal("MyPort", port.Name);
    }

    [Fact]
    public void Port_stores_flow()
    {
        var node = new Node();
        var port = new Port(node, "Out", PortFlow.Output, new Point(0, 0));
        Assert.Equal(PortFlow.Output, port.Flow);
    }

    [Fact]
    public void Port_name_defaults_to_empty_string()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        Assert.Equal(string.Empty, port.Name);
    }

    [Fact]
    public void Port_flow_defaults_to_Input()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        Assert.Equal(PortFlow.Input, port.Flow);
    }

    [Fact]
    public void Setting_Label_fires_PropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));

        var changedProps = new List<string?>();
        port.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        port.Label = "Input";

        Assert.Single(changedProps);
        Assert.Equal(nameof(Port.Label), changedProps[0]);
    }

    [Fact]
    public void Setting_Label_to_same_value_does_not_fire_PropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        port.Label = "Input";

        var fired = false;
        port.PropertyChanged += (_, _) => fired = true;

        port.Label = "Input";

        Assert.False(fired);
    }

    [Fact]
    public void MaxConnections_defaults_to_null()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        Assert.Null(port.MaxConnections);
    }

    [Fact]
    public void MaxConnections_can_be_set()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        port.MaxConnections = 3;
        Assert.Equal(3u, port.MaxConnections);
    }

    [Fact]
    public void MaxConnections_fires_PropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        var changedProps = new List<string?>();
        port.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        port.MaxConnections = 1;

        Assert.Contains(nameof(Port.MaxConnections), changedProps);
    }

    [Fact]
    public void MaxConnections_same_value_does_not_fire_PropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        port.MaxConnections = 2;

        var fired = false;
        port.PropertyChanged += (_, _) => fired = true;

        port.MaxConnections = 2;

        Assert.False(fired);
    }

    [Fact]
    public void AbsolutePosition_is_cached_and_invalidated_on_node_move()
    {
        var node = new Node { X = 10, Y = 10 };
        var port = new Port(node, new Point(5, 5));

        // Prime the cache
        var first = port.AbsolutePosition;
        Assert.Equal(new Point(15, 15), first);

        // Move node — cache should be invalidated
        node.X = 100;
        node.Y = 200;

        var second = port.AbsolutePosition;
        Assert.Equal(new Point(105, 205), second);
    }

    [Fact]
    public void AbsolutePosition_is_invalidated_on_position_change()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(10, 10));

        var first = port.AbsolutePosition;
        Assert.Equal(new Point(10, 10), first);

        port.Position = new Point(20, 30);

        var second = port.AbsolutePosition;
        Assert.Equal(new Point(20, 30), second);
    }

    [Fact]
    public void AbsolutePosition_fires_PropertyChanged_when_node_moves()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(5, 5));

        var firedProps = new List<string?>();
        port.PropertyChanged += (_, e) => firedProps.Add(e.PropertyName);

        node.X = 50;

        Assert.Contains(nameof(Port.AbsolutePosition), firedProps);
    }

    [Fact]
    public void AbsolutePosition_fires_PropertyChanged_when_position_changes()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(5, 5));

        var firedProps = new List<string?>();
        port.PropertyChanged += (_, e) => firedProps.Add(e.PropertyName);

        port.Position = new Point(20, 20);

        Assert.Contains(nameof(Port.AbsolutePosition), firedProps);
    }

    [Fact]
    public void Detach_unsubscribes_from_owner()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(5, 5));

        // Prime the cache
        var initial = port.AbsolutePosition;
        Assert.Equal(new Point(5, 5), initial);

        port.Detach();

        var firedProps = new List<string?>();
        port.PropertyChanged += (_, e) => firedProps.Add(e.PropertyName);

        // After detach, moving the node should NOT notify AbsolutePosition
        node.X = 100;

        Assert.DoesNotContain(nameof(Port.AbsolutePosition), firedProps);
    }

    [Fact]
    public void Detach_can_be_called_twice_safely()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));

        // Should not throw
        port.Detach();
        port.Detach();
    }
}

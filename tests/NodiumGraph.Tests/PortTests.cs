using System.ComponentModel;
using NodiumGraph.Model;
using NodiumGraph.Tests.Helpers;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class PortTests
{
    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Ctor_takes_anchor()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "in", PortFlow.Input, PortAnchor.Left(0.5));
        Assert.Equal(PortAnchor.Left(0.5), port.Anchor);
        Assert.Equal("in", port.Name);
        Assert.Equal(PortFlow.Input, port.Flow);
    }

    [Fact]
    public void New_port_has_unique_id()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port1 = new Port(node, "", PortFlow.Input, PortAnchor.Top(0));
        var port2 = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        Assert.NotEqual(port1.Id, port2.Id);
    }

    [Fact]
    public void Port_stores_owner()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));
        Assert.Same(node, port.Owner);
    }

    [Fact]
    public void Port_stores_name()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "MyPort", PortFlow.Input, PortAnchor.Top(0.5));
        Assert.Equal("MyPort", port.Name);
    }

    [Fact]
    public void Port_stores_flow()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "Out", PortFlow.Output, PortAnchor.Right(0.5));
        Assert.Equal(PortFlow.Output, port.Flow);
    }

    [Fact]
    public void Ctor_throws_on_null_owner()
    {
        Assert.Throws<ArgumentNullException>(() => new Port(null!, "", PortFlow.Input, PortAnchor.Top(0)));
    }

    [Fact]
    public void Ctor_throws_on_null_name()
    {
        var node = new Node();
        Assert.Throws<ArgumentNullException>(() => new Port(node, null!, PortFlow.Input, PortAnchor.Top(0)));
    }

    // ── Position (derived) ────────────────────────────────────────────────────

    [Fact]
    public void Position_derived_from_anchor_and_owner_size()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        Assert.Equal(new Point(100, 25), port.Position);
    }

    [Fact]
    public void Position_invalidates_on_Width_change()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        var before = port.Position;   // (100, 25)
        node.Width = 200;
        var after = port.Position;    // (200, 25)
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Position_invalidates_on_Height_change()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Bottom(0.5));
        var before = port.Position;   // (50, 50)
        node.Height = 100;
        var after = port.Position;    // (50, 100)
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Position_fires_INPC_on_Width_change()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        var firedProps = new List<string?>();
        port.PropertyChanged += (_, e) => firedProps.Add(e.PropertyName);
        node.Width = 200;
        Assert.Contains(nameof(Port.Position), firedProps);
    }

    [Fact]
    public void Position_does_not_fire_INPC_on_X_change()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        var firedProps = new List<string?>();
        port.PropertyChanged += (_, e) => firedProps.Add(e.PropertyName);
        node.X = 999;
        Assert.DoesNotContain(nameof(Port.Position), firedProps);
    }

    // ── AbsolutePosition ──────────────────────────────────────────────────────

    [Fact]
    public void AbsolutePosition_adds_owner_position()
    {
        var node = new Node { X = 100, Y = 200, Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        Assert.Equal(new Point(200, 225), port.AbsolutePosition);
    }

    [Fact]
    public void AbsolutePosition_updates_when_node_moves()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));
        Assert.Equal(new Point(0, 25), port.AbsolutePosition);

        node.X = 50;
        node.Y = 75;
        Assert.Equal(new Point(50, 100), port.AbsolutePosition);
    }

    [Fact]
    public void AbsolutePosition_is_cached_and_invalidated_on_node_move()
    {
        var node = new Node { X = 10, Y = 10, Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Top(0.5));

        // Prime the cache
        var first = port.AbsolutePosition;
        Assert.Equal(new Point(60, 10), first);

        // Move node — cache should be invalidated
        node.X = 100;
        node.Y = 200;

        var second = port.AbsolutePosition;
        Assert.Equal(new Point(150, 200), second);
    }

    [Fact]
    public void AbsolutePosition_fires_PropertyChanged_when_node_moves()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));

        var firedProps = new List<string?>();
        port.PropertyChanged += (_, e) => firedProps.Add(e.PropertyName);

        node.X = 50;

        Assert.Contains(nameof(Port.AbsolutePosition), firedProps);
    }

    [Fact]
    public void AbsolutePosition_fires_INPC_on_Width_change()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        var firedProps = new List<string?>();
        port.PropertyChanged += (_, e) => firedProps.Add(e.PropertyName);
        node.Width = 200;
        Assert.Contains(nameof(Port.AbsolutePosition), firedProps);
    }

    // ── EmissionDirection ─────────────────────────────────────────────────────

    [Fact]
    public void EmissionDirection_returns_outward_normal()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        Assert.Equal(new Vector(1, 0), port.EmissionDirection);
    }

    [Fact]
    public void EmissionDirection_fires_INPC_on_Width_change_for_ellipse()
    {
        var node = new Node { Width = 100, Height = 50, Shape = new EllipseShape() };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.25));
        var fired = 0;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Port.EmissionDirection)) fired++;
        };
        node.Width = 200;
        Assert.Equal(1, fired);
    }

    [Fact]
    public void EmissionDirection_does_not_fire_on_X_change()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Top(0.5));
        var fired = 0;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Port.EmissionDirection)) fired++;
        };
        node.X = 999;
        Assert.Equal(0, fired);
    }

    // ── Metadata properties ───────────────────────────────────────────────────

    [Fact]
    public void MaxConnections_defaults_to_null()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));
        Assert.Null(port.MaxConnections);
    }

    [Fact]
    public void MaxConnections_can_be_set()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));
        port.MaxConnections = 3;
        Assert.Equal(3u, port.MaxConnections);
    }

    [Fact]
    public void MaxConnections_fires_PropertyChanged()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));
        var changedProps = new List<string?>();
        port.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        port.MaxConnections = 1;

        Assert.Contains(nameof(Port.MaxConnections), changedProps);
    }

    [Fact]
    public void MaxConnections_same_value_does_not_fire_PropertyChanged()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));
        port.MaxConnections = 2;

        var fired = false;
        port.PropertyChanged += (_, _) => fired = true;

        port.MaxConnections = 2;

        Assert.False(fired);
    }

    [Fact]
    public void Setting_Label_fires_PropertyChanged()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));

        var changedProps = new List<string?>();
        port.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        port.Label = "Input";

        Assert.Single(changedProps);
        Assert.Equal(nameof(Port.Label), changedProps[0]);
    }

    [Fact]
    public void Setting_Label_to_same_value_does_not_fire_PropertyChanged()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));
        port.Label = "Input";

        var fired = false;
        port.PropertyChanged += (_, _) => fired = true;

        port.Label = "Input";

        Assert.False(fired);
    }

    // ── Detach ────────────────────────────────────────────────────────────────

    [Fact]
    public void Detach_unsubscribes_from_owner()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Left(0.5));

        // Prime the cache
        var initial = port.AbsolutePosition;
        Assert.Equal(new Point(0, 25), initial);

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
        var node = new Node { Width = 100, Height = 50 };
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Top(0.5));

        // Should not throw
        port.Detach();
        port.Detach();
    }
}

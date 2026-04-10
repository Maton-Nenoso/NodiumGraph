using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class AnglePortProviderTests
{
    private const double Tolerance = 0.01;

    [Fact]
    public void New_provider_has_empty_ports()
    {
        var provider = new AnglePortProvider();
        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void AddPort_adds_port_to_list()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "A", PortFlow.Input, new Point(0, 0));

        provider.AddPort(port);

        Assert.Single(provider.Ports);
        Assert.Same(port, provider.Ports[0]);
    }

    [Fact]
    public void RemovePort_removes_port_from_list()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "A", PortFlow.Input, new Point(0, 0));

        provider.AddPort(port);
        var removed = provider.RemovePort(port);

        Assert.True(removed);
        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void RemovePort_returns_false_for_unknown_port()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "A", PortFlow.Input, new Point(0, 0));

        Assert.False(provider.RemovePort(port));
    }

    [Fact]
    public void UpdateLayout_positions_port_at_angle_0_to_top_center()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "Top", PortFlow.Input, new Point(0, 0));
        port.Angle = 0;

        provider.AddPort(port);
        provider.UpdateLayout(100, 80, new RectangleShape());

        // Boundary at 0 degrees = (0, -40) center-relative
        // Top-left-relative = (0 + 50, -40 + 40) = (50, 0)
        Assert.Equal(50, port.Position.X, Tolerance);
        Assert.Equal(0, port.Position.Y, Tolerance);
    }

    [Fact]
    public void UpdateLayout_positions_port_at_angle_90_to_right_center()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "Right", PortFlow.Output, new Point(0, 0));
        port.Angle = 90;

        provider.AddPort(port);
        provider.UpdateLayout(100, 80, new RectangleShape());

        // Boundary at 90 degrees = (50, 0) center-relative
        // Top-left-relative = (50 + 50, 0 + 40) = (100, 40)
        Assert.Equal(100, port.Position.X, Tolerance);
        Assert.Equal(40, port.Position.Y, Tolerance);
    }

    [Fact]
    public void UpdateLayout_positions_port_at_angle_180_to_bottom_center()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "Bottom", PortFlow.Output, new Point(0, 0));
        port.Angle = 180;

        provider.AddPort(port);
        provider.UpdateLayout(100, 80, new RectangleShape());

        // Boundary at 180 degrees = (0, 40) center-relative
        // Top-left-relative = (0 + 50, 40 + 40) = (50, 80)
        Assert.Equal(50, port.Position.X, Tolerance);
        Assert.Equal(80, port.Position.Y, Tolerance);
    }

    [Fact]
    public void UpdateLayout_positions_port_at_angle_270_to_left_center()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "Left", PortFlow.Input, new Point(0, 0));
        port.Angle = 270;

        provider.AddPort(port);
        provider.UpdateLayout(100, 80, new RectangleShape());

        // Boundary at 270 degrees = (-50, 0) center-relative
        // Top-left-relative = (-50 + 50, 0 + 40) = (0, 40)
        Assert.Equal(0, port.Position.X, Tolerance);
        Assert.Equal(40, port.Position.Y, Tolerance);
    }

    [Fact]
    public void UpdateLayout_uses_node_shape_parameter()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "E", PortFlow.Input, new Point(0, 0));
        port.Angle = 45;

        provider.AddPort(port);

        // Use ellipse shape
        provider.UpdateLayout(100, 100, new EllipseShape());

        // Ellipse at 45 degrees, 100x100: x = 50*sin(45) = 35.36, y = -50*cos(45) = -35.36
        // Top-left-relative: (35.36 + 50, -35.36 + 50) = (85.36, 14.64)
        Assert.Equal(85.36, port.Position.X, Tolerance);
        Assert.Equal(14.64, port.Position.Y, Tolerance);
    }

    [Fact]
    public void UpdateLayout_uses_default_rectangle_when_shape_is_null()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "N", PortFlow.Input, new Point(0, 0));
        port.Angle = 0;

        provider.AddPort(port);
        provider.UpdateLayout(100, 80, null);

        // Should use default RectangleShape
        Assert.Equal(50, port.Position.X, Tolerance);
        Assert.Equal(0, port.Position.Y, Tolerance);
    }

    [Fact]
    public void UpdateLayout_fires_LayoutInvalidated()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "A", PortFlow.Input, new Point(0, 0));
        provider.AddPort(port);

        var fired = false;
        provider.LayoutInvalidated += () => fired = true;

        provider.UpdateLayout(100, 80, new RectangleShape());

        Assert.True(fired);
    }

    [Fact]
    public void Changing_port_angle_recomputes_position_and_fires_LayoutInvalidated()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "A", PortFlow.Input, new Point(0, 0));
        port.Angle = 0;
        provider.AddPort(port);
        provider.UpdateLayout(100, 80, new RectangleShape());

        var fired = false;
        provider.LayoutInvalidated += () => fired = true;

        // Change angle to 180 (bottom center)
        port.Angle = 180;

        Assert.True(fired);
        Assert.Equal(50, port.Position.X, Tolerance);
        Assert.Equal(80, port.Position.Y, Tolerance);
    }

    [Fact]
    public void RemovePort_unsubscribes_from_PropertyChanged()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "A", PortFlow.Input, new Point(0, 0));
        port.Angle = 0;
        provider.AddPort(port);
        provider.UpdateLayout(100, 80, new RectangleShape());

        provider.RemovePort(port);

        var fired = false;
        provider.LayoutInvalidated += () => fired = true;

        // Changing angle after removal should not fire LayoutInvalidated
        port.Angle = 180;

        Assert.False(fired);
    }

    [Fact]
    public void DistributeEvenly_spaces_ports_equally()
    {
        var provider = new AnglePortProvider();
        var node = new Node();

        var p1 = new Port(node, "A", PortFlow.Input, new Point(0, 0));
        var p2 = new Port(node, "B", PortFlow.Output, new Point(0, 0));
        var p3 = new Port(node, "C", PortFlow.Input, new Point(0, 0));
        var p4 = new Port(node, "D", PortFlow.Output, new Point(0, 0));

        provider.AddPort(p1);
        provider.AddPort(p2);
        provider.AddPort(p3);
        provider.AddPort(p4);

        provider.UpdateLayout(100, 100, new RectangleShape());
        provider.DistributeEvenly();

        // 4 ports at 360/4 = 90 degree intervals
        Assert.Equal(0, p1.Angle, Tolerance);
        Assert.Equal(90, p2.Angle, Tolerance);
        Assert.Equal(180, p3.Angle, Tolerance);
        Assert.Equal(270, p4.Angle, Tolerance);
    }

    [Fact]
    public void DistributeEvenly_with_single_port()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "Only", PortFlow.Input, new Point(0, 0));
        provider.AddPort(port);
        provider.UpdateLayout(100, 100, new RectangleShape());

        provider.DistributeEvenly();

        Assert.Equal(0, port.Angle, Tolerance);
    }

    [Fact]
    public void DistributeEvenly_with_no_ports_does_not_throw()
    {
        var provider = new AnglePortProvider();
        provider.DistributeEvenly(); // Should not throw
    }

    [Fact]
    public void ResolvePort_returns_nearest_within_radius()
    {
        var provider = new AnglePortProvider();
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, "A", PortFlow.Input, new Point(0, 0));
        var port2 = new Port(node, "B", PortFlow.Output, new Point(0, 0));

        port1.Angle = 0;
        port2.Angle = 180;

        provider.AddPort(port1);
        provider.AddPort(port2);
        provider.UpdateLayout(100, 80, new RectangleShape());

        // port1 is at (50, 0) relative -> absolute (50, 0)
        // port2 is at (50, 80) relative -> absolute (50, 80)
        var resolved = provider.ResolvePort(new Point(50, 5));
        Assert.Same(port1, resolved);
    }

    [Fact]
    public void ResolvePort_returns_null_when_outside_radius()
    {
        var provider = new AnglePortProvider();
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, "A", PortFlow.Input, new Point(0, 0));
        port.Angle = 0;

        provider.AddPort(port);
        provider.UpdateLayout(100, 80, new RectangleShape());

        var resolved = provider.ResolvePort(new Point(500, 500));
        Assert.Null(resolved);
    }

    [Fact]
    public void Constructor_throws_on_zero_hitRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnglePortProvider(hitRadius: 0));
    }

    [Fact]
    public void Constructor_throws_on_negative_hitRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnglePortProvider(hitRadius: -5));
    }

    [Fact]
    public void AddPort_throws_on_null()
    {
        var provider = new AnglePortProvider();
        Assert.Throws<ArgumentNullException>(() => provider.AddPort(null!));
    }

    [Fact]
    public void RemovePort_throws_on_null()
    {
        var provider = new AnglePortProvider();
        Assert.Throws<ArgumentNullException>(() => provider.RemovePort(null!));
    }

    [Fact]
    public void UpdateLayout_does_not_move_ports_when_dimensions_are_zero()
    {
        var provider = new AnglePortProvider();
        var node = new Node();
        var port = new Port(node, "A", PortFlow.Input, new Point(42, 42));
        port.Angle = 90;

        provider.AddPort(port);
        provider.UpdateLayout(0, 0, new RectangleShape());

        // Position should remain unchanged when dimensions are zero
        Assert.Equal(42, port.Position.X, Tolerance);
        Assert.Equal(42, port.Position.Y, Tolerance);
    }

    [Fact]
    public void Implements_ILayoutAwarePortProvider()
    {
        var provider = new AnglePortProvider();
        Assert.IsAssignableFrom<ILayoutAwarePortProvider>(provider);
        Assert.IsAssignableFrom<IPortProvider>(provider);
    }
}

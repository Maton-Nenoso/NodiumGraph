using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionRendererTests
{
    private static Connection MakeStraightConnection(double bx = 200, double by = 0)
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = bx, Y = by };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        return new Connection(source, target);
    }

    [AvaloniaFact]
    public void CreateGeometry_returns_world_space_coordinates()
    {
        // Regression gate for the world-space refactor: CreateGeometry must emit
        // geometry in the same coordinate space as the routed points, regardless of
        // any viewport transform in effect. The caller (NodiumGraphCanvas) now owns
        // the viewport push so the cached geometry stays stable across pan/zoom.
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new StraightRouter();
        var style = new ConnectionStyle();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, style);

        // Straight route goes from world (100,25) to world (200,125).
        // Bounds should match world-space, not any screen-space projection.
        var bounds = geometry.Bounds;
        Assert.Equal(100, bounds.X, 3);
        Assert.Equal(25, bounds.Y, 3);
        Assert.Equal(100, bounds.Width, 3);
        Assert.Equal(100, bounds.Height, 3);
    }

    [AvaloniaFact]
    public void CreateGeometry_with_straight_router_returns_non_null()
    {
        var connection = MakeStraightConnection(200, 100);
        var router = new StraightRouter();
        var style = new ConnectionStyle();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, style);

        Assert.NotNull(geometry);
    }

    [AvaloniaFact]
    public void CreateGeometry_with_bezier_router_returns_non_null()
    {
        var connection = MakeStraightConnection(300, 0);
        var router = new BezierRouter();
        var style = new ConnectionStyle();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, style);

        Assert.NotNull(geometry);
    }

    [AvaloniaFact]
    public void CreateGeometry_with_step_router_returns_non_null()
    {
        var connection = MakeStraightConnection(200, 100);
        var router = new StepRouter();
        var style = new ConnectionStyle();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, style);

        Assert.NotNull(geometry);
    }

    [AvaloniaFact]
    public void CreateGeometry_with_step_router_returns_geometry_with_bounds()
    {
        var connection = MakeStraightConnection(200, 100);
        var router = new StepRouter();
        var style = new ConnectionStyle();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, style);

        Assert.True(geometry.Bounds.Width > 0 || geometry.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void CreateGeometry_with_bezier_router_returns_geometry_with_bounds()
    {
        var connection = MakeStraightConnection(300, 0);
        var router = new BezierRouter();
        var style = new ConnectionStyle();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, style);

        Assert.True(geometry.Bounds.Width > 0 || geometry.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void NoneEndpoint_target_produces_same_stroke_as_null_endpoint()
    {
        // Regression gate: NoneEndpoint must be equivalent to null (no endpoint).
        var connection = MakeStraightConnection(200, 0);
        var router = new StraightRouter();

        var nullStyle = new ConnectionStyle();
        var noneStyle = new ConnectionStyle(targetEndpoint: NoneEndpoint.Instance);

        var nullRenderable = ConnectionRenderer.CreateRenderable(connection, router, nullStyle);
        var noneRenderable = ConnectionRenderer.CreateRenderable(connection, router, noneStyle);

        Assert.Equal(nullRenderable.Stroke.Bounds.X, noneRenderable.Stroke.Bounds.X, 3);
        Assert.Equal(nullRenderable.Stroke.Bounds.Y, noneRenderable.Stroke.Bounds.Y, 3);
        Assert.Equal(nullRenderable.Stroke.Bounds.Width, noneRenderable.Stroke.Bounds.Width, 3);
        Assert.Equal(nullRenderable.Stroke.Bounds.Height, noneRenderable.Stroke.Bounds.Height, 3);
    }

    [AvaloniaFact]
    public void ArrowEndpoint_target_adds_to_filled_endpoints_group()
    {
        var connection = MakeStraightConnection(200, 0);
        var router = new StraightRouter();
        var style = new ConnectionStyle(targetEndpoint: new ArrowEndpoint(size: 10, filled: true));

        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);

        Assert.NotNull(renderable.FilledEndpoints);
        var group = Assert.IsType<GeometryGroup>(renderable.FilledEndpoints);
        Assert.Single(group.Children);
        Assert.Null(renderable.OpenEndpoints);

        // Route is straight from (100,25) to (200,25). Target endpoint inset by 10.
        var strokeBounds = renderable.Stroke.Bounds;
        Assert.Equal(100, strokeBounds.X, 3);
        Assert.Equal(25, strokeBounds.Y, 3);
        Assert.Equal(90, strokeBounds.Width, 3); // inset from 100 → 90
    }

    [AvaloniaFact]
    public void BarEndpoint_target_adds_to_open_endpoints_group()
    {
        var connection = MakeStraightConnection(200, 0);
        var router = new StraightRouter();
        // Width/2 + Thickness/2 = 1 + 1 = 2 inset for default thickness 2.
        var style = new ConnectionStyle(
            thickness: 2,
            targetEndpoint: new BarEndpoint(width: 2, length: 12));

        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);

        Assert.Null(renderable.FilledEndpoints);
        Assert.NotNull(renderable.OpenEndpoints);
        var group = Assert.IsType<GeometryGroup>(renderable.OpenEndpoints);
        Assert.Single(group.Children);

        var strokeBounds = renderable.Stroke.Bounds;
        Assert.Equal(100, strokeBounds.X, 3);
        Assert.Equal(98, strokeBounds.Width, 3); // 100 - 2 inset
    }

    [AvaloniaFact]
    public void Both_endpoints_with_mixed_fill_produces_both_groups()
    {
        var connection = MakeStraightConnection(200, 0);
        var router = new StraightRouter();
        var style = new ConnectionStyle(
            sourceEndpoint: new BarEndpoint(),
            targetEndpoint: new ArrowEndpoint());

        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);

        Assert.NotNull(renderable.FilledEndpoints);
        Assert.NotNull(renderable.OpenEndpoints);
        var filled = Assert.IsType<GeometryGroup>(renderable.FilledEndpoints);
        var open = Assert.IsType<GeometryGroup>(renderable.OpenEndpoints);
        Assert.Single(filled.Children);
        Assert.Single(open.Children);
    }

    [AvaloniaFact]
    public void Inset_longer_than_route_is_skipped()
    {
        // Route of effective length 10 with an arrow whose inset is 20 — must not throw
        // and must leave the stroke using the original points.
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 10, Y = 0 };
        var source = new Port(nodeA, new Point(0, 0));
        var target = new Port(nodeB, new Point(0, 0));
        var connection = new Connection(source, target);
        var router = new StraightRouter();
        var style = new ConnectionStyle(targetEndpoint: new ArrowEndpoint(size: 20, filled: true));

        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);

        // Stroke should still span the full 10-unit route (no inset applied because
        // source+target insets would exceed the route length).
        var bounds = renderable.Stroke.Bounds;
        Assert.Equal(0, bounds.X, 3);
        Assert.Equal(10, bounds.Width, 3);
    }

    [AvaloniaFact]
    public void Zero_length_first_segment_skips_source_endpoint()
    {
        // Polyline with coincident p0==p1 — source tangent is degenerate, source endpoint must be skipped.
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 20, Y = 0 };
        var source = new Port(nodeA, new Point(5, 5));
        var target = new Port(nodeB, new Point(0, 5));
        var connection = new Connection(source, target);
        var router = new DegenerateFirstSegmentRouter();
        var style = new ConnectionStyle(
            sourceEndpoint: new ArrowEndpoint(size: 4, filled: true),
            targetEndpoint: new ArrowEndpoint(size: 4, filled: true));

        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);

        // Only target endpoint should appear — source tangent is degenerate.
        Assert.NotNull(renderable.FilledEndpoints);
        var group = Assert.IsType<GeometryGroup>(renderable.FilledEndpoints);
        Assert.Single(group.Children);
        Assert.Null(renderable.OpenEndpoints);
    }

    [AvaloniaFact]
    public void StepRouter_long_path_with_small_endpoints_still_insets()
    {
        // Regression gate for the per-segment inset guard. The old guard used the
        // straight-line p0-to-pN distance, which for a zig-zag polyline is much smaller
        // than the actual polyline length — so a small inset that fits comfortably inside
        // the first/last segments was wrongly rejected. Here: a polyline
        // [(0,0), (100,0), (100,100), (200,100)] with a 5-unit endpoint. Straight-line
        // endpoint distance is ~224, but the guard must look at per-segment lengths (100).
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(0, 0));
        var target = new Port(nodeB, new Point(0, 0));
        var connection = new Connection(source, target);
        var router = new FixedZigZagRouter();
        var style = new ConnectionStyle(targetEndpoint: new ArrowEndpoint(size: 5, filled: true));

        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);

        // Last segment runs from (100,100) to (200,100) along +X. A 5-unit inset must
        // pull pN back from (200,100) to (195,100), shrinking the stroke bounds by 5.
        var strokeBounds = renderable.Stroke.Bounds;
        Assert.Equal(0, strokeBounds.X, 3);
        Assert.Equal(0, strokeBounds.Y, 3);
        Assert.Equal(195, strokeBounds.Width, 3); // 200 - 5 inset along last segment
        Assert.Equal(100, strokeBounds.Height, 3);
    }

    [AvaloniaFact]
    public void Render_with_selected_false_and_null_halo_does_not_throw()
    {
        // Non-selected path must be identical to the Task 7 behavior: no halo pass,
        // just stroke + endpoints. Null haloPen is the legal "nothing selected" signal.
        var connection = MakeStraightConnection(200, 0);
        var router = new StraightRouter();
        var style = new ConnectionStyle(targetEndpoint: new ArrowEndpoint(size: 10, filled: true));
        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);
        var strokePen = new Pen(style.Stroke, style.Thickness);

        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(256, 256));
        using (var ctx = bitmap.CreateDrawingContext())
        {
            ConnectionRenderer.Render(ctx, renderable, style, strokePen, selected: false, haloPen: null);
        }
    }

    [AvaloniaFact]
    public void Render_with_selected_true_and_halo_pen_does_not_throw()
    {
        // Smoke gate for the halo draw path. We pick a style with both a filled and an
        // open endpoint so every branch of the halo pass runs (stroke halo, filled halo,
        // open halo) before the normal stroke+endpoint draws. No recording context —
        // the two contracts we rely on are: no exception and the signature accepts
        // selected=true with a non-null halo pen.
        var connection = MakeStraightConnection(200, 0);
        var router = new StraightRouter();
        var style = new ConnectionStyle(
            sourceEndpoint: new BarEndpoint(),
            targetEndpoint: new ArrowEndpoint(size: 10, filled: true));
        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);
        var strokePen = new Pen(style.Stroke, style.Thickness);
        var haloPen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0x21, 0x96, 0xF3)), style.Thickness + 6);

        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(256, 256));
        using (var ctx = bitmap.CreateDrawingContext())
        {
            ConnectionRenderer.Render(ctx, renderable, style, strokePen, selected: true, haloPen: haloPen);
        }
    }

    [AvaloniaFact]
    public void Render_with_selected_true_but_null_halo_is_noop_for_halo_pass()
    {
        // Degenerate case: selected flag set but no halo pen supplied. The renderer
        // must fall back to the non-selected path rather than NRE — this is the
        // contract the canvas relies on if halo resolution ever fails.
        var connection = MakeStraightConnection(200, 0);
        var router = new StraightRouter();
        var style = new ConnectionStyle();
        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);
        var strokePen = new Pen(style.Stroke, style.Thickness);

        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(256, 256));
        using (var ctx = bitmap.CreateDrawingContext())
        {
            ConnectionRenderer.Render(ctx, renderable, style, strokePen, selected: true, haloPen: null);
        }
    }

    [Fact]
    public void ConnectionSelectionHaloBrushKey_is_defined_and_namespaced()
    {
        // Structural pin for the new theme resource key. Changing the string is a
        // breaking change for any consumer that overrode the halo brush, so we lock
        // the exact name here. Prefix matches the existing NodiumGraph* convention.
        Assert.Equal("NodiumGraphConnectionSelectionHaloBrush", NodiumGraphResources.ConnectionSelectionHaloBrushKey);
        Assert.StartsWith("NodiumGraph", NodiumGraphResources.ConnectionSelectionHaloBrushKey);
    }

    [AvaloniaFact]
    public void ConnectionSelectionHaloBrush_is_defined_in_both_theme_dictionaries()
    {
        // Prove the halo brush is wired up in Generic.axaml for both Light and Dark
        // theme dictionaries. We load the library's generic styles from its avares
        // URI and interrogate the ThemeDictionaries directly — this side-steps the
        // need for a live Application/Window hosting the canvas while still pinning
        // the concrete key → brush mapping in the XAML.
        var styles = (Avalonia.Styling.Styles)Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(
            new System.Uri("avares://NodiumGraph/Themes/Generic.axaml"));

        var dict = styles.Resources;
        Assert.True(dict.ThemeDictionaries.ContainsKey(Avalonia.Styling.ThemeVariant.Light));
        Assert.True(dict.ThemeDictionaries.ContainsKey(Avalonia.Styling.ThemeVariant.Dark));

        var lightDict = (Avalonia.Controls.IResourceDictionary)dict.ThemeDictionaries[Avalonia.Styling.ThemeVariant.Light];
        var darkDict = (Avalonia.Controls.IResourceDictionary)dict.ThemeDictionaries[Avalonia.Styling.ThemeVariant.Dark];

        Assert.True(lightDict.TryGetResource(
            NodiumGraphResources.ConnectionSelectionHaloBrushKey,
            Avalonia.Styling.ThemeVariant.Light,
            out var lightBrush));
        Assert.IsAssignableFrom<IBrush>(lightBrush);

        Assert.True(darkDict.TryGetResource(
            NodiumGraphResources.ConnectionSelectionHaloBrushKey,
            Avalonia.Styling.ThemeVariant.Dark,
            out var darkBrush));
        Assert.IsAssignableFrom<IBrush>(darkBrush);
    }

    private sealed class DegenerateFirstSegmentRouter : IConnectionRouter
    {
        public RouteKind RouteKind => RouteKind.Polyline;

        public IReadOnlyList<Point> Route(Port source, Port target)
        {
            // Emit a path with a zero-length leading segment.
            var p0 = source.AbsolutePosition;
            return new[] { p0, p0, target.AbsolutePosition };
        }
    }

    private sealed class FixedZigZagRouter : IConnectionRouter
    {
        public RouteKind RouteKind => RouteKind.Polyline;

        public IReadOnlyList<Point> Route(Port source, Port target) => new[]
        {
            new Point(0, 0),
            new Point(100, 0),
            new Point(100, 100),
            new Point(200, 100),
        };
    }
}

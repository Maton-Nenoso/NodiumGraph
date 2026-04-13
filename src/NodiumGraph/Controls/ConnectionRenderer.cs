using Avalonia;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

internal static class ConnectionRenderer
{
    public static Geometry CreateGeometry(
        IReadOnlyList<Point> routePoints, RouteKind routeKind, ViewportTransform transform)
    {
        if (routePoints.Count < 2)
            return new StreamGeometry();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(transform.WorldToScreen(routePoints[0]), false);

            if (routeKind == RouteKind.Bezier && routePoints.Count == 4)
            {
                ctx.CubicBezierTo(
                    transform.WorldToScreen(routePoints[1]),
                    transform.WorldToScreen(routePoints[2]),
                    transform.WorldToScreen(routePoints[3]));
            }
            else
            {
                for (var i = 1; i < routePoints.Count; i++)
                    ctx.LineTo(transform.WorldToScreen(routePoints[i]));
            }

            ctx.EndFigure(false);
        }

        return geo;
    }

    public static Geometry CreateGeometry(
        Connection connection, IConnectionRouter router, ViewportTransform transform)
    {
        var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
        return CreateGeometry(routePoints, router.RouteKind, transform);
    }

    public static void Render(
        DrawingContext context,
        IReadOnlyList<Point> routePoints,
        RouteKind routeKind,
        Pen pen,
        ViewportTransform transform)
    {
        var geometry = CreateGeometry(routePoints, routeKind, transform);
        context.DrawGeometry(null, pen, geometry);
    }
}

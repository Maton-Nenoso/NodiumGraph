using Avalonia;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

internal static class ConnectionRenderer
{
    public static Geometry CreateGeometry(
        IReadOnlyList<Point> routePoints, RouteKind routeKind)
    {
        if (routePoints.Count < 2)
            return new StreamGeometry();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(routePoints[0], false);

            if (routeKind == RouteKind.Bezier && routePoints.Count == 4)
            {
                ctx.CubicBezierTo(routePoints[1], routePoints[2], routePoints[3]);
            }
            else
            {
                for (var i = 1; i < routePoints.Count; i++)
                    ctx.LineTo(routePoints[i]);
            }

            ctx.EndFigure(false);
        }

        return geo;
    }

    public static Geometry CreateGeometry(
        Connection connection, IConnectionRouter router)
    {
        var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
        return CreateGeometry(routePoints, router.RouteKind);
    }

    public static void Render(
        DrawingContext context,
        IReadOnlyList<Point> routePoints,
        RouteKind routeKind,
        Pen pen)
    {
        var geometry = CreateGeometry(routePoints, routeKind);
        context.DrawGeometry(null, pen, geometry);
    }
}

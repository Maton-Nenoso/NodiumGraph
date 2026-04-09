using Avalonia;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

internal static class ConnectionRenderer
{
    public static Geometry CreateGeometry(
        Connection connection, IConnectionRouter router, ViewportTransform transform)
    {
        var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
        var screenPoints = new List<Point>(routePoints.Count);
        foreach (var pt in routePoints)
            screenPoints.Add(transform.WorldToScreen(pt));

        if (screenPoints.Count < 2)
            return new LineGeometry();

        // Use bezier only when the router says so AND we have exactly 4 points
        if (router.IsBezierRoute && screenPoints.Count == 4)
        {
            var fig = new PathFigure { StartPoint = screenPoints[0], IsClosed = false };
            fig.Segments!.Add(new BezierSegment
            {
                Point1 = screenPoints[1],
                Point2 = screenPoints[2],
                Point3 = screenPoints[3]
            });
            var geo = new PathGeometry();
            geo.Figures!.Add(fig);
            return geo;
        }

        // Polyline for straight/step/any other route
        var figure = new PathFigure { StartPoint = screenPoints[0], IsClosed = false };
        for (var i = 1; i < screenPoints.Count; i++)
            figure.Segments!.Add(new LineSegment { Point = screenPoints[i] });

        var pathGeo = new PathGeometry();
        pathGeo.Figures!.Add(figure);
        return pathGeo;
    }

    public static void Render(DrawingContext context, Connection connection,
        IConnectionRouter router, Pen pen, ViewportTransform transform)
    {
        var geometry = CreateGeometry(connection, router, transform);
        context.DrawGeometry(null, pen, geometry);
    }
}

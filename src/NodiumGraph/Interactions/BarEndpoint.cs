using System;
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Endpoint renderer that draws a short perpendicular bar at the connection tip (e.g. to indicate
/// a "one" end in an ER-style cardinality marker). The canonical shape is a vertical segment
/// at x = -Width/2 running from -Length/2 to Length/2, rotated to align with the emission direction
/// and translated to the tip. The bar is always stroke-only: <see cref="IsFilled"/> is <c>false</c>.
/// </summary>
public sealed class BarEndpoint : IEndpointRenderer
{
    /// <summary>
    /// Creates a new <see cref="BarEndpoint"/>.
    /// </summary>
    /// <param name="width">Bar thickness along the connection direction, in world units. Must be positive.</param>
    /// <param name="length">Bar length perpendicular to the connection direction, in world units. Must be positive.</param>
    public BarEndpoint(double width = 2, double length = 12)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        Width = width;
        Length = length;
    }

    /// <summary>Bar thickness along the connection direction, in world units.</summary>
    public double Width { get; }

    /// <summary>Bar length perpendicular to the connection direction, in world units.</summary>
    public double Length { get; }

    /// <inheritdoc />
    // The bar is an open line segment; it never fills.
    public bool IsFilled => false;

    /// <inheritdoc />
    // Bar is centered on its width; the connection line meets the middle of the bar.
    public double GetInset(double strokeThickness) => Width / 2;

    /// <inheritdoc />
    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)
    {
        if (direction.X == 0 && direction.Y == 0)
            throw new ArgumentException("direction must be non-zero", nameof(direction));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(-Width / 2, -Length / 2), isFilled: false);
            ctx.LineTo(new Point(-Width / 2, Length / 2));
            ctx.EndFigure(false);
        }

        var rotation = Matrix.CreateRotation(Math.Atan2(direction.Y, direction.X));
        var translation = Matrix.CreateTranslation(tip.X, tip.Y);
        geo.Transform = new MatrixTransform(rotation * translation);

        return geo;
    }
}

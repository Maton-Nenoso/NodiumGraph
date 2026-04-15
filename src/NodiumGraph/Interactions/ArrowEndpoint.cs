using System;
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Endpoint renderer that draws a triangular arrow head at the connection tip.
/// The canonical shape is a triangle pointing toward +X, rotated to align with the emission direction
/// and translated to the tip. Filled arrows use a closed figure; open arrows draw only the two sides,
/// meeting at the apex.
/// </summary>
public sealed class ArrowEndpoint : IEndpointRenderer
{
    private readonly double _size;

    /// <summary>
    /// Creates a new <see cref="ArrowEndpoint"/>.
    /// </summary>
    /// <param name="size">Arrow length (from base to tip) in world units. Must be positive.</param>
    /// <param name="filled">When true the arrow is rendered as a closed, filled triangle; otherwise as an open chevron.</param>
    public ArrowEndpoint(double size = 8, bool filled = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        _size = size;
        IsFilled = filled;
    }

    /// <inheritdoc />
    public bool IsFilled { get; }

    /// <inheritdoc />
    public double GetInset(double strokeThickness) => IsFilled ? _size : _size * 0.9;

    /// <inheritdoc />
    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, 0), IsFilled);
            ctx.LineTo(new Point(-_size, -_size / 2));
            ctx.LineTo(new Point(-_size, _size / 2));
            ctx.EndFigure(IsFilled);
        }

        var rotation = Matrix.CreateRotation(Math.Atan2(direction.Y, direction.X));
        var translation = Matrix.CreateTranslation(tip.X, tip.Y);
        geo.Transform = new MatrixTransform(rotation * translation);

        return geo;
    }
}

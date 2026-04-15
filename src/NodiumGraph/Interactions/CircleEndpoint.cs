using System;
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Endpoint renderer that draws a circle at the connection tip. The canonical shape has the
/// circle's front edge (+X side) touching the origin so the "tip" is the port center; the circle's
/// center sits at (-Radius, 0). It is rotated to align with the emission direction and translated
/// to the tip. <see cref="IsFilled"/> is used downstream by the renderer to choose fill-vs-stroke;
/// the geometry itself is always a closed ellipse.
/// </summary>
public sealed class CircleEndpoint : IEndpointRenderer
{
    /// <summary>
    /// Creates a new <see cref="CircleEndpoint"/>.
    /// </summary>
    /// <param name="radius">Circle radius in world units. Must be positive.</param>
    /// <param name="filled">When true the circle is rendered filled; otherwise as a stroked outline.</param>
    public CircleEndpoint(double radius = 5, bool filled = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        Radius = radius;
        IsFilled = filled;
    }

    /// <summary>Circle radius in world units.</summary>
    public double Radius { get; }

    /// <inheritdoc />
    public bool IsFilled { get; }

    /// <inheritdoc />
    // Inset is the diameter: the connection line meets the far side of the circle from the tip.
    public double GetInset(double strokeThickness) => Radius * 2;

    /// <inheritdoc />
    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)
    {
        if (direction.X == 0 && direction.Y == 0)
            throw new ArgumentException("direction must be non-zero", nameof(direction));

        // Bounding box: center at (-Radius, 0), diameter = 2*Radius. Front edge touches origin.
        var geo = new EllipseGeometry(new Rect(-Radius * 2, -Radius, Radius * 2, Radius * 2));

        var rotation = Matrix.CreateRotation(Math.Atan2(direction.Y, direction.X));
        var translation = Matrix.CreateTranslation(tip.X, tip.Y);
        geo.Transform = new MatrixTransform(rotation * translation);

        return geo;
    }
}

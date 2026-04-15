using System;
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Endpoint renderer that draws a diamond (rhombus) at the connection tip.
/// The canonical shape has its front vertex at the origin pointing toward +X, with the back vertex
/// at (-Size, 0), top at (-Size/2, -Size/2), and bottom at (-Size/2, Size/2). The shape is rotated
/// to align with the emission direction and translated to the tip.
/// </summary>
public sealed class DiamondEndpoint : IEndpointRenderer
{
    /// <summary>
    /// Creates a new <see cref="DiamondEndpoint"/>.
    /// </summary>
    /// <param name="size">Diamond length (from front tip to back vertex) in world units. Must be positive.</param>
    /// <param name="filled">When true the diamond is rendered as a closed, filled shape; otherwise as an open outline.</param>
    public DiamondEndpoint(double size = 10, bool filled = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        Size = size;
        IsFilled = filled;
    }

    /// <summary>Diamond length (from front tip to back vertex) in world units.</summary>
    public double Size { get; }

    /// <inheritdoc />
    public bool IsFilled { get; }

    /// <inheritdoc />
    // Diamond inset is the full length: the back vertex sits at the base, not recessed.
    public double GetInset(double strokeThickness) => Size;

    /// <inheritdoc />
    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)
    {
        if (direction.X == 0 && direction.Y == 0)
            throw new ArgumentException("direction must be non-zero", nameof(direction));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, 0), IsFilled);
            ctx.LineTo(new Point(-Size / 2, -Size / 2));
            ctx.LineTo(new Point(-Size, 0));
            ctx.LineTo(new Point(-Size / 2, Size / 2));
            ctx.EndFigure(IsFilled);
        }

        var rotation = Matrix.CreateRotation(Math.Atan2(direction.Y, direction.X));
        var translation = Matrix.CreateTranslation(tip.X, tip.Y);
        geo.Transform = new MatrixTransform(rotation * translation);

        return geo;
    }
}

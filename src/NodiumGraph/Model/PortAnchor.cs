using System;

namespace NodiumGraph.Model;

public readonly record struct PortAnchor(PortEdge Edge, double Fraction)
{
    public PortEdge Edge { get; } = ValidateEdge(Edge);
    public double Fraction { get; } = ValidateFraction(Fraction);

    private static PortEdge ValidateEdge(PortEdge edge) => edge switch
    {
        PortEdge.Left or PortEdge.Top or PortEdge.Right or PortEdge.Bottom => edge,
        _ => throw new ArgumentOutOfRangeException(nameof(Edge), $"Invalid PortEdge value: {(int)edge}."),
    };

    private static double ValidateFraction(double f)
    {
        if (double.IsNaN(f) || f < 0.0 || f > 1.0)
            throw new ArgumentOutOfRangeException(nameof(Fraction), "Must be in [0, 1].");
        return f;
    }

    public static PortAnchor Left(double f)   => new(PortEdge.Left,   f);
    public static PortAnchor Top(double f)    => new(PortEdge.Top,    f);
    public static PortAnchor Right(double f)  => new(PortEdge.Right,  f);
    public static PortAnchor Bottom(double f) => new(PortEdge.Bottom, f);
}

using Avalonia;

namespace NodiumGraph.Controls;

/// <summary>
/// Converts between world coordinates (where nodes live) and screen coordinates (pixels).
/// Transform: screen = world * zoom + offset
/// </summary>
public readonly struct ViewportTransform(double zoom, Point offset)
{
    public double Zoom { get; } = zoom;
    public Point Offset { get; } = offset;

    public Point WorldToScreen(Point world) =>
        new(world.X * Zoom + Offset.X, world.Y * Zoom + Offset.Y);

    public Point ScreenToWorld(Point screen)
    {
        if (Zoom == 0) return screen;
        return new((screen.X - Offset.X) / Zoom, (screen.Y - Offset.Y) / Zoom);
    }

    public double WorldToScreen(double worldLength) => worldLength * Zoom;

    public double ScreenToWorld(double screenLength)
    {
        if (Zoom == 0) return screenLength;
        return screenLength / Zoom;
    }
}

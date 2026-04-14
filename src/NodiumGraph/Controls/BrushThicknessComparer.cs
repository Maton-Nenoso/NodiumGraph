using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace NodiumGraph.Controls;

/// <summary>
/// Equality comparer for (brush, thickness) keys used by pen caches.
/// Brush is compared by reference identity so in-place brush mutation invalidates cache hits.
/// </summary>
internal sealed class BrushThicknessComparer
    : IEqualityComparer<(IBrush brush, double thickness)>
{
    public static readonly BrushThicknessComparer Instance = new();

    // double.Equals (not ==) avoids an analyzer warning on float equality.
    // Thickness is never NaN here, so NaN-self-equal semantics don't matter.
    public bool Equals((IBrush brush, double thickness) x, (IBrush brush, double thickness) y)
        => ReferenceEquals(x.brush, y.brush) && x.thickness.Equals(y.thickness);

    public int GetHashCode((IBrush brush, double thickness) obj)
        => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.brush), obj.thickness);
}

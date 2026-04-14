using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace NodiumGraph.Controls;

/// <summary>
/// Equality comparer for (label, bucketedFontSize, brush) keys used by the FormattedText label cache.
/// Brush is compared by reference identity so in-place brush mutation invalidates cache hits.
/// </summary>
internal sealed class LabelCacheKeyComparer
    : IEqualityComparer<(string label, double bucketedFontSize, IBrush brush)>
{
    public static readonly LabelCacheKeyComparer Instance = new();

    public bool Equals(
        (string label, double bucketedFontSize, IBrush brush) x,
        (string label, double bucketedFontSize, IBrush brush) y)
        => ReferenceEquals(x.brush, y.brush)
            && x.bucketedFontSize.Equals(y.bucketedFontSize)
            && string.Equals(x.label, y.label, StringComparison.Ordinal);

    public int GetHashCode((string label, double bucketedFontSize, IBrush brush) obj)
        => HashCode.Combine(
            obj.label,
            obj.bucketedFontSize,
            RuntimeHelpers.GetHashCode(obj.brush));
}

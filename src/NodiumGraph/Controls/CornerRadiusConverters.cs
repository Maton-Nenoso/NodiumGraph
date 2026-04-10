using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace NodiumGraph.Controls;

/// <summary>
/// Converters for extracting partial CornerRadius values (e.g. top-only for headers).
/// </summary>
public static class CornerRadiusConverters
{
    /// <summary>
    /// Converts a CornerRadius to one with only the top corners, zeroing bottom corners.
    /// </summary>
    public static readonly IValueConverter TopOnly = new TopOnlyConverter();

    private sealed class TopOnlyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is CornerRadius cr)
                return new CornerRadius(cr.TopLeft, cr.TopRight, 0, 0);
            return new CornerRadius(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

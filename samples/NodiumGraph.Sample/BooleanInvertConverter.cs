using System.Globalization;
using Avalonia.Data.Converters;

namespace NodiumGraph.Sample;

public class BooleanInvertConverter : IValueConverter
{
    public static readonly BooleanInvertConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Model;

/// <summary>
/// Per-instance visual overrides for a node. All properties are nullable —
/// null means "fall through to theme resource, then to hardcoded default."
/// </summary>
public class NodeStyle : INotifyPropertyChanged
{
    private IBrush? _headerBackground;
    private IBrush? _bodyBackground;
    private IBrush? _borderBrush;
    private double? _borderThickness;
    private double? _opacity;
    private CornerRadius? _cornerRadius;

    /// <summary>
    /// Background brush for the node header area.
    /// </summary>
    public IBrush? HeaderBackground
    {
        get => _headerBackground;
        set => SetField(ref _headerBackground, value);
    }

    /// <summary>
    /// Background brush for the node body area.
    /// </summary>
    public IBrush? BodyBackground
    {
        get => _bodyBackground;
        set => SetField(ref _bodyBackground, value);
    }

    /// <summary>
    /// Brush for the node border.
    /// </summary>
    public IBrush? BorderBrush
    {
        get => _borderBrush;
        set => SetField(ref _borderBrush, value);
    }

    /// <summary>
    /// Thickness of the node border.
    /// </summary>
    public double? BorderThickness
    {
        get => _borderThickness;
        set => SetField(ref _borderThickness, value);
    }

    /// <summary>
    /// Overall opacity of the node visual.
    /// </summary>
    public double? Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, value);
    }

    /// <summary>
    /// Corner radius for the node border.
    /// </summary>
    public CornerRadius? CornerRadius
    {
        get => _cornerRadius;
        set => SetField(ref _cornerRadius, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

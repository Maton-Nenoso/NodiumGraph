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
    private IBrush? _headerForeground;
    private IBrush? _bodyBackground;
    private IBrush? _borderBrush;
    private double? _borderThickness;
    private double? _opacity;
    private CornerRadius? _cornerRadius;
    private double? _headerFontSize;
    private FontWeight? _headerFontWeight;
    private FontFamily? _headerFontFamily;
    private Thickness? _headerPadding;
    private double? _bodyMinHeight;
    private double? _minWidth;
    private IBrush? _selectionBorderBrush;
    private double? _selectionBorderThickness;
    private IBrush? _hoverBorderBrush;
    private double? _hoverBorderThickness;

    /// <summary>
    /// Background brush for the node header area.
    /// </summary>
    public IBrush? HeaderBackground
    {
        get => _headerBackground;
        set => SetField(ref _headerBackground, value);
    }

    /// <summary>
    /// Foreground brush for the node header text.
    /// </summary>
    public IBrush? HeaderForeground
    {
        get => _headerForeground;
        set => SetField(ref _headerForeground, value);
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

    /// <summary>
    /// Font size for the node header text. Default template uses 12.
    /// </summary>
    public double? HeaderFontSize
    {
        get => _headerFontSize;
        set => SetField(ref _headerFontSize, value);
    }

    /// <summary>
    /// Font weight for the node header text. Default template uses SemiBold.
    /// </summary>
    public FontWeight? HeaderFontWeight
    {
        get => _headerFontWeight;
        set => SetField(ref _headerFontWeight, value);
    }

    /// <summary>
    /// Font family for the node header text. Default template uses system default.
    /// </summary>
    public FontFamily? HeaderFontFamily
    {
        get => _headerFontFamily;
        set => SetField(ref _headerFontFamily, value);
    }

    /// <summary>
    /// Padding for the node header area. Default template uses (8, 4).
    /// </summary>
    public Thickness? HeaderPadding
    {
        get => _headerPadding;
        set => SetField(ref _headerPadding, value);
    }

    /// <summary>
    /// Minimum height for the node body area. Default template uses 4.
    /// </summary>
    public double? BodyMinHeight
    {
        get => _bodyMinHeight;
        set => SetField(ref _bodyMinHeight, value);
    }

    /// <summary>
    /// Minimum width for the node. Default template uses 120.
    /// </summary>
    public double? MinWidth
    {
        get => _minWidth;
        set => SetField(ref _minWidth, value);
    }

    /// <summary>
    /// Brush for the selection border. Overrides theme resource when set.
    /// </summary>
    public IBrush? SelectionBorderBrush
    {
        get => _selectionBorderBrush;
        set => SetField(ref _selectionBorderBrush, value);
    }

    /// <summary>
    /// Thickness of the selection border. Default is 2.
    /// </summary>
    public double? SelectionBorderThickness
    {
        get => _selectionBorderThickness;
        set => SetField(ref _selectionBorderThickness, value);
    }

    /// <summary>
    /// Brush for the hover border. Overrides theme resource when set.
    /// </summary>
    public IBrush? HoverBorderBrush
    {
        get => _hoverBorderBrush;
        set => SetField(ref _hoverBorderBrush, value);
    }

    /// <summary>
    /// Thickness of the hover border. Default is 1.5.
    /// </summary>
    public double? HoverBorderThickness
    {
        get => _hoverBorderThickness;
        set => SetField(ref _hoverBorderThickness, value);
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

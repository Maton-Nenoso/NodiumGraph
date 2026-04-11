using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace NodiumGraph.Model;

/// <summary>
/// Controls where a port label is rendered relative to the port visual.
/// </summary>
public enum PortLabelPlacement
{
    Left,
    Right,
    Above,
    Below
}

/// <summary>
/// Per-instance visual overrides for a port. All properties are nullable —
/// null means "fall through to theme resource, then to hardcoded default."
/// </summary>
public class PortStyle : INotifyPropertyChanged
{
    private IBrush? _fill;
    private IBrush? _stroke;
    private double? _strokeWidth;
    private PortShape? _shape;
    private double? _size;
    private double? _labelFontSize;
    private IBrush? _labelBrush;
    private double? _labelOffset;
    private PortLabelPlacement? _labelPlacement;

    /// <summary>
    /// Fill brush for the port.
    /// </summary>
    public IBrush? Fill
    {
        get => _fill;
        set => SetField(ref _fill, value);
    }

    /// <summary>
    /// Stroke brush for the port outline.
    /// </summary>
    public IBrush? Stroke
    {
        get => _stroke;
        set => SetField(ref _stroke, value);
    }

    /// <summary>
    /// Width of the port outline stroke.
    /// </summary>
    public double? StrokeWidth
    {
        get => _strokeWidth;
        set => SetField(ref _strokeWidth, value);
    }

    /// <summary>
    /// Visual shape of the port (circle, square, diamond, triangle).
    /// </summary>
    public PortShape? Shape
    {
        get => _shape;
        set => SetField(ref _shape, value);
    }

    /// <summary>
    /// Size (radius for circle, half-side for square/diamond/triangle) of the port.
    /// </summary>
    public double? Size
    {
        get => _size;
        set => SetField(ref _size, value);
    }

    /// <summary>
    /// Font size for the port label. Default is 11.
    /// </summary>
    public double? LabelFontSize
    {
        get => _labelFontSize;
        set => SetField(ref _labelFontSize, value);
    }

    /// <summary>
    /// Brush for the port label text. Overrides theme resource when set.
    /// </summary>
    public IBrush? LabelBrush
    {
        get => _labelBrush;
        set => SetField(ref _labelBrush, value);
    }

    /// <summary>
    /// Offset distance of the label from the port center. Default is 8.
    /// </summary>
    public double? LabelOffset
    {
        get => _labelOffset;
        set => SetField(ref _labelOffset, value);
    }

    /// <summary>
    /// Where the label is placed relative to the port visual.
    /// When null, placement is determined by position heuristic (left/right of node center).
    /// </summary>
    public PortLabelPlacement? LabelPlacement
    {
        get => _labelPlacement;
        set => SetField(ref _labelPlacement, value);
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

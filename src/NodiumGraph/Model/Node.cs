using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NodiumGraph.Model;

/// <summary>
/// A visual node in the graph. Subclass to attach domain data.
/// Width and Height are set internally by the canvas after measure.
/// </summary>
public class Node : INotifyPropertyChanged
{
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private string _title;
    private bool _isSelected;
    private bool _showHeader = true;
    private NodeStyle? _style;

    public Node()
    {
        _title = GetType().Name;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public double Width
    {
        get => _width;
        internal set => SetField(ref _width, value);
    }

    public double Height
    {
        get => _height;
        internal set => SetField(ref _height, value);
    }

    public IPortProvider? PortProvider { get; set; }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetField(ref _isSelected, value);
    }

    /// <summary>
    /// Controls whether the default template renders the header bar.
    /// When false, the header is hidden and node height shrinks naturally.
    /// Title remains unchanged regardless of this value.
    /// </summary>
    public bool ShowHeader
    {
        get => _showHeader;
        set => SetField(ref _showHeader, value);
    }

    /// <summary>
    /// Per-instance visual overrides. Null properties fall through to theme, then default.
    /// </summary>
    /// <remarks>
    /// Style properties are applied when the node's DataTemplate is first created.
    /// Changing style properties at runtime will not automatically update the node's
    /// visuals — the consumer must force a template rebuild (e.g., by removing and
    /// re-adding the node) for changes to take effect. This is a known limitation
    /// of the FuncDataTemplate approach.
    /// </remarks>
    public NodeStyle? Style
    {
        get => _style;
        set => SetField(ref _style, value);
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

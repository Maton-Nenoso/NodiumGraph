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

    public double Width { get; internal set; }
    public double Height { get; internal set; }

    public IPortProvider? PortProvider { get; set; }

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

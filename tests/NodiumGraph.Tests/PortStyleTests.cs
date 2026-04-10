using System.ComponentModel;
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class PortStyleTests
{
    [Fact]
    public void All_properties_default_to_null()
    {
        var style = new PortStyle();
        Assert.Null(style.Fill);
        Assert.Null(style.Stroke);
        Assert.Null(style.StrokeWidth);
        Assert.Null(style.Shape);
        Assert.Null(style.Size);
    }

    [Fact]
    public void Fill_set_and_get()
    {
        var style = new PortStyle { Fill = Brushes.Red };
        Assert.Same(Brushes.Red, style.Fill);
    }

    [Fact]
    public void Stroke_set_and_get()
    {
        var style = new PortStyle { Stroke = Brushes.White };
        Assert.Same(Brushes.White, style.Stroke);
    }

    [Fact]
    public void StrokeWidth_set_and_get()
    {
        var style = new PortStyle { StrokeWidth = 2.0 };
        Assert.Equal(2.0, style.StrokeWidth);
    }

    [Fact]
    public void Shape_set_and_get()
    {
        var style = new PortStyle { Shape = PortShape.Diamond };
        Assert.Equal(PortShape.Diamond, style.Shape);
    }

    [Fact]
    public void Size_set_and_get()
    {
        var style = new PortStyle { Size = 6.0 };
        Assert.Equal(6.0, style.Size);
    }

    [Theory]
    [InlineData(nameof(PortStyle.Fill))]
    [InlineData(nameof(PortStyle.Stroke))]
    [InlineData(nameof(PortStyle.StrokeWidth))]
    [InlineData(nameof(PortStyle.Shape))]
    [InlineData(nameof(PortStyle.Size))]
    public void Setting_property_fires_PropertyChanged(string propertyName)
    {
        var style = new PortStyle();
        var firedNames = new List<string>();
        ((INotifyPropertyChanged)style).PropertyChanged += (_, e) => firedNames.Add(e.PropertyName!);

        switch (propertyName)
        {
            case nameof(PortStyle.Fill):
                style.Fill = Brushes.Red;
                break;
            case nameof(PortStyle.Stroke):
                style.Stroke = Brushes.White;
                break;
            case nameof(PortStyle.StrokeWidth):
                style.StrokeWidth = 2.0;
                break;
            case nameof(PortStyle.Shape):
                style.Shape = PortShape.Square;
                break;
            case nameof(PortStyle.Size):
                style.Size = 8.0;
                break;
        }

        Assert.Contains(propertyName, firedNames);
    }

    [Fact]
    public void Setting_same_value_does_not_fire_PropertyChanged()
    {
        var style = new PortStyle { StrokeWidth = 2.0 };
        var fired = false;
        ((INotifyPropertyChanged)style).PropertyChanged += (_, _) => fired = true;

        style.StrokeWidth = 2.0;
        Assert.False(fired);
    }

    [Fact]
    public void Port_Style_property_defaults_to_null()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        Assert.Null(port.Style);
    }

    [Fact]
    public void Port_Style_set_and_get()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        var style = new PortStyle { Fill = Brushes.Red };
        port.Style = style;
        Assert.Same(style, port.Style);
    }

    [Fact]
    public void Port_Style_fires_PropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        var fired = false;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Port.Style)) fired = true;
        };

        port.Style = new PortStyle();
        Assert.True(fired);
    }

    [Fact]
    public void Port_implements_INotifyPropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        Assert.IsAssignableFrom<INotifyPropertyChanged>(port);
    }

    [Fact]
    public void Port_Position_change_fires_PropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        var fired = false;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Port.Position)) fired = true;
        };

        port.Position = new Point(10, 20);
        Assert.True(fired);
    }

    [Fact]
    public void Port_Position_same_value_does_not_fire()
    {
        var node = new Node();
        var port = new Port(node, new Point(5, 10));
        var fired = false;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, _) => fired = true;

        port.Position = new Point(5, 10);
        Assert.False(fired);
    }

    [Fact]
    public void PortShape_enum_has_expected_values()
    {
        Assert.Equal(0, (int)PortShape.Circle);
        Assert.Equal(1, (int)PortShape.Square);
        Assert.Equal(2, (int)PortShape.Diamond);
        Assert.Equal(3, (int)PortShape.Triangle);
    }

    [Fact]
    public void Setting_same_Style_does_not_fire_PropertyChanged()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        var style = new PortStyle();
        port.Style = style;

        var fired = false;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, _) => fired = true;

        port.Style = style;
        Assert.False(fired);
    }
}

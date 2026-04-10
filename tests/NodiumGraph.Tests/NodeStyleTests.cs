using System.ComponentModel;
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodeStyleTests
{
    [Fact]
    public void All_properties_default_to_null()
    {
        var style = new NodeStyle();
        Assert.Null(style.HeaderBackground);
        Assert.Null(style.HeaderForeground);
        Assert.Null(style.BodyBackground);
        Assert.Null(style.BorderBrush);
        Assert.Null(style.BorderThickness);
        Assert.Null(style.Opacity);
        Assert.Null(style.CornerRadius);
    }

    [Fact]
    public void HeaderBackground_set_and_get()
    {
        var style = new NodeStyle { HeaderBackground = Brushes.Red };
        Assert.Same(Brushes.Red, style.HeaderBackground);
    }

    [Fact]
    public void HeaderForeground_set_and_get()
    {
        var style = new NodeStyle { HeaderForeground = Brushes.Yellow };
        Assert.Same(Brushes.Yellow, style.HeaderForeground);
    }

    [Fact]
    public void BodyBackground_set_and_get()
    {
        var style = new NodeStyle { BodyBackground = Brushes.Blue };
        Assert.Same(Brushes.Blue, style.BodyBackground);
    }

    [Fact]
    public void BorderBrush_set_and_get()
    {
        var style = new NodeStyle { BorderBrush = Brushes.Green };
        Assert.Same(Brushes.Green, style.BorderBrush);
    }

    [Fact]
    public void BorderThickness_set_and_get()
    {
        var style = new NodeStyle { BorderThickness = 2.5 };
        Assert.Equal(2.5, style.BorderThickness);
    }

    [Fact]
    public void Opacity_set_and_get()
    {
        var style = new NodeStyle { Opacity = 0.75 };
        Assert.Equal(0.75, style.Opacity);
    }

    [Fact]
    public void CornerRadius_set_and_get()
    {
        var cr = new CornerRadius(10);
        var style = new NodeStyle { CornerRadius = cr };
        Assert.Equal(cr, style.CornerRadius);
    }

    [Theory]
    [InlineData(nameof(NodeStyle.HeaderBackground))]
    [InlineData(nameof(NodeStyle.HeaderForeground))]
    [InlineData(nameof(NodeStyle.BodyBackground))]
    [InlineData(nameof(NodeStyle.BorderBrush))]
    [InlineData(nameof(NodeStyle.BorderThickness))]
    [InlineData(nameof(NodeStyle.Opacity))]
    [InlineData(nameof(NodeStyle.CornerRadius))]
    public void Setting_property_fires_PropertyChanged(string propertyName)
    {
        var style = new NodeStyle();
        var firedNames = new List<string>();
        ((INotifyPropertyChanged)style).PropertyChanged += (_, e) => firedNames.Add(e.PropertyName!);

        switch (propertyName)
        {
            case nameof(NodeStyle.HeaderBackground):
                style.HeaderBackground = Brushes.Red;
                break;
            case nameof(NodeStyle.HeaderForeground):
                style.HeaderForeground = Brushes.Yellow;
                break;
            case nameof(NodeStyle.BodyBackground):
                style.BodyBackground = Brushes.Blue;
                break;
            case nameof(NodeStyle.BorderBrush):
                style.BorderBrush = Brushes.Green;
                break;
            case nameof(NodeStyle.BorderThickness):
                style.BorderThickness = 2.0;
                break;
            case nameof(NodeStyle.Opacity):
                style.Opacity = 0.5;
                break;
            case nameof(NodeStyle.CornerRadius):
                style.CornerRadius = new CornerRadius(8);
                break;
        }

        Assert.Contains(propertyName, firedNames);
    }

    [Fact]
    public void Setting_same_value_does_not_fire_PropertyChanged()
    {
        var style = new NodeStyle { BorderThickness = 2.0 };
        var fired = false;
        ((INotifyPropertyChanged)style).PropertyChanged += (_, _) => fired = true;

        style.BorderThickness = 2.0;
        Assert.False(fired);
    }

    [Fact]
    public void Node_Style_property_defaults_to_null()
    {
        var node = new Node();
        Assert.Null(node.Style);
    }

    [Fact]
    public void Node_Style_set_and_get()
    {
        var node = new Node();
        var style = new NodeStyle { HeaderBackground = Brushes.Red };
        node.Style = style;
        Assert.Same(style, node.Style);
    }

    [Fact]
    public void Node_Style_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.Style)) fired = true;
        };

        node.Style = new NodeStyle();
        Assert.True(fired);
    }

    [Fact]
    public void Setting_same_Style_does_not_fire_PropertyChanged()
    {
        var style = new NodeStyle();
        var node = new Node { Style = style };
        var fired = false;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, _) => fired = true;

        node.Style = style;
        Assert.False(fired);
    }
}

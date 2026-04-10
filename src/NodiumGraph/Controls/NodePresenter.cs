using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NodiumGraph.Controls;

/// <summary>
/// A ContentControl that provides standard node chrome (header, border, shadow, collapse toggle).
/// The consumer only defines the body content. Use inside a DataTemplate:
/// <code>
/// &lt;DataTemplate DataType="local:MyNode"&gt;
///     &lt;ng:NodePresenter HeaderBackground="#10B981"&gt;
///         &lt;TextBlock Text="Body content here" /&gt;
///     &lt;/ng:NodePresenter&gt;
/// &lt;/DataTemplate&gt;
/// </code>
/// </summary>
public class NodePresenter : ContentControl
{
    // --- Header appearance ---

    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<NodePresenter, IBrush?>(nameof(HeaderBackground));

    public static readonly StyledProperty<IBrush?> HeaderForegroundProperty =
        AvaloniaProperty.Register<NodePresenter, IBrush?>(nameof(HeaderForeground));

    public static readonly StyledProperty<double> HeaderFontSizeProperty =
        AvaloniaProperty.Register<NodePresenter, double>(nameof(HeaderFontSize), 12);

    public static readonly StyledProperty<FontWeight> HeaderFontWeightProperty =
        AvaloniaProperty.Register<NodePresenter, FontWeight>(nameof(HeaderFontWeight), FontWeight.SemiBold);

    public static readonly StyledProperty<FontFamily> HeaderFontFamilyProperty =
        AvaloniaProperty.Register<NodePresenter, FontFamily>(nameof(HeaderFontFamily), FontFamily.Default);

    public static readonly StyledProperty<Thickness> HeaderPaddingProperty =
        AvaloniaProperty.Register<NodePresenter, Thickness>(nameof(HeaderPadding), new Thickness(10, 6));

    // --- Outer border ---

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        Border.BoxShadowProperty.AddOwner<NodePresenter>();

    // --- Collapse toggle ---

    public static readonly StyledProperty<IBrush?> CollapseToggleForegroundProperty =
        AvaloniaProperty.Register<NodePresenter, IBrush?>(nameof(CollapseToggleForeground));

    /// <summary>Background brush for the header bar.</summary>
    public IBrush? HeaderBackground
    {
        get => GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>Foreground brush for the header text.</summary>
    public IBrush? HeaderForeground
    {
        get => GetValue(HeaderForegroundProperty);
        set => SetValue(HeaderForegroundProperty, value);
    }

    /// <summary>Font size for the header text. Default: 12.</summary>
    public double HeaderFontSize
    {
        get => GetValue(HeaderFontSizeProperty);
        set => SetValue(HeaderFontSizeProperty, value);
    }

    /// <summary>Font weight for the header text. Default: SemiBold.</summary>
    public FontWeight HeaderFontWeight
    {
        get => GetValue(HeaderFontWeightProperty);
        set => SetValue(HeaderFontWeightProperty, value);
    }

    /// <summary>Font family for the header text. Default: system default.</summary>
    public FontFamily HeaderFontFamily
    {
        get => GetValue(HeaderFontFamilyProperty);
        set => SetValue(HeaderFontFamilyProperty, value);
    }

    /// <summary>Padding inside the header bar. Default: 10,6.</summary>
    public Thickness HeaderPadding
    {
        get => GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    /// <summary>Box shadow for card elevation effect.</summary>
    public BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }

    /// <summary>Foreground brush for the collapse toggle arrows.</summary>
    public IBrush? CollapseToggleForeground
    {
        get => GetValue(CollapseToggleForegroundProperty);
        set => SetValue(CollapseToggleForegroundProperty, value);
    }
}

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

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

    public static readonly StyledProperty<BoxShadows> BaseBoxShadowProperty =
        AvaloniaProperty.Register<NodePresenter, BoxShadows>(nameof(BaseBoxShadow));

    // --- Collapse toggle ---

    public static readonly StyledProperty<IBrush?> CollapseToggleForegroundProperty =
        AvaloniaProperty.Register<NodePresenter, IBrush?>(nameof(CollapseToggleForeground));

    public static readonly StyledProperty<double> CollapseToggleFontSizeProperty =
        AvaloniaProperty.Register<NodePresenter, double>(nameof(CollapseToggleFontSize), 8);

    public static readonly StyledProperty<string> CollapseExpandedGlyphProperty =
        AvaloniaProperty.Register<NodePresenter, string>(nameof(CollapseExpandedGlyph), "\u25B2");

    public static readonly StyledProperty<string> CollapseCollapsedGlyphProperty =
        AvaloniaProperty.Register<NodePresenter, string>(nameof(CollapseCollapsedGlyph), "\u25BC");

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

    /// <summary>Effective box shadow for card elevation. Computed from <see cref="BaseBoxShadow"/>
    /// scaled by the canvas's <see cref="NodiumGraphCanvas.ViewportZoom"/> so the shadow visually
    /// scales with the node. Set by the library; do not assign directly — set <see cref="BaseBoxShadow"/>
    /// instead.</summary>
    public BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }

    /// <summary>Declared (zoom=1) box shadow for card elevation. The effective
    /// <see cref="BoxShadow"/> is this value scaled by the canvas's ViewportZoom.</summary>
    public BoxShadows BaseBoxShadow
    {
        get => GetValue(BaseBoxShadowProperty);
        set => SetValue(BaseBoxShadowProperty, value);
    }

    /// <summary>Foreground brush for the collapse toggle arrows.</summary>
    public IBrush? CollapseToggleForeground
    {
        get => GetValue(CollapseToggleForegroundProperty);
        set => SetValue(CollapseToggleForegroundProperty, value);
    }

    /// <summary>Font size for the collapse toggle glyphs. Default: 8.</summary>
    public double CollapseToggleFontSize
    {
        get => GetValue(CollapseToggleFontSizeProperty);
        set => SetValue(CollapseToggleFontSizeProperty, value);
    }

    /// <summary>Glyph shown when the node body is expanded. Default: ▲.</summary>
    public string CollapseExpandedGlyph
    {
        get => GetValue(CollapseExpandedGlyphProperty);
        set => SetValue(CollapseExpandedGlyphProperty, value);
    }

    /// <summary>Glyph shown when the node body is collapsed. Default: ▼.</summary>
    public string CollapseCollapsedGlyph
    {
        get => GetValue(CollapseCollapsedGlyphProperty);
        set => SetValue(CollapseCollapsedGlyphProperty, value);
    }

    private NodiumGraphCanvas? _canvas;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _canvas = this.FindAncestorOfType<NodiumGraphCanvas>();
        if (_canvas != null)
            _canvas.PropertyChanged += OnCanvasPropertyChanged;
        UpdateEffectiveShadow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_canvas != null)
            _canvas.PropertyChanged -= OnCanvasPropertyChanged;
        _canvas = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnCanvasPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NodiumGraphCanvas.ViewportZoomProperty)
            UpdateEffectiveShadow();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BaseBoxShadowProperty)
            UpdateEffectiveShadow();
    }

    private void UpdateEffectiveShadow()
    {
        var zoom = _canvas?.ViewportZoom ?? 1.0;
        BoxShadow = ScaleBoxShadows(BaseBoxShadow, zoom);
    }

    internal static BoxShadows ScaleBoxShadows(BoxShadows source, double scale)
    {
        var count = source.Count;
        if (count == 0 || scale == 1.0) return source;

        if (count == 1)
            return new BoxShadows(ScaleOne(source[0], scale));

        var head = ScaleOne(source[0], scale);
        var tail = new BoxShadow[count - 1];
        for (var i = 1; i < count; i++)
            tail[i - 1] = ScaleOne(source[i], scale);
        return new BoxShadows(head, tail);
    }

    private static BoxShadow ScaleOne(BoxShadow s, double scale) => new()
    {
        OffsetX = s.OffsetX * scale,
        OffsetY = s.OffsetY * scale,
        Blur = s.Blur * scale,
        Spread = s.Spread * scale,
        Color = s.Color,
        IsInset = s.IsInset,
    };
}

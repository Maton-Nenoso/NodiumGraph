using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// Provides default DataTemplates for node types when no custom template is set.
/// </summary>
internal static class DefaultTemplates
{
    /// <summary>
    /// Value converter that inverts a boolean value. Used for binding IsVisible to !IsCollapsed.
    /// </summary>
    private sealed class InvertBoolConverter : IValueConverter
    {
        public static readonly InvertBoolConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }

    public static IDataTemplate NodeTemplate { get; } = new FuncDataTemplate<Node>((node, _) =>
    {
        var style = node?.Style;
        var cornerRadius = style?.CornerRadius ?? new CornerRadius(6);

        var header = new Border
        {
            CornerRadius = new CornerRadius(cornerRadius.TopLeft, cornerRadius.TopRight, 0, 0),
            Padding = style?.HeaderPadding ?? new Thickness(8, 4),
            [!Visual.IsVisibleProperty] = new Binding(nameof(Node.ShowHeader)),
            Child = new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding(nameof(Node.Title)),
                FontWeight = style?.HeaderFontWeight ?? FontWeight.SemiBold,
                FontSize = style?.HeaderFontSize ?? 12
            }
        };

        if (style?.HeaderFontFamily != null)
            ((TextBlock)header.Child).FontFamily = style.HeaderFontFamily;

        // Resolution: per-instance style → theme resource → default
        var headerText = (TextBlock)header.Child;
        if (style?.HeaderForeground != null)
            headerText.Foreground = style.HeaderForeground;
        else
        {
            headerText.Foreground = Brushes.White;
            headerText.Bind(TextBlock.ForegroundProperty,
                headerText.GetResourceObservable(NodiumGraphResources.NodeHeaderForegroundBrushKey));
        }

        if (style?.HeaderBackground != null)
            header.Background = style.HeaderBackground;
        else
            header.Bind(Border.BackgroundProperty,
                header.GetResourceObservable(NodiumGraphResources.NodeHeaderBrushKey));

        // Body: hidden when IsCollapsed==true
        var body = new Border
        {
            MinHeight = style?.BodyMinHeight ?? 4,
            [!Visual.IsVisibleProperty] = new Binding(nameof(Node.IsCollapsed))
            {
                Converter = InvertBoolConverter.Instance
            }
        };

        // Pill indicator: shown when ShowHeader==false AND IsCollapsed==true.
        // Uses a MultiBinding with BoolConverters.And — binds to IsCollapsed and !ShowHeader.
        var pill = new Border
        {
            Height = 8,
            MinWidth = 40,
            CornerRadius = new CornerRadius(4),
            IsVisible = false // default hidden
        };

        if (style?.HeaderBackground != null)
            pill.Background = style.HeaderBackground;
        else
            pill.Bind(Border.BackgroundProperty,
                pill.GetResourceObservable(NodiumGraphResources.NodeHeaderBrushKey));

        // Pill visible when IsCollapsed==true AND ShowHeader==false
        pill.Bind(Visual.IsVisibleProperty, new MultiBinding
        {
            Converter = BoolConverters.And,
            Bindings =
            {
                new Binding(nameof(Node.IsCollapsed)),
                new Binding(nameof(Node.ShowHeader)) { Converter = InvertBoolConverter.Instance }
            }
        });

        var border = new Border
        {
            CornerRadius = cornerRadius,
            BorderThickness = new Thickness(style?.BorderThickness ?? 1),
            MinWidth = style?.MinWidth ?? 120,
            Child = new StackPanel
            {
                Children =
                {
                    header,
                    body,
                    pill
                }
            }
        };

        if (style?.BodyBackground != null)
            border.Background = style.BodyBackground;
        else
            border.Bind(Border.BackgroundProperty,
                border.GetResourceObservable(NodiumGraphResources.NodeBodyBrushKey));

        if (style?.BorderBrush != null)
            border.BorderBrush = style.BorderBrush;
        else
            border.Bind(Border.BorderBrushProperty,
                border.GetResourceObservable(NodiumGraphResources.NodeBorderBrushKey));

        if (style?.Opacity != null)
            border.Opacity = style.Opacity.Value;

        return border;
    }, supportsRecycling: false);

    public static IDataTemplate GroupNodeTemplate { get; } = new FuncDataTemplate<GroupNode>((node, _) =>
    {
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 120, 180, 255)),
            Background = new SolidColorBrush(Color.FromArgb(30, 120, 180, 255)),
            Padding = new Thickness(8),
            MinWidth = 200,
            MinHeight = 100,
            Child = new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding(nameof(Node.Title)),
                Foreground = new SolidColorBrush(Color.FromArgb(180, 200, 220, 255)),
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Top
            }
        };
    }, supportsRecycling: false);

    public static IDataTemplate CommentNodeTemplate { get; } = new FuncDataTemplate<CommentNode>((node, _) =>
    {
        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 220, 100)),
            Padding = new Thickness(8),
            Child = new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding(nameof(CommentNode.Comment)),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 220, 100)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 200
            }
        };
    }, supportsRecycling: false);

    /// <summary>
    /// Resolves the appropriate template for a node. Checks custom template first,
    /// then falls back to built-in defaults.
    /// </summary>
    public static IDataTemplate? ResolveTemplate(Node node, IDataTemplate? customTemplate)
    {
        if (customTemplate != null && customTemplate.Match(node))
            return customTemplate;

        return node switch
        {
            CommentNode => CommentNodeTemplate,
            GroupNode => GroupNodeTemplate,
            _ => NodeTemplate
        };
    }
}

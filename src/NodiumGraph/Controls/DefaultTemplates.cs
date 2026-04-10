using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// Provides default DataTemplates for node types when no custom template is set.
/// </summary>
internal static class DefaultTemplates
{
    public static IDataTemplate NodeTemplate { get; } = new FuncDataTemplate<Node>((node, _) =>
    {
        var style = node?.Style;
        var cornerRadius = style?.CornerRadius ?? new CornerRadius(6);

        var header = new Border
        {
            CornerRadius = new CornerRadius(cornerRadius.TopLeft, cornerRadius.TopRight, 0, 0),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding(nameof(Node.Title)),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12
            }
        };

        // Resolution: per-instance style → theme resource → default
        if (style?.HeaderBackground != null)
            header.Background = style.HeaderBackground;
        else
            header.Bind(Border.BackgroundProperty,
                header.GetResourceObservable(NodiumGraphResources.NodeHeaderBrushKey));

        var border = new Border
        {
            CornerRadius = cornerRadius,
            BorderThickness = new Thickness(style?.BorderThickness ?? 1),
            MinWidth = 120,
            Child = new StackPanel
            {
                Children =
                {
                    header,
                    // Body: empty padding area. Subclass templates replace the
                    // entire node visual — this just provides consistent spacing.
                    new Border { MinHeight = 4 }
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

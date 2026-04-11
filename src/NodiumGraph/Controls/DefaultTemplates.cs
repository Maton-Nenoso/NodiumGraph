using Avalonia;
using Avalonia.Controls;
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
    /// <summary>
    /// Default <see cref="IDataTemplate"/> for <see cref="Node"/> instances, creating a <see cref="NodePresenter"/>.
    /// <para>
    /// <b>Limitation:</b> <see cref="NodeStyle"/> properties are read once at template instantiation.
    /// Changing <see cref="Node.Style"/> at runtime will rebuild the container automatically if the canvas
    /// handles the <c>Node.Style</c> property change (see <c>OnNodePropertyChanged</c>).
    /// </para>
    /// </summary>
    public static IDataTemplate NodeTemplate { get; } = new FuncDataTemplate<Node>((node, _) =>
    {
        var presenter = new NodePresenter();
        var style = node?.Style;

        if (style?.HeaderBackground != null)
            presenter.HeaderBackground = style.HeaderBackground;
        if (style?.HeaderForeground != null)
            presenter.HeaderForeground = style.HeaderForeground;
        if (style?.BodyBackground != null)
            presenter.Background = style.BodyBackground;
        if (style?.BorderBrush != null)
            presenter.BorderBrush = style.BorderBrush;
        if (style?.BorderThickness != null)
            presenter.BorderThickness = new Thickness(style.BorderThickness.Value);
        if (style?.CornerRadius != null)
            presenter.CornerRadius = style.CornerRadius.Value;
        if (style?.MinWidth != null)
            presenter.MinWidth = style.MinWidth.Value;
        if (style?.Opacity != null)
            presenter.Opacity = style.Opacity.Value;

        return presenter;
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
    /// then falls back to built-in defaults. Returns null for custom Node subclasses
    /// so that Avalonia's DataTemplate resolution from the visual tree can work.
    /// </summary>
    public static IDataTemplate? ResolveTemplate(Node node, IDataTemplate? customTemplate)
    {
        if (customTemplate != null && customTemplate.Match(node))
            return customTemplate;

        return node switch
        {
            CommentNode => CommentNodeTemplate,
            GroupNode => GroupNodeTemplate,
            _ when node.GetType() != typeof(Node) => null, // Custom subclass — let DataTemplate resolution work
            _ => NodeTemplate
        };
    }
}

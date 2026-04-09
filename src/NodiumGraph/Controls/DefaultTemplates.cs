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
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 58)),
            MinWidth = 120,
            Child = new StackPanel
            {
                Children =
                {
                    // Header
                    new Border
                    {
                        CornerRadius = new CornerRadius(6, 6, 0, 0),
                        Background = new SolidColorBrush(Color.FromRgb(70, 100, 160)),
                        Padding = new Thickness(8, 4),
                        Child = new TextBlock
                        {
                            [!TextBlock.TextProperty] = new Binding(nameof(Node.Title)),
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.SemiBold,
                            FontSize = 12
                        }
                    },
                    // Body (content area for subclass data)
                    new ContentPresenter
                    {
                        Margin = new Thickness(8, 4),
                        [!ContentPresenter.ContentProperty] = new Binding(".")
                    }
                }
            }
        };

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

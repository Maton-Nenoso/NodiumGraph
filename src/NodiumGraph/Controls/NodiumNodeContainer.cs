using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// Internal per-node container. Replaces the bare ContentControl that used to be
/// created per node. Hosts the consumer's NodeTemplate in a ContentPresenter and
/// a NodeAdornmentLayer as a second visual child so that per-node decorations
/// render with their own node, respecting z-order overlap.
/// </summary>
internal sealed class NodiumNodeContainer : Panel
{
    private readonly ContentPresenter _contentPresenter;
    private readonly NodeAdornmentLayer _adornments;

    public NodiumNodeContainer(NodiumGraphCanvas canvas, Node node)
    {
        Node = node;
        ClipToBounds = false;
        DataContext = node;

        _contentPresenter = new ContentPresenter
        {
            Content = node,
        };
        Children.Add(_contentPresenter);

        _adornments = new NodeAdornmentLayer(canvas, node);
        Children.Add(_adornments);
    }

    internal Node Node { get; }

    internal ContentPresenter ContentPresenter => _contentPresenter;

    internal NodeAdornmentLayer AdornmentLayer => _adornments;

    internal Avalonia.Controls.Templates.IDataTemplate? ContentTemplate
    {
        get => _contentPresenter.ContentTemplate;
        set => _contentPresenter.ContentTemplate = value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _contentPresenter.Measure(availableSize);
        _adornments.Measure(_contentPresenter.DesiredSize);
        return _contentPresenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _contentPresenter.Arrange(rect);
        _adornments.Arrange(rect);
        return finalSize;
    }
}

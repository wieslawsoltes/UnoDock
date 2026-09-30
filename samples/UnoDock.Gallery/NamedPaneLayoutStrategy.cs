namespace UnoDock.Gallery;
/// <summary>An <see cref = "ILayoutUpdateStrategy"/> that places tools added at run
/// time into the anchorable pane whose <see cref = "LayoutAnchorablePane.Name"/>
/// matches, instead of the pane on the requested side. Documents keep the default
/// placement. Without a matching pane the default placement applies.</summary>
public sealed class NamedPaneLayoutStrategy(string paneName) : ILayoutUpdateStrategy
{
    public string PaneName { get; } = paneName;
    public LayoutAnchorable? LastPlaced
    {
        get;
        private set;
    }

    public bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer)
    {
        var pane = layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(p => p.Name == PaneName);
        if (pane == null)
            return false;
        pane.Children.Add(anchorableToShow);
        return true;
    }

    public void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown) => LastPlaced = anchorableShown;
    public bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument documentToShow, ILayoutContainer destinationContainer) => false;
    public void AfterInsertDocument(LayoutRoot layout, LayoutDocument documentShown)
    {
    }
}

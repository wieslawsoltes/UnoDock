namespace UnoDock.Layout;
public interface ILayoutUpdateStrategy
{
    bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer);
    void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown);
    bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument documentToShow, ILayoutContainer destinationContainer);
    void AfterInsertDocument(LayoutRoot layout, LayoutDocument documentShown);
}

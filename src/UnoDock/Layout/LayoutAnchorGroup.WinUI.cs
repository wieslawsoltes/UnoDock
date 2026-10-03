#if WINDOWS
namespace UnoDock.Layout;
/// <summary>Native WinUI markup: the XAML Binary Format generator cannot encode members
/// declared on generic base classes, so the markup-facing ones are declared here.</summary>
public partial class LayoutAnchorGroup
{
    private XamlChildList<LayoutAnchorable>? _xamlChildren;
    /// <summary>The children as objects, for the content property in WinUI markup.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public IList<object> XamlChildren => _xamlChildren ??= new(Children);
}
#endif

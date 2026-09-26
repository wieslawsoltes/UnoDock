namespace UnoDock.Gallery;

public sealed class XamlItemStyleSelector : StyleSelector
{
    public Style? DocumentStyle
    {
        get;
        set;
    }

    protected override Style? SelectStyleCore(object item, DependencyObject container) => item is XamlDocument ? DocumentStyle : null;
}

namespace UnoDock.Gallery;

public sealed class XamlItemStyleSelector : StyleSelector
{
    public Style? DocumentStyle
    {
        get;
        set;
    }
    public Style? ToolStyle
    {
        get;
        set;
    }

    protected override Style? SelectStyleCore(object item, DependencyObject container) => item switch
    {
        XamlDocument => DocumentStyle,
        XamlTool => ToolStyle,
        _ => null
    };
}

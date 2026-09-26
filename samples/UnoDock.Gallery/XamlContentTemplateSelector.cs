namespace UnoDock.Gallery;

public sealed class XamlContentTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DocumentTemplate
    {
        get;
        set;
    }
    public DataTemplate? ToolTemplate
    {
        get;
        set;
    }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        XamlDocument => DocumentTemplate,
        XamlTool => ToolTemplate,
        _ => null
    };
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}

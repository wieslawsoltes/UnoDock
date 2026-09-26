namespace UnoDock.Gallery;

public sealed class XamlWorkspaceTemplateSelector : DataTemplateSelector
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

    protected override DataTemplate? SelectTemplateCore(object item) => item is XamlWorkspaceItem { IsTool: true } ? ToolTemplate : DocumentTemplate;
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}

namespace UnoDock.Gallery;

public sealed class XamlContentTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DocumentTemplate
    {
        get;
        set;
    }

    protected override DataTemplate? SelectTemplateCore(object item) => item is XamlDocument ? DocumentTemplate : null;
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}

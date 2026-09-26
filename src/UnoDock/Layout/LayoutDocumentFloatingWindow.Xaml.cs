namespace UnoDock.Layout;

public partial class LayoutDocumentFloatingWindow
{
    static LayoutDocumentFloatingWindow()
    {
    }

    public static readonly DependencyProperty RootDocumentProperty = LayoutXamlProperty.Register<LayoutDocumentFloatingWindow, LayoutDocument?>(nameof(RootDocument), null, owner => owner.RootDocument, (owner, value) => owner.RootDocument = value!);
}

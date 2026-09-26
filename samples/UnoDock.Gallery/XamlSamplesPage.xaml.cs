namespace UnoDock.Gallery;

public sealed partial class XamlSamplesPage : UserControl, IDisposable
{
    public XamlSamplesPage() => InitializeComponent();
    public new void Dispose()
    {
        Declarative.Dispose();
        Mvvm.Dispose();
        Templates.Dispose();
    }
}

using System.Runtime.CompilerServices;

namespace UnoDock.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class XamlTool : INotifyPropertyChanged, IDockContent
{
    private string _title = "Inspector", _text = "This tool is created from AnchorablesSource. Its metadata, policy and content template are bound in XAML.";
    private bool _canHide = true, _canAutoHide = true, _canDockAsTabbedDocument = true;
    public string ContentId
    {
        get;
        set;
    } = "xaml-tool";
    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }
    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }
    public bool CanHide
    {
        get => _canHide;
        set => Set(ref _canHide, value);
    }
    public bool CanAutoHide
    {
        get => _canAutoHide;
        set => Set(ref _canAutoHide, value);
    }
    public bool CanDockAsTabbedDocument
    {
        get => _canDockAsTabbedDocument;
        set => Set(ref _canDockAsTabbedDocument, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
    }
}

using System.Runtime.CompilerServices;

namespace UnoDock.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class XamlDocument : INotifyPropertyChanged, IDockContent
{
    private string _title = "Workspace.xaml", _text = "// Edit this buffer, float its tab, and dock it back.\r// The same editor and binding are retained.";
    private bool _canClose = true;
    public string ContentId
    {
        get;
        set;
    } = "xaml-document";
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
    public bool CanClose
    {
        get => _canClose;
        set => Set(ref _canClose, value);
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

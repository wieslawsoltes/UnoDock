using System.Runtime.CompilerServices;

namespace UnoDock.Gallery;
/// <summary>Application data; it has no dependency on a docking view or layout model.</summary>
public sealed class XamlWorkspaceItem : INotifyPropertyChanged
{
    private string _title = "Untitled", _text = "";
    private bool _canClose = true, _selected;
    public string ContentId
    {
        get;
        set;
    } = Guid.NewGuid().ToString("N");
    public bool IsTool
    {
        get;
        set;
    }
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
    public bool IsSelected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
    }
}

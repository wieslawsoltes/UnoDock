using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Storage;

namespace UnoDock.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class WorkspaceDocument : INotifyPropertyChanged, IDockContent
{
    private string _name, _text, _savedText;
    private bool _isReadOnly, _isSaving, _isOpen;
    private readonly WorkspaceCommand _save, _revert, _close;
    internal WorkspaceDocument(string contentId, string name, string text, Action save, Action revert, Action close)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        if (contentId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Document IDs must be safe storage names.", nameof(contentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentNullException.ThrowIfNull(text);
        ContentId = contentId; _name = name; _text = _savedText = text;
        _save = new(save, () => IsOpen && IsDirty && !IsSaving);
        _revert = new(revert, () => IsOpen && IsDirty && !IsSaving);
        _close = new(close, () => IsOpen && !IsSaving);
    }
    public string ContentId { get; }
    public string Name
    {
        get => _name;
        set { ArgumentException.ThrowIfNullOrWhiteSpace(value); if (_name == value) return; _name = value; Changed(); Changed(nameof(Title)); }
    }
    public string Title => IsDirty ? Name + " *" : Name;
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value); if (value == _text) return;
            _text = value; Changed(); Changed(nameof(IsDirty)); Changed(nameof(Title));
            Changed(nameof(CharacterCount)); Changed(nameof(LineCount)); RefreshCommands();
        }
    }
    public bool IsDirty => !string.Equals(_savedText, _text, StringComparison.Ordinal);
    public bool IsReadOnly { get => _isReadOnly; set { if (_isReadOnly == value) return; _isReadOnly = value; Changed(); } }
    public bool IsSaving { get => _isSaving; internal set { if (_isSaving == value) return; _isSaving = value; Changed(); RefreshCommands(); } }
    public bool IsOpen { get => _isOpen; internal set { if (_isOpen == value) return; _isOpen = value; Changed(); RefreshCommands(); } }
    public int CharacterCount => _text.Length;
    public int LineCount => 1 + _text.Count(c => c == '\n');
    public ICommand SaveCommand => _save;
    public ICommand RevertCommand => _revert;
    public ICommand CloseCommand => _close;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void AcceptSaved(string snapshot)
    {
        _savedText = snapshot; Changed(nameof(IsDirty)); Changed(nameof(Title)); RefreshCommands();
    }
    internal void Revert() => Text = _savedText;
    internal void DetachCommands() { _save.Detach(); _revert.Detach(); _close.Detach(); }
    private void RefreshCommands() { _save.Refresh(); _revert.Refresh(); _close.Refresh(); }
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    public override string ToString() => Title;
}

internal sealed class WorkspaceCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    private Action? _execute = execute;
    private Func<bool>? _canExecute = canExecute;
    public bool CanExecute(object? parameter) => _execute != null && (_canExecute?.Invoke() ?? true);
    public void Execute(object? parameter) { if (CanExecute(parameter)) _execute?.Invoke(); }
    public event EventHandler? CanExecuteChanged;
    internal void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    internal void Detach() { _execute = null; _canExecute = null; Refresh(); CanExecuteChanged = null; }
}

internal interface IWorkspaceStorage
{
    Task WriteAsync(string contentId, string text);
}
internal sealed class LocalWorkspaceStorage : IWorkspaceStorage
{
    public async Task WriteAsync(string contentId, string text)
    {
        var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("mvvm-documents", CreationCollisionOption.OpenIfExists);
        // IDs are validated by WorkspaceDocument. The content is application-owned
        // text; display names never become paths or overwrite arbitrary user files.
        StorageFile? temporary = null;
        try
        {
            temporary = await folder.CreateFileAsync(contentId + "-" + Guid.NewGuid().ToString("N") + ".tmp", CreationCollisionOption.FailIfExists);
            await FileIO.WriteTextAsync(temporary, text);
            await temporary.RenameAsync(contentId + ".txt", NameCollisionOption.ReplaceExisting);
            temporary = null;
        }
        finally { if (temporary != null) await temporary.DeleteAsync(); }
    }
}

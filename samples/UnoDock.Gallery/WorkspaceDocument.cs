using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Storage;

namespace UnoDock.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class WorkspaceDocument : INotifyPropertyChanged, IDockContent
{
    private string _name, _text, _savedText;
    private string? _editorText;
    private bool _isReadOnly, _isSaving, _isOpen;
    private readonly WorkspaceCommand _save, _revert, _close;
    internal WorkspaceDocument(string contentId, string name, string text, Action save, Action revert, Action close)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        if (contentId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Document IDs must be safe storage names.", nameof(contentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(text);
        ContentId = contentId;
        _name = name;
        _text = _savedText = text;
        _save = new(save, () => IsOpen && IsDirty && !IsSaving);
        _revert = new(revert, () => IsOpen && IsDirty && !IsSaving);
        _close = new(close, () => IsOpen && !IsSaving);
    }

    public string ContentId
    {
        get;
    }

    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (_name == value)
                return;
            _name = value;
            Changed();
            Changed(nameof(Title));
        }
    }

    public string Title => IsDirty ? Name + " *" : Name;

    /// <summary>Exact application text, including original line-delimiter choices.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value == _text)
                return;
            _text = value;
            // Invalidate before notifying: reentrant observers must read the new
            // projection. Ordinary binding reads do not rescan an unchanged buffer.
            _editorText = null;
            Changed();
            Changed(nameof(EditorText));
            Changed(nameof(IsDirty));
            Changed(nameof(Title));
            Changed(nameof(CharacterCount));
            Changed(nameof(LineCount));
            RefreshCommands();
        }
    }

    /// <summary>CR-based native editing projection. No-op native synchronization must
        /// not rewrite the original buffer or mark the document dirty.</summary>
        public string EditorText
    {
        get => _editorText ??= WorkspaceTextProjection.ForEditor(_text); set => Text = WorkspaceTextProjection.ApplyEditorEdit(_text, value);
    }
    public bool IsDirty => !string.Equals(_savedText, _text, StringComparison.Ordinal);

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (_isReadOnly == value)
                return;
            _isReadOnly = value;
            Changed();
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        internal set
        {
            if (_isSaving == value)
                return;
            _isSaving = value;
            Changed();
            RefreshCommands();
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        internal set
        {
            if (_isOpen == value)
                return;
            _isOpen = value;
            Changed();
            RefreshCommands();
        }
    }

    public int CharacterCount => _text.Length;
    public int LineCount => WorkspaceTextProjection.CountLines(_text);
    public ICommand SaveCommand => _save;
    public ICommand RevertCommand => _revert;
    public ICommand CloseCommand => _close;

    public event PropertyChangedEventHandler? PropertyChanged;
    internal void AcceptSaved(string snapshot)
    {
        _savedText = snapshot;
        Changed(nameof(IsDirty));
        Changed(nameof(Title));
        RefreshCommands();
    }

    internal void Revert() => Text = _savedText;
    internal void DetachCommands()
    {
        _save.Detach();
        _revert.Detach();
        _close.Detach();
    }

    private void RefreshCommands()
    {
        _save.Refresh();
        _revert.Refresh();
        _close.Refresh();
    }

    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    public override string ToString() => Title;
}

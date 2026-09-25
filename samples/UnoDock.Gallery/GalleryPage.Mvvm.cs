using Windows.Storage;

namespace UnoDock.Gallery;
public sealed partial class GalleryPage
{
    private MvvmWorkspace? _mvvmWorkspace;
    private long _workspaceEpoch, _restoreRequest;
    private readonly System.Threading.SemaphoreSlim _layoutSaveGate = new(1, 1);
    internal MvvmWorkspace? ActiveMvvmWorkspace => _mvvmWorkspace;

    private void StopMvvmWorkspace()
    {
        var workspace = _mvvmWorkspace;
        _mvvmWorkspace = null;
        workspace?.Dispose();
    }

    private async Task SaveWorkspaceLayoutAsync()
    {
        var epoch = _workspaceEpoch;
        using var text = new StringWriter();
        new XmlLayoutSerializer(Dock).Serialize(text);
        var snapshot = text.ToString();
        await _layoutSaveGate.WaitAsync();
        StorageFile? temporary = null;
        try
        {
            if (_pageDisposed || epoch != _workspaceEpoch)
                return;
            var folder = ApplicationData.Current.LocalFolder;
            temporary = await folder.CreateFileAsync("workspace-" + Guid.NewGuid().ToString("N") + ".tmp", CreationCollisionOption.FailIfExists);
            await FileIO.WriteTextAsync(temporary, snapshot);
            if (_pageDisposed || epoch != _workspaceEpoch)
                return;
            await temporary.RenameAsync("workspace.xml", NameCollisionOption.ReplaceExisting);
            temporary = null;
            if (!_pageDisposed && epoch == _workspaceEpoch)
                Log("Layout saved to application storage; document buffers are not serialized.");
        }
        finally
        {
            try
            {
                if (temporary != null)
                    await temporary.DeleteAsync();
            }
            finally
            {
                _layoutSaveGate.Release();
            }
        }
    }

    private async Task RestoreWorkspaceLayoutAsync()
    {
        var epoch = _workspaceEpoch;
        var request = ++_restoreRequest;
        var root = Dock.Layout;
        var workspace = _mvvmWorkspace;
        var file = await ApplicationData.Current.LocalFolder.GetFileAsync("workspace.xml");
        var xml = await FileIO.ReadTextAsync(file);
        if (_pageDisposed || epoch != _workspaceEpoch || request != _restoreRequest || !ReferenceEquals(root, Dock.Layout))
            return;
        if (workspace?.IsCurrent == true)
            workspace.RestoreLayout(xml);
        else
        {
            var serializer = new XmlLayoutSerializer(Dock);
            serializer.LayoutSerializationCallback += (_, e) =>
            {
                if (_pageDisposed || epoch != _workspaceEpoch)
                    throw new InvalidOperationException("Sample changed during layout restoration.");
                if (e.Model.ContentId != null && _content.TryGetValue(e.Model.ContentId, out var content))
                    e.Content = content;
                else if (e.Content == null)
                    e.Cancel = true;
            };
            serializer.Deserialize(new StringReader(xml));
        }

        if (!_pageDisposed && epoch == _workspaceEpoch)
            Log("Layout restored using current application-owned content identities.");
    }
}

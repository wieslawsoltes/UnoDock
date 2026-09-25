using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Storage;

namespace UnoDock.Gallery;
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
        finally
        {
            if (temporary != null)
                await temporary.DeleteAsync();
        }
    }
}

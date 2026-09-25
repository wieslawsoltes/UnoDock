using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Storage;

namespace UnoDock.Gallery;

internal interface IWorkspaceStorage
{
    Task WriteAsync(string contentId, string text);
}

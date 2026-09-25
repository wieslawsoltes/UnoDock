using System.ComponentModel;
using Microsoft.Windows.Shell;
using UnoDock.Controls;

namespace UnoDock.Gallery;
/// <summary>Expose the protected navigator entry point in the sample without reflection.</summary>
public sealed class GalleryDockingManager : DockingManager
{
    public void OpenNavigator() => ShowNavigatorWindow();
}

using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Layout;
using UnoDock.Controls;

namespace UnoDock.Internal;
internal interface IRefreshableLayoutControl : ILayoutControl
{
    void Update(DockSurface surface);
}

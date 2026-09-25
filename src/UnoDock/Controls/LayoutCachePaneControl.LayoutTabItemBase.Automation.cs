using Microsoft.UI.Xaml.Automation.Peers;
using UnoDock.Layout;

namespace UnoDock.Controls;

public partial class LayoutCachePaneControl
{
    internal ILayoutGroup? AutomationPane => Pane;
    internal IEnumerable<LayoutContent> AutomationItems => Pane?.Root is LayoutRoot { Manager: { } manager } root && ReferenceEquals(manager.Layout, root) ? Items.Where(item => item is not LayoutDocument { IsVisible: false } and not LayoutAnchorable { IsHidden: true }) : [];

    private bool _automationSelectionQueued;
    private void QueueSelectionAutomation()
    {
        if (_automationSelectionQueued || FrameworkElementAutomationPeer.FromElement(this) is not LayoutPaneAutomationPeer peer)
            return;
        _automationSelectionQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _automationSelectionQueued = false;
            peer.Synchronize();
        }))
            _automationSelectionQueued = false;
    }
}

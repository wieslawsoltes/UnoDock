using Microsoft.UI.Xaml.Automation.Peers;
using UnoDock.Layout;

namespace UnoDock.Controls;
public abstract partial class LayoutTabItemBase
{
    private bool _automationWasLoaded, _automationQueued;
    private void InitializeTabAutomation()
    {
        Loaded += (_, _) =>
        {
            _automationWasLoaded = true;
            QueueAutomationRefresh();
        };
        Unloaded += (_, _) => QueueAutomationRefresh();
        IsEnabledChanged += (_, _) => QueueAutomationRefresh();
    }

    internal LayoutContent? AutomationModel
    {
        get
        {
            if (!DispatcherQueue.HasThreadAccess)
                throw new InvalidOperationException("Automation must run on the owning UI thread.");
            if (Model is not { Parent: ILayoutGroup group, Root: LayoutRoot { Manager: { } manager } root } model || !ReferenceEquals(manager, _manager) || !ReferenceEquals(manager.Layout, root) || !group.Children.Any(child => ReferenceEquals(child, model)) || model is LayoutDocument { IsVisible: false } or LayoutAnchorable { IsHidden: true } || (_automationWasLoaded && !IsLoaded))
                return null;
            if (this.FindVisualAncestor<LayoutCachePaneControl>()is { } pane && !ReferenceEquals(pane.AutomationPane, group))
                return null;
            return model;
        }
    }

    internal bool AutomationEnabled => IsEnabled && AutomationModel is { IsEnabled: true };
    internal bool AutomationHasFocus => _label.FocusState != FocusState.Unfocused;

    internal bool FocusFromAutomation() => AutomationEnabled && _label.Focus(FocusState.Keyboard);
    internal void QueueAutomationRefresh()
    {
        if (_automationQueued || Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(this)is not LayoutTabAutomationPeer peer)
            return;
        _automationQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _automationQueued = false;
            peer.Synchronize();
        }))
            _automationQueued = false;
    }
}

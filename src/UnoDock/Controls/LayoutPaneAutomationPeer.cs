using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Layout;

namespace UnoDock.Controls;

public sealed class LayoutPaneAutomationPeer(LayoutCachePaneControl owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
{
    private LayoutContent? _lastSelection = owner.SelectedItem as LayoutContent;
    protected override string GetClassNameCore() => owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    protected override object? GetPatternCore(PatternInterface patternInterface) => patternInterface == PatternInterface.Selection ? this : base.GetPatternCore(patternInterface);
    public bool CanSelectMultiple => false;
    public bool IsSelectionRequired => owner.AutomationItems.Any(c => c.IsEnabled);

    public IRawElementProviderSimple[] GetSelection()
    {
        if (owner.SelectedItem is not LayoutContent model || !owner.AutomationItems.Any(c => ReferenceEquals(c, model)) || owner.TabFor(model) is not { } tab || CreatePeerForElement(tab) is not { } peer)
            return [];
        return [ProviderFromPeer(peer)];
    }

    protected override IList<AutomationPeer> GetChildrenCore()
    {
        // A snapshot is required: provider creation can invoke application peers.
        var models = owner.AutomationItems.ToArray();
        var peers = new List<AutomationPeer>();
        foreach (var model in models)
            if (owner.TabFor(model) is { } tab && CreatePeerForElement(tab) is { } peer)
                peers.Add(peer);
        if (owner.SelectedItem is LayoutContent selected && models.Any(m => ReferenceEquals(m, selected)) && selected.Root?.Manager?.GetLayoutItemFromModel(selected).ExistingView is { } view && CreatePeerForElement(view) is { } contentPeer)
            peers.Add(contentPeer);
        return peers;
    }

    internal void Synchronize()
    {
        var selected = owner.SelectedItem as LayoutContent;
        if (selected != null && !owner.AutomationItems.Any(c => ReferenceEquals(c, selected)))
            selected = null;
        if (ReferenceEquals(_lastSelection, selected))
            return;
        _lastSelection = selected;
        RaiseAutomationEvent(AutomationEvents.SelectionPatternOnInvalidated);
    }
}

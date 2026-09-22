using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Xceed.Wpf.AvalonDock.Controls;

public sealed class LayoutPaneAutomationPeer(LayoutCachePaneControl owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
{
    protected override string GetClassNameCore() => owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    protected override object? GetPatternCore(PatternInterface patternInterface) => patternInterface == PatternInterface.Selection ? this : base.GetPatternCore(patternInterface);
    public bool CanSelectMultiple => false;
    public bool IsSelectionRequired => owner.Items.Any(c => c.IsEnabled);
    public IRawElementProviderSimple[] GetSelection()
    {
        if (owner.SelectedItem is not Layout.LayoutContent model || owner.TabFor(model) is not { } tab) return [];
        return [ProviderFromPeer(CreatePeerForElement(tab))];
    }
    protected override IList<AutomationPeer> GetChildrenCore()
    {
        var peers = owner.Items.Select(owner.TabFor).OfType<LayoutTabItemBase>().Select(CreatePeerForElement).Where(p => p != null).ToList();
        if (owner.SelectedItem is Layout.LayoutContent model && model.Root?.Manager?.GetLayoutItemFromModel(model).ExistingView is { } view)
            if (CreatePeerForElement(view) is { } contentPeer) peers.Add(contentPeer);
        return peers;
    }
}

public sealed class LayoutTabAutomationPeer(LayoutTabItemBase owner) : FrameworkElementAutomationPeer(owner), ISelectionItemProvider, IInvokeProvider
{
    protected override string GetClassNameCore() => owner.GetType().Name;
    protected override string GetNameCore() => owner.Model?.Title ?? base.GetNameCore();
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    protected override object? GetPatternCore(PatternInterface patternInterface) => patternInterface is PatternInterface.SelectionItem or PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);
    public bool IsSelected => owner.Model?.IsSelected == true;
    public IRawElementProviderSimple? SelectionContainer => owner.FindVisualAncestor<LayoutCachePaneControl>() is { } pane ? ProviderFromPeer(CreatePeerForElement(pane)) : null;
    public void Select()
    {
        if (owner.Model is not { IsEnabled: true } model) throw new InvalidOperationException("The tab is not enabled.");
        model.IsActive = true;
    }
    public void AddToSelection() => Select();
    public void RemoveFromSelection()
    {
        if (IsSelected) throw new InvalidOperationException("A nonempty docking pane requires one selected tab.");
    }
    public void Invoke() => Select();
}

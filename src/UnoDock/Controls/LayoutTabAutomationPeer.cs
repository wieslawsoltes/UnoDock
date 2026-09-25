using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using UnoDock.Layout;

namespace UnoDock.Controls;
public sealed class LayoutTabAutomationPeer(LayoutTabItemBase owner) : FrameworkElementAutomationPeer(owner), ISelectionItemProvider, IInvokeProvider
{
    private string? _lastName = Explicit(AutomationProperties.GetName(owner), owner.Model?.Title ?? "");
    private bool? _lastSelected = owner.AutomationModel is { IsSelected: true, Parent: ILayoutContentSelector selector } model && ReferenceEquals(selector.SelectedContent, model);
    private bool? _lastEnabled = owner.AutomationEnabled;
    protected override string GetClassNameCore() => owner.GetType().Name;
    protected override string GetNameCore() => Explicit(AutomationProperties.GetName(owner), owner.Model?.Title ?? base.GetNameCore());
    protected override string GetAutomationIdCore() => Explicit(AutomationProperties.GetAutomationId(owner), owner.Model?.ContentId ?? base.GetAutomationIdCore());
    protected override string GetHelpTextCore() => Explicit(AutomationProperties.GetHelpText(owner), (owner.Model as LayoutDocument)?.Description ?? "");
    private static string Explicit(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    protected override object? GetPatternCore(PatternInterface patternInterface) => patternInterface is PatternInterface.SelectionItem or PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);
    protected override bool IsEnabledCore() => owner.AutomationEnabled;
    protected override bool IsKeyboardFocusableCore() => owner.AutomationEnabled;
    protected override bool HasKeyboardFocusCore() => owner.AutomationHasFocus;
    protected override void SetFocusCore()
    {
        if (!owner.FocusFromAutomation())
            throw new InvalidOperationException("The tab cannot receive keyboard focus.");
    }

    public bool IsSelected => owner.AutomationModel is { IsSelected: true, Parent: ILayoutContentSelector selector } model && ReferenceEquals(selector.SelectedContent, model);
    public IRawElementProviderSimple? SelectionContainer => owner.AutomationModel != null && owner.FindVisualAncestor<LayoutCachePaneControl>()is { } pane && CreatePeerForElement(pane)is { } peer ? ProviderFromPeer(peer) : null;

    public void Select()
    {
        if (!owner.AutomationEnabled || owner.AutomationModel is not { } model || owner.LayoutItem is not { } item)
            throw new InvalidOperationException("The tab is not enabled or no longer belongs to its workspace.");
        var command = item.ActivateCommand;
        if (command?.CanExecute(null) != true)
            throw new InvalidOperationException("The tab activation command is disabled.");
        // CanExecute is application code: it may replace the root, transfer the
        // model, change the command, disable the tab or dispose the manager.
        if (!ReferenceEquals(owner.AutomationModel, model) || !owner.AutomationEnabled || !ReferenceEquals(item, owner.LayoutItem) || !ReferenceEquals(item.ActivateCommand, command))
            throw new InvalidOperationException("Tab ownership changed during activation validation.");
        command.Execute(null);
        owner.QueueAutomationRefresh();
    }

    public void AddToSelection()
    {
        if (owner.AutomationModel is not { Parent: ILayoutContentSelector selector } model)
            throw new InvalidOperationException("The tab is no longer selectable.");
        if (selector.SelectedContent is { } selected && !ReferenceEquals(selected, model))
            throw new InvalidOperationException("A docking pane does not support multiple selection. Use Select to replace it.");
        Select();
    }

    public void RemoveFromSelection()
    {
        if (owner.AutomationModel == null)
            throw new InvalidOperationException("The tab is no longer selectable.");
        if (IsSelected)
            throw new InvalidOperationException("A nonempty docking pane requires one selected tab.");
    }

    public void Invoke()
    {
        Select();
        if (owner.AutomationEnabled && IsSelected)
            RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked);
    }

    internal void Synchronize()
    {
        var name = GetNameCore();
        var selected = IsSelected;
        var enabled = IsEnabledCore();
        var oldName = _lastName;
        var oldSelected = _lastSelected;
        var oldEnabled = _lastEnabled;
        _lastName = name;
        _lastSelected = selected;
        _lastEnabled = enabled;
        if (oldName != null && oldName != name)
            RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, oldName, name);
        if (oldEnabled is { } enabledBefore && enabledBefore != enabled)
            RaisePropertyChangedEvent(AutomationElementIdentifiers.IsEnabledProperty, enabledBefore, enabled);
        if (oldSelected is { } selectedBefore && selectedBefore != selected)
        {
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, selectedBefore, selected);
            RaiseAutomationEvent(selected ? AutomationEvents.SelectionItemPatternOnElementSelected : AutomationEvents.SelectionItemPatternOnElementRemovedFromSelection);
        }
    }
}

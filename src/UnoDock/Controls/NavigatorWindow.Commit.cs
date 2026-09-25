using System.ComponentModel;
using UnoDock.Layout;

namespace UnoDock.Controls;

public partial class NavigatorWindow
{
    private bool _committingSelection;
    private long _activationSession;
    private EventHandler? _sessionLayoutChanged;
    internal DockingManager OwnerManager => _manager;
    internal bool HasSelectionSession => _sessionRoot != null;
    internal LayoutRoot? SelectionSessionRoot => _sessionRoot;

    private void CloseNavigatorForInput(bool commit)
    {
        var surface = _manager.Surface;
        if (surface?.OwnsNavigator(this) == true) surface.CloseNavigator(commit);
    }

    internal void CommitSelection()
    {
        if (_committingSelection) return;
        var session = _sessionVersion;
        CaptureSelectionCommit(() => session == _sessionVersion, false)?.Invoke();
    }

    /// <summary>Capture a one-use activation before removing the view. The surface
    /// supplies its closing-generation fence; ordinary EndSession does not revoke
    /// that authorized close, but a replacement session or workspace does.</summary>
    internal Action? CaptureSelectionCommit(Func<bool> ownsOperation, bool afterDetach) =>
        CaptureActivation(ownsOperation, afterDetach, null);

    /// <summary>Install the activation guards before explicit-close detachment.
    /// The caller must ensure detachment even when there is no eligible command.</summary>
    internal void CommitClosingSelection(Func<bool> ownsOperation, Action detach)
    {
        ArgumentNullException.ThrowIfNull(detach);
        CaptureActivation(ownsOperation, false, null, detach)?.Invoke();
    }

    /// <summary>Direct property assignment checks CanExecute while still visible.
    /// A veto/query failure must not dismiss the navigator. Successful queries run
    /// the category-specific hide/close stage before the guarded command executes.</summary>
    internal void CommitDirectSelection(Func<bool> ownsOperation, Action prepareExecution)
    {
        ArgumentNullException.ThrowIfNull(prepareExecution);
        CaptureActivation(ownsOperation, false, prepareExecution)?.Invoke();
    }

    private Action? CaptureActivation(Func<bool> ownsOperation, bool afterDetach, Action? prepareExecution, Action? detachBeforeQuery = null)
    {
        ArgumentNullException.ThrowIfNull(ownsOperation);
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator activation requires its owning UI thread.");
        if (_committingSelection || _sessionRoot is not { } root || !Eligible(_selected)) return null;
        var item = _selected!;
        var model = item.LayoutElement;
        var parent = model.Parent;
        var command = item.ActivateCommand;
        var surface = _manager.Surface;
        var activationSession = _activationSession;
        var selection = _selectionVersion;
        var directRequest = _directSelectionVersion;
        if (command == null || parent is not ILayoutContainer container) return null;
        var consumed = false;
        return () =>
        {
            if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator activation requires its owning UI thread.");
            if (consumed) return;
            consumed = true;
            if (_committingSelection) return;
            var invalidated = false;
            var detachedForQuery = false;
            bool Current()
            {
                if (invalidated || !ownsOperation() || activationSession != _activationSession || selection != _selectionVersion ||
                    directRequest != _directSelectionVersion ||
                    !ReferenceEquals(_selected, item) || !IsEnabled || !_manager.IsEnabled ||
                    !ReferenceEquals(_manager.Surface, surface) || !ReferenceEquals(_manager.Layout, root) ||
                    !ReferenceEquals(root.Manager, _manager) || !ReferenceEquals(model.Root, root) ||
                    !ReferenceEquals(model.Parent, parent) || !ReferenceEquals(item.LayoutElement, model) ||
                    !ReferenceEquals(item.ActivateCommand, command) || !Eligible(model)) return false;
                // The direct caller fences both sides of its own detach. Explicit
                // keyboard/host commits keep their original strict attachment rule.
                if (prepareExecution == null)
                {
                    if ((afterDetach || detachedForQuery) ? _sessionRoot != null : !ReferenceEquals(_sessionRoot, root)) return false;
                }
                else if (_sessionRoot != null && !ReferenceEquals(_sessionRoot, root)) return false;
                if (!container.Children.Any(child => ReferenceEquals(child, model))) return false;
                return ReferenceEquals(_manager.GetLayoutItemFromModel(model), item);
            }
            if (!Current()) return;
            EventHandler layoutChanged = (_, _) => invalidated = true;
            PropertyChangedEventHandler modelChanged = (_, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Parent" or "IsEnabled" or "IsVisible" or "IsHidden")
                    invalidated = true;
            };
            _committingSelection = true;
            long? commandToken = null, enabledToken = null, managerEnabledToken = null;
            _manager.LayoutChanged += layoutChanged;
            model.PropertyChanged += modelChanged;
            try
            {
                commandToken = item.RegisterPropertyChangedCallback(LayoutItem.ActivateCommandProperty, (_, _) => invalidated = true);
                enabledToken = RegisterPropertyChangedCallback(IsEnabledProperty, (_, _) => invalidated = true);
                managerEnabledToken = _manager.RegisterPropertyChangedCallback(IsEnabledProperty, (_, _) => invalidated = true);
                if (!Current()) return;
                if (detachBeforeQuery != null)
                {
                    detachedForQuery = true;
                    detachBeforeQuery();
                }
                if (!Current() || !command.CanExecute(null) || !Current()) return;
                prepareExecution?.Invoke();
                // Closing/Closed/Unloaded callbacks are application code too.
                if (Current()) command.Execute(null);
            }
            finally
            {
                if (managerEnabledToken is { } managerToken) _manager.UnregisterPropertyChangedCallback(IsEnabledProperty, managerToken);
                if (enabledToken is { } navigatorToken) UnregisterPropertyChangedCallback(IsEnabledProperty, navigatorToken);
                if (commandToken is { } itemToken) item.UnregisterPropertyChangedCallback(LayoutItem.ActivateCommandProperty, itemToken);
                model.PropertyChanged -= modelChanged;
                _manager.LayoutChanged -= layoutChanged;
                _committingSelection = false;
            }
        };
    }
}

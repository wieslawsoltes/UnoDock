using System.ComponentModel;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

public partial class NavigatorWindow
{
    private bool _committingSelection;
    private long _activationSession;
    private EventHandler? _sessionLayoutChanged;
    internal DockingManager OwnerManager => _manager;
    internal bool HasSelectionSession => _sessionRoot != null;

    private void CloseNavigatorForInput(bool commit)
    {
        var surface = _manager.Surface;
        if (surface?.OwnsNavigator(this) == true) surface.CloseNavigator(commit);
    }

    // Direct in-session commits are used by the control's integration hooks. A
    // detached/cancelled instance must never authorize a later activation.
    internal void CommitSelection()
    {
        if (_committingSelection) return;
        var session = _sessionVersion;
        CaptureSelectionCommit(() => session == _sessionVersion, false)?.Invoke();
    }

    /// <summary>Capture a one-use activation intent before the surface removes the
    /// navigator. The surface supplies its own closing-generation fence, so the
    /// normal Unloaded/EndSession sequence does not invalidate an authorized close,
    /// but cancellation, a replacement navigator or a new session always does.</summary>
    internal Action? CaptureSelectionCommit(Func<bool> ownsOperation, bool afterDetach)
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
        if (command == null || parent is not ILayoutContainer container) return null;
        var consumed = false;
        return () =>
        {
            if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator activation requires its owning UI thread.");
            if (consumed) return;
            consumed = true;
            if (_committingSelection) return;
            var invalidated = false;
            bool Current()
            {
                if (invalidated || !ownsOperation() || activationSession != _activationSession || selection != _selectionVersion ||
                    !ReferenceEquals(_selected, item) || !IsEnabled || !_manager.IsEnabled ||
                    !ReferenceEquals(_manager.Surface, surface) || !ReferenceEquals(_manager.Layout, root) ||
                    !ReferenceEquals(root.Manager, _manager) || !ReferenceEquals(model.Root, root) ||
                    !ReferenceEquals(model.Parent, parent) || !ReferenceEquals(item.LayoutElement, model) ||
                    !ReferenceEquals(item.ActivateCommand, command) || !Eligible(model) ||
                    (afterDetach ? _sessionRoot != null : !ReferenceEquals(_sessionRoot, root))) return false;
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
                // CanExecute is application code, not a pure predicate. The second
                // check includes ABA changes observed while that callback ran.
                if (Current() && command.CanExecute(null) && Current()) command.Execute(null);
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

using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Internal;

internal sealed partial class DockSurface
{
    internal bool OwnsNavigator(NavigatorWindow navigator) => !_disposed && ReferenceEquals(_navigator, navigator);

    internal void ShowNavigator(NavigatorWindow navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator presentation requires its owning UI thread.");
        if (!ReferenceEquals(navigator.OwnerManager, Manager)) throw new ArgumentException("The navigator belongs to another docking manager.", nameof(navigator));
        if (navigator.IsSelectionWindowClosed) throw new InvalidOperationException("A closed navigator cannot be shown again.");
        if (_disposed || !Manager.IsEnabled) return;
        if (_navigator is { } existing)
        {
            if (existing.HasSelectionSession) existing.Advance(InputState.ShiftDown ? -1 : 1);
            return;
        }
        var generation = ++_navigatorGeneration;
        var root = Manager.Layout;
        _navigator = navigator;
        bool Current() => OwnsNavigator(navigator) && generation == _navigatorGeneration &&
            ReferenceEquals(Manager.Layout, root) && ReferenceEquals(root.Manager, Manager);
        try
        {
            Microsoft.Windows.Shell.WindowRegistry.Find(Manager)?.Activate();
            if (!Current()) return;
            navigator.HorizontalAlignment = HorizontalAlignment.Center;
            if (!Current()) return;
            navigator.VerticalAlignment = VerticalAlignment.Center;
            if (!Current()) return;
            _flyouts.Children.Add(navigator);
            if (!Current()) return;
            _flyouts.IsHitTestVisible = true;
            navigator.Initialize();
            if (!Current() || !navigator.HasSelectionSession) return;
            if (!navigator.Focus(FocusState.Programmatic)) DispatcherQueue.TryEnqueue(() =>
            {
                if (Current() && navigator.HasSelectionSession) navigator.Focus(FocusState.Programmatic);
            });
        }
        catch (Exception failure)
        {
            if (OwnsNavigator(navigator) && generation == _navigatorGeneration)
            {
                try { CloseNavigator(false); }
                catch (Exception cleanup) { throw new AggregateException("Navigator opening and cleanup both failed.", failure, cleanup); }
            }
            throw;
        }
        finally
        {
            if (OwnsNavigator(navigator) && generation == _navigatorGeneration &&
                (!navigator.HasSelectionSession || !ReferenceEquals(Manager.Layout, root) || !ReferenceEquals(root.Manager, Manager)))
                CloseNavigator(false);
        }
    }

    internal void CloseNavigator(bool commit)
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator presentation requires its owning UI thread.");
        if (_navigator is not { } navigator) return;
        var root = navigator.SelectionSessionRoot;
        _navigator = null;
        var generation = ++_navigatorGeneration;
        bool Current() => root != null && !_disposed && _navigator == null && _navigatorGeneration == generation &&
            ReferenceEquals(Manager.Layout, root) && ReferenceEquals(root.Manager, Manager);
        var detached = false;
        void Detach()
        {
            if (detached) return;
            detached = true;
            DetachNavigator(navigator);
        }
        // Unloaded is application code. Install command/model/manager observers
        // before removal, not afterwards, to catch temporary-and-reversed writes.
        try { if (commit) navigator.CommitClosingSelection(Current, Detach); }
        finally { Detach(); }
        if (!Current()) return;
        RestoreNavigatorFocus(root, Current);
    }

    internal void CommitDirectNavigatorSelection(NavigatorWindow navigator, bool closeWindow, Func<bool> ownsSelection)
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator presentation requires its owning UI thread.");
        if (!OwnsNavigator(navigator) || navigator.SelectionSessionRoot is not { } root) return;
        var generation = _navigatorGeneration;
        var detached = false;
        bool Current() => ownsSelection() && !_disposed && _navigatorGeneration == generation &&
            ReferenceEquals(Manager.Layout, root) && ReferenceEquals(root.Manager, Manager) &&
            (detached ? _navigator == null : OwnsNavigator(navigator) && navigator.HasSelectionSession);
        navigator.CommitDirectSelection(Current, () =>
        {
            // Documents hide without Closing/Closed. Tools follow the cancellable
            // lifecycle. A veto retains the view, but (as observed) still permits
            // activation while the same request and workspace remain valid.
            if (closeWindow && !navigator.RequestSelectionClose()) return;
            if (!Current()) return;
            _navigator = null;
            generation = ++_navigatorGeneration;
            detached = true;
            DetachNavigator(navigator);
            if (closeWindow) navigator.CompleteSelectionClose();
        });
        if (detached) RestoreNavigatorFocus(root, Current);
    }

    private void DetachNavigator(NavigatorWindow navigator)
    {
        try { navigator.EndSession(); }
        finally
        {
            try { _flyouts.Children.Remove(navigator); }
            finally { _flyouts.IsHitTestVisible = _navigator != null || _autoHide?.Visibility == Visibility.Visible; }
        }
    }

    private void RestoreNavigatorFocus(LayoutRoot? root, Func<bool> ownsOperation)
    {
        if (root == null || !ownsOperation() || root.ActiveContent is not { } active || !ReferenceEquals(active.Root, root)) return;
        bool CurrentFocus() => ownsOperation() && ReferenceEquals(root.ActiveContent, active) && ReferenceEquals(active.Root, root) && active.IsEnabled;
        Manager.Refresh();
        if (!CurrentFocus()) return;
        var item = Manager.GetLayoutItemFromModel(active);
        if (!CurrentFocus()) return;
        if (active is LayoutAnchorable { IsAutoHidden: true } tool) OpenAutoHide(tool);
        if (!CurrentFocus()) return;
        var floating = Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, active.FindParent<LayoutFloatingWindow>()));
        floating?.Activate();
        if (!CurrentFocus()) return;
        if (item.RestoreEditorFocus()) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (CurrentFocus()) item.RestoreEditorFocus();
        });
    }
}

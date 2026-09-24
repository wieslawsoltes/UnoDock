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
        if (_disposed || !Manager.IsEnabled) return;
        if (_navigator is { } existing)
        {
            // Activation/Loaded callbacks may arrive while the new host is being
            // reserved. Do not navigate arrays belonging to a previous session.
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
            // Native activation can synchronously execute arbitrary focus handlers.
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
            // A root replacement before the session listener is installed, or an
            // initialization callback ending that session, releases this reservation.
            // A replacement navigator is never removed by this obsolete opening.
            if (OwnsNavigator(navigator) && generation == _navigatorGeneration &&
                (!navigator.HasSelectionSession || !ReferenceEquals(Manager.Layout, root) || !ReferenceEquals(root.Manager, Manager)))
                CloseNavigator(false);
        }
    }

    internal void CloseNavigator(bool commit)
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Navigator presentation requires its owning UI thread.");
        if (_navigator is not { } navigator) return;
        // A layout-replacement callback must not appropriate the new workspace's
        // focus. An uninitialized opening has no editor-focus restoration to perform.
        var root = navigator.SelectionSessionRoot;
        _navigator = null;
        var generation = ++_navigatorGeneration;
        bool Current() => root != null && !_disposed && _navigator == null && _navigatorGeneration == generation &&
            ReferenceEquals(Manager.Layout, root) && ReferenceEquals(root.Manager, Manager);
        var activate = commit ? navigator.CaptureSelectionCommit(Current, true) : null;
        try { navigator.EndSession(); }
        finally
        {
            try { _flyouts.Children.Remove(navigator); }
            finally { _flyouts.IsHitTestVisible = _navigator != null || _autoHide?.Visibility == Visibility.Visible; }
        }
        // The removed control's Unloaded callback may have shown another navigator
        // or replaced the workspace. Neither that host nor its focus belongs to us.
        if (!Current()) return;
        activate?.Invoke();
        if (root == null || !Current() || root.ActiveContent is not { } active || !ReferenceEquals(active.Root, root)) return;
        bool CurrentFocus() => Current() && ReferenceEquals(root.ActiveContent, active) && ReferenceEquals(active.Root, root) && active.IsEnabled;
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

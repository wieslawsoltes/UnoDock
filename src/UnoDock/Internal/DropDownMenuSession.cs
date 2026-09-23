using System.Runtime.ExceptionServices;

namespace Xceed.Wpf.AvalonDock.Internal;

/// <summary>A menu has one owner, even when application callbacks replace that owner.
/// All transitions run on its UI thread. The weak registry does not root closed triggers.</summary>
internal sealed class DropDownMenuSession
{
    private sealed record Request(FrameworkElement Owner, Func<bool> IsValid,
        Func<object?> Context, Action<bool> SetOpen, Point? Position, XamlRoot Root);
    private static readonly ConditionalWeakTable<MenuFlyout, DropDownMenuSession> Sessions = new();
    private readonly MenuFlyout _menu;
    private Request? _desired, _current;
    private long _contextRevision;
    private bool _draining, _contextDirty, _cleaningAfterFailure;

    private DropDownMenuSession(MenuFlyout menu)
    {
        _menu = menu;
        menu.Opening += (_, _) =>
        {
            if (_current is not { } current) return;
            if (ReferenceEquals(menu.Target, current.Owner)) ApplyContext(current);
            else
            {
                // An application may show the same flyout directly at a different owner.
                // Release our scope, but never hide that new owner's popup.
                _desired = null;
                Drain();
            }
        };
        menu.Closed += (_, _) =>
        {
            // An earlier subscriber may already have reopened the menu. Invocation
            // lists are snapshots: this old notification must not close the successor.
            if (menu.IsOpen || _current == null) return;
            _desired = null;
            Drain();
        };
    }

    internal static bool Owns(MenuFlyout menu, FrameworkElement owner) =>
        Sessions.TryGetValue(menu, out var state) && ReferenceEquals(state._desired?.Owner, owner);

    internal static void Open(MenuFlyout menu, FrameworkElement owner, Func<bool> valid,
        Func<object?> context, Action<bool> setOpen, Point? position = null)
    {
        var root = owner.XamlRoot ?? throw new InvalidOperationException("The dropdown trigger must be attached to a XamlRoot.");
        var state = Sessions.GetValue(menu, static value => new(value));
        if (state._cleaningAfterFailure) return;
        state._desired = new(owner, valid, context, setOpen, position, root);
        state.Drain();
    }

    internal static void Close(MenuFlyout? menu, FrameworkElement owner)
    {
        if (menu == null || !Sessions.TryGetValue(menu, out var state) ||
            !ReferenceEquals(state._desired?.Owner, owner)) return;
        state._desired = null;
        state.Drain();
    }

    internal static void UpdateContext(MenuFlyout? menu, FrameworkElement owner)
    {
        if (menu == null || !Sessions.TryGetValue(menu, out var state) ||
            !ReferenceEquals(state._desired?.Owner, owner)) return;
        state._contextDirty = true;
        state.Drain();
    }

    private static bool Valid(Request request) => request.Owner.IsLoaded && request.Owner.IsEnabled &&
        ReferenceEquals(request.Owner.XamlRoot, request.Root) && request.IsValid();

    private void Drain()
    {
        if (_draining || _cleaningAfterFailure) return;
        _draining = true;
        try
        {
            var budget = 64;
            while (!ReferenceEquals(_current, _desired) || _contextDirty)
            {
                if (--budget == 0) throw new InvalidOperationException("Dropdown ownership callbacks did not converge.");
                if (!ReferenceEquals(_current, _desired))
                {
                    if (!ReleaseCurrent(false)) break;
                    var next = _desired;
                    if (next == null) { _contextDirty = false; continue; }
                    if (!Valid(next)) { Withdraw(next); next.SetOpen(false); continue; }
                    _current = next;
                    next.SetOpen(true); // Checked/Unchecked can run arbitrary application code.
                    if (!ReferenceEquals(_desired, next) || !Valid(next)) { Withdraw(next); continue; }
                    if (next.Position is { } point)
                        _menu.ShowAt(next.Owner, new FlyoutShowOptions { Position = point });
                    else _menu.ShowAt(next.Owner);
                    if (!Valid(next)) Withdraw(next);
                }
                if (_contextDirty)
                {
                    _contextDirty = false;
                    if (_current is { } current && ReferenceEquals(current, _desired))
                    {
                        if (Valid(current)) ApplyContext(current);
                        else Withdraw(current);
                    }
                }
            }
        }
        catch (Exception original)
        {
            _cleaningAfterFailure = true;
            _desired = null; _contextDirty = false;
            try { ReleaseCurrent(true); }
            catch (Exception cleanup)
            { throw new AggregateException("Dropdown transition and cleanup failed.", original, cleanup); }
            ExceptionDispatchInfo.Capture(original).Throw();
            throw;
        }
        finally { _cleaningAfterFailure = false; _draining = false; }
    }

    private void Withdraw(Request request)
    { if (ReferenceEquals(_desired, request)) _desired = null; }

    private void ApplyContext(Request request)
    {
        if (!ReferenceEquals(_current, request) || !ReferenceEquals(_desired, request) || !Valid(request)) return;
        _contextRevision = MenuContext.NextRevision(_menu);
        MenuContext.Apply(_menu, request.Context());
    }

    private bool ReleaseCurrent(bool force)
    {
        if (_current is not { } previous) return true;
        _current = null;
        var revision = _contextRevision; _contextRevision = 0;
        List<Exception>? failures = null;
        void Cleanup(Action action)
        { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        if (ReferenceEquals(_menu.Target, previous.Owner))
        {
            Cleanup(_menu.Hide);
            if (!force && failures == null && _menu.IsOpen)
            {
                // FlyoutBase.Closing is cancellable. Keep the active scope coherent
                // rather than presenting an unchecked trigger with a live popup.
                var denied = _desired;
                _current = _desired = previous; _contextRevision = revision;
                _contextDirty = false;
                if (denied != null && !ReferenceEquals(denied.Owner, previous.Owner)) denied.SetOpen(false);
                previous.SetOpen(true);
                return false;
            }
        }
        Cleanup(() => MenuContext.ClearIfRevision(_menu, revision));
        Cleanup(() => previous.SetOpen(false));
        if (failures is { Count: 1 }) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures != null) throw new AggregateException("Dropdown cleanup failed.", failures);
        return true;
    }
}

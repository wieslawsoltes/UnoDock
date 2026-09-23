using System.Runtime.ExceptionServices;
using Xceed.Wpf.AvalonDock.Controls;

namespace Xceed.Wpf.AvalonDock.Internal;

/// <summary>Serializes dropdown requests around application and native callbacks.
/// No original implementation code or private platform state is accessed.</summary>
internal sealed class DropDownMenuSession
{
    private sealed class CloseQueue
    {
        internal readonly HashSet<Slot> Closing = [];
        internal WeakReference<DropDownMenuSession>? WaitingOwner;
        internal WeakReference<MenuFlyout>? WaitingMenu;
        internal long WaitingRevision;
    }
    private sealed class Slot(CloseQueue queue)
    {
        internal readonly CloseQueue Queue = queue;
        internal WeakReference<DropDownMenuSession>? Owner;
        internal FlyoutBaseClosingEventArgs? ClosingArguments;
        internal bool ResumeQueued;
    }
    private sealed class Opening(MenuFlyout menu, XamlRoot root)
    {
        internal readonly MenuFlyout Menu = menu;
        internal readonly XamlRoot Root = root;
        internal bool ContextAssigned, ShowStarted, WasShown;
        internal object? Context;
        internal EventHandler<object>? Preparing, Opened, Closed;
        internal void Subscribe()
        { Menu.Opening += Preparing; Menu.Opened += Opened; Menu.Closed += Closed; }
        internal void Unsubscribe()
        { Menu.Opening -= Preparing; Menu.Opened -= Opened; Menu.Closed -= Closed; }
    }
    private static readonly ConditionalWeakTable<MenuFlyout, Slot> Owners = new();
    // Menu dismissal can close the next menu in the native thread's popup chain.
    // Fence different menus as well as repeat openings of the same instance.
    [ThreadStatic] private static CloseQueue? _threadCloseQueue;
    private readonly Control _owner;
    private readonly Func<MenuFlyout?> _menu;
    private readonly Func<object?> _context;
    private readonly Action<bool> _state;
    private Opening? _active;
    private Point? _position;
    private bool _wanted, _pending, _draining, _cleaning, _releasing, _retryQueued;
    private int _transferRetries;
    private long _revision;

    internal DropDownMenuSession(Control owner, Func<MenuFlyout?> menu, Func<object?> context, Action<bool> state)
    { _owner = owner; _menu = menu; _context = context; _state = state; }
    internal bool IsRequested => _wanted;
    internal bool IsOpen => _active is { } active && active.Menu.IsOpen;
    internal void Open(Point? position = null)
    { _position = position; _transferRetries = 0; Request(true); }
    internal void Close() => Request(false);
    internal void Refresh() { if (_wanted || _active != null) Request(_wanted); }
    private bool CanOpen => _owner.IsEnabled && _owner.IsLoaded && _owner.XamlRoot != null;

    private static Slot GetSlot(MenuFlyout menu) => Owners.GetValue(menu, key =>
    {
        var slot = new Slot(_threadCloseQueue ??= new());
        // Retained delegates reference only their menu and weak ownership slots.
        key.Closing += (_, args) => slot.ClosingArguments = args;
        key.Closed += (_, _) =>
        {
            if (key.IsOpen) return;
            slot.Queue.Closing.Add(slot);
            QueueAfterClosed(key, slot);
        };
        return slot;
    });
    private static void QueueAfterClosed(MenuFlyout menu, Slot slot)
    {
        if (slot.ResumeQueued) return;
        slot.ResumeQueued = true;
        if (!menu.DispatcherQueue.TryEnqueue(() =>
        {
            slot.ResumeQueued = false; slot.Queue.Closing.Remove(slot);
            if (slot.Queue.Closing.Count != 0) return;
            var queue = slot.Queue;
            var pending = queue.WaitingOwner; var pendingMenu = queue.WaitingMenu; var revision = queue.WaitingRevision;
            queue.WaitingOwner = null; queue.WaitingMenu = null;
            if (pending?.TryGetTarget(out var owner) == true && pendingMenu?.TryGetTarget(out var target) == true &&
                owner._wanted && owner._revision == revision && ReferenceEquals(owner._menu(), target)) owner.Request(true);
        }))
        {
            slot.ResumeQueued = false; slot.Queue.Closing.Remove(slot);
            slot.Queue.WaitingOwner = null; slot.Queue.WaitingMenu = null;
        }
    }
    private void WaitForClose(MenuFlyout menu, CloseQueue queue)
    {
        if (queue.WaitingOwner?.TryGetTarget(out var previous) == true && !ReferenceEquals(previous, this))
        {
            // Superseded waiters own no native opening. Withdraw their intent so
            // a later DataContext change cannot revive an obsolete request.
            previous._wanted = false; previous._revision++;
        }
        queue.WaitingOwner = new(this); queue.WaitingMenu = new(menu); queue.WaitingRevision = _revision;
        _state(false);
    }

    private void Request(bool wanted)
    {
        if (_owner.DispatcherQueue is { HasThreadAccess: false })
            throw new InvalidOperationException("Dropdown operations require their owning UI thread.");
        if (_cleaning || (_releasing && !wanted && !_wanted)) return;
        _wanted = wanted; _pending = true; _revision++;
        if (_draining) return;
        _draining = true;
        try
        {
            var budget = 64;
            while (_pending)
            {
                if (--budget == 0) throw new InvalidOperationException("Dropdown callbacks did not converge.");
                _pending = false;
                Reconcile();
            }
        }
        catch (Exception original)
        {
            _cleaning = true; _wanted = false;
            try { Release(); }
            catch (Exception cleanup) { throw new AggregateException("Dropdown operation and cleanup failed.", original, cleanup); }
            ExceptionDispatchInfo.Capture(original).Throw();
            throw;
        }
        finally { _pending = false; _draining = false; _cleaning = false; }
    }

    private void Reconcile()
    {
        var revision = _revision;
        var menu = _menu();
        if (!_wanted || !CanOpen || menu == null)
        {
            _wanted = false;
            if (!Release()) return;
            if (revision == _revision) _state(false);
            return;
        }
        if (_active is { } previous && (!ReferenceEquals(previous.Menu, menu) || !ReferenceEquals(previous.Root, _owner.XamlRoot) ||
            (previous.WasShown && !previous.Menu.IsOpen)))
        {
            if (!Release() || revision != _revision) return;
        }
        if (_active == null)
        {
            var slot = GetSlot(menu);
            if (slot.Queue.Closing.Count != 0) { WaitForClose(menu, slot.Queue); return; }
            if (slot.Owner?.TryGetTarget(out var current) == true && !ReferenceEquals(current, this))
            {
                current.Close();
                if (revision != _revision) return;
                if (current.IsOpen)
                {
                    // Native Closing was cancelled; never steal its live context.
                    _wanted = false; _state(false); return;
                }
                if (slot.Queue.Closing.Count != 0) { WaitForClose(menu, slot.Queue); return; }
                if (slot.Owner?.TryGetTarget(out current) == true && !ReferenceEquals(current, this))
                { Defer(revision); return; }
            }
            var active = new Opening(menu, _owner.XamlRoot!);
            _active = active; slot.Owner = new(this);
            active.Preparing = (_, _) =>
            {
                if (ReferenceEquals(_active, active) && _wanted &&
                    menu is ContextMenuEx { ItemsSource: not null, MenuDataContext: null })
                    MenuContext.Apply(menu, _context());
            };
            active.Opened = (_, _) =>
            {
                if (!ReferenceEquals(_active, active)) return;
                active.WasShown = true;
                if (!_wanted || !CanOpen || !ReferenceEquals(_menu(), menu) || !ReferenceEquals(_owner.XamlRoot, active.Root))
                { Close(); return; }
                Refresh();
            };
            active.Closed = (_, _) =>
            {
                if (ReferenceEquals(_active, active) && !menu.IsOpen) Close();
            };
            active.Subscribe();
        }
        var opening = _active!;
        var context = menu is ContextMenuEx { MenuDataContext: { } explicitContext } ? explicitContext : _context();
        if (!opening.ContextAssigned || !ReferenceEquals(opening.Context, context))
        {
            // Own the scope before its assignment invokes application callbacks.
            opening.ContextAssigned = true; opening.Context = context;
            MenuContext.Apply(menu, context);
            if (revision != _revision || !ReferenceEquals(_active, opening)) return;
        }
        if (!CanOpen || !ReferenceEquals(_owner.XamlRoot, opening.Root) || !ReferenceEquals(_menu(), menu))
        { Close(); return; }
        if (!opening.ShowStarted)
        {
            opening.ShowStarted = true;
            if (_position is { } point) menu.ShowAt(_owner, new FlyoutShowOptions { Position = point });
            else menu.ShowAt(_owner);
            opening.WasShown |= menu.IsOpen;
        }
        if (revision == _revision && ReferenceEquals(_active, opening)) _state(menu.IsOpen);
    }

    private void Defer(long revision)
    {
        if (_retryQueued) return;
        if (++_transferRetries > 1) { _wanted = false; _state(false); return; }
        _retryQueued = true;
        if (!_owner.DispatcherQueue.TryEnqueue(() =>
        {
            _retryQueued = false;
            if (_revision == revision && _wanted) Request(true);
        }))
        { _retryQueued = false; _wanted = false; _state(false); }
    }

    /// <returns>False when the application vetoed native closing.</returns>
    private bool Release()
    {
        if (_active is not { } active) return true;
        _active = null; active.Unsubscribe(); _releasing = true;
        var menu = active.Menu;
        var slot = GetSlot(menu);
        var owns = slot.Owner?.TryGetTarget(out var owner) == true && ReferenceEquals(owner, this);
        var retained = false;
        List<Exception>? failures = null;
        void Attempt(Action action)
        { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        try
        {
            if (owns)
            {
                var awaitingNativeClose = active.WasShown || menu.IsOpen;
                if (active.ShowStarted)
                {
                    // Hold ownership through native Closing before releasing data.
                    // A cancelled close retains the exact opening and row scope.
                    slot.Queue.Closing.Add(slot); slot.ClosingArguments = null;
                    Attempt(menu.Hide);
                    if (failures == null && menu.IsOpen && slot.ClosingArguments?.Cancel == true)
                    {
                        retained = true; _active = active; _wanted = true; active.Subscribe();
                        QueueAfterClosed(menu, slot); _state(true);
                        return false;
                    }
                    if (!awaitingNativeClose && !menu.IsOpen) QueueAfterClosed(menu, slot);
                }
                // The shared lease is still held. Nested takeovers cannot clear a
                // successor's context while these application callbacks execute.
                Attempt(() => MenuContext.Clear(menu));
            }
            Attempt(() => _state(false));
        }
        finally
        {
            _releasing = false;
            if (!retained)
            {
                if (owns && slot.Owner?.TryGetTarget(out owner) == true && ReferenceEquals(owner, this)) slot.Owner = null;
                active.Context = null;
            }
        }
        if (failures?.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures != null) throw new AggregateException("Dropdown cleanup failed.", failures);
        return true;
    }
}

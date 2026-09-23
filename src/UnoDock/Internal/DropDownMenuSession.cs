using System.Runtime.ExceptionServices;
using Xceed.Wpf.AvalonDock.Controls;

namespace Xceed.Wpf.AvalonDock.Internal;

/// <summary>One UI-thread opening owned by a dropdown trigger. Context assignment,
/// checked state and native Show/Hide are all application-callback boundaries.</summary>
internal sealed class DropDownMenuSession
{
    private sealed class Slot
    {
        internal WeakReference<DropDownMenuSession>? Owner, WaitingOwner;
        internal long WaitingRevision;
        internal bool Closing, ResumeQueued;
    }
    private sealed class Opening(MenuFlyout menu, XamlRoot root)
    {
        internal readonly MenuFlyout Menu = menu;
        internal readonly XamlRoot Root = root;
        internal bool ContextAssigned, ShowStarted, WasShown;
        internal object? Context;
        internal EventHandler<object>? Preparing, Opened, Closed;
    }
    private static readonly ConditionalWeakTable<MenuFlyout, Slot> Owners = new();
    private readonly Control _owner;
    private readonly Func<MenuFlyout?> _menu;
    private readonly Func<object?> _context;
    private readonly Action<bool> _state;
    private Opening? _active;
    private Point? _position;
    private bool _wanted, _pending, _draining, _cleaning, _retryQueued;
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
        var slot = new Slot();
        // The retained delegate references only its menu and weak owner slots.
        // Native IsOpen may become false before Closed, and ShowAt may be ignored
        // until the entire Closed callback returns. Do not rehost inside Closed.
        key.Closed += (_, _) =>
        {
            if (key.IsOpen) return;
            slot.Closing = true;
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
            slot.ResumeQueued = false; slot.Closing = false;
            var pending = slot.WaitingOwner; var revision = slot.WaitingRevision;
            slot.WaitingOwner = null;
            if (pending?.TryGetTarget(out var owner) == true && owner._wanted && owner._revision == revision &&
                ReferenceEquals(owner._menu(), menu)) owner.Request(true);
        }))
        { slot.ResumeQueued = false; slot.Closing = false; slot.WaitingOwner = null; }
    }
    private void WaitForClose(Slot slot)
    { slot.WaitingOwner = new(this); slot.WaitingRevision = _revision; _state(false); }

    private void Request(bool wanted)
    {
        if (_owner.DispatcherQueue is { HasThreadAccess: false })
            throw new InvalidOperationException("Dropdown operations require their owning UI thread.");
        if (_cleaning) return;
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
            _wanted = false; Release();
            if (revision == _revision) _state(false);
            return;
        }
        if (_active is { } previous && (!ReferenceEquals(previous.Menu, menu) || !ReferenceEquals(previous.Root, _owner.XamlRoot) ||
            (previous.WasShown && !previous.Menu.IsOpen)))
        {
            Release();
            if (revision != _revision) return;
        }
        if (_active == null)
        {
            var slot = GetSlot(menu);
            if (slot.Closing) { WaitForClose(slot); return; }
            if (slot.Owner?.TryGetTarget(out var current) == true && !ReferenceEquals(current, this))
            {
                current.Close();
                if (revision != _revision) return;
                if (slot.Closing) { WaitForClose(slot); return; }
                // A second trigger can arrive while preparation invokes user code.
                if (slot.Owner?.TryGetTarget(out current) == true && !ReferenceEquals(current, this))
                { Defer(revision); return; }
            }
            var active = new Opening(menu, _owner.XamlRoot!);
            _active = active; slot.Owner = new(this);
            active.Preparing = (_, _) =>
            {
                // The source container generates its rows during Opening. Apply
                // trigger context to the new rows only in the absence of an
                // explicit context declared by the menu itself.
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
            menu.Opening += active.Preparing; menu.Opened += active.Opened; menu.Closed += active.Closed;
        }
        var opening = _active!;
        var context = _context();
        if (!opening.ContextAssigned || !ReferenceEquals(opening.Context, context))
        {
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

    private void Release()
    {
        if (_active is not { } active) return;
        _active = null;
        var menu = active.Menu;
        menu.Opening -= active.Preparing; menu.Opened -= active.Opened; menu.Closed -= active.Closed;
        var slot = GetSlot(menu);
        var owns = slot.Owner?.TryGetTarget(out var owner) == true && ReferenceEquals(owner, this);
        List<Exception>? failures = null;
        void Attempt(Action action)
        { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        try
        {
            if (owns)
            {
                var awaitingNativeClose = active.WasShown || menu.IsOpen;
                // Gate BEFORE cleanup callbacks can request another owner. Keep
                // it gated beyond Hide until native Closed has finished dispatch.
                if (active.ShowStarted) slot.Closing = true;
                Attempt(() => MenuContext.Clear(menu));
                if (active.ShowStarted)
                {
                    Attempt(menu.Hide);
                    // A preparation cancelled before actual native showing has
                    // no Closed event; unwind its callbacks once before retrying.
                    if (!awaitingNativeClose && !menu.IsOpen) QueueAfterClosed(menu, slot);
                }
            }
            Attempt(() => _state(false));
        }
        finally
        {
            if (owns && slot.Owner?.TryGetTarget(out owner) == true && ReferenceEquals(owner, this)) slot.Owner = null;
            active.Context = null;
        }
        if (failures?.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures != null) throw new AggregateException("Dropdown cleanup failed.", failures);
    }
}

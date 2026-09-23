using System.Runtime.ExceptionServices;
using Xceed.Wpf.AvalonDock.Controls;

namespace Xceed.Wpf.AvalonDock.Internal;

/// <summary>One UI-thread opening owned by a dropdown trigger. Context assignment,
/// checked state and native Show/Hide are all application-callback boundaries.</summary>
internal sealed class DropDownMenuSession
{
    private sealed class Slot { internal WeakReference<DropDownMenuSession>? Owner; }
    private sealed class Opening(MenuFlyout menu, XamlRoot root)
    {
        internal readonly MenuFlyout Menu = menu;
        internal readonly XamlRoot Root = root;
        internal bool ContextAssigned, ShowStarted;
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
        if (_active is { } previous && (!ReferenceEquals(previous.Menu, menu) || !ReferenceEquals(previous.Root, _owner.XamlRoot)))
        {
            Release();
            if (revision != _revision) return;
        }
        if (_active == null)
        {
            var slot = Owners.GetOrCreateValue(menu);
            if (slot.Owner?.TryGetTarget(out var current) == true && !ReferenceEquals(current, this))
            {
                current.Close();
                if (revision != _revision) return;
                // A second trigger can arrive from the prior owner's cleanup.
                // Allow one dispatcher retry after that callback stack unwinds.
                if (slot.Owner?.TryGetTarget(out current) == true && !ReferenceEquals(current, this))
                { Defer(revision); return; }
            }
            var active = new Opening(menu, _owner.XamlRoot!);
            _active = active; slot.Owner = new(this);
            active.Preparing = (_, _) =>
            {
                // ContextMenuEx generates its rows from ItemsSource in Opening.
                // Its explicit MenuDataContext has precedence; otherwise apply
                // the trigger context to the newly generated rows as well.
                if (ReferenceEquals(_active, active) && _wanted &&
                    menu is ContextMenuEx { ItemsSource: not null, MenuDataContext: null })
                    MenuContext.Apply(menu, _context());
            };
            active.Opened = (_, _) =>
            {
                if (!ReferenceEquals(_active, active)) return;
                if (!_wanted || !CanOpen || !ReferenceEquals(_menu(), menu) || !ReferenceEquals(_owner.XamlRoot, active.Root))
                { Close(); return; }
                Refresh();
            };
            active.Closed = (_, _) =>
            {
                // A late event cannot tear down a replacement opening.
                if (ReferenceEquals(_active, active) && !menu.IsOpen) Close();
            };
            menu.Opening += active.Preparing; menu.Opened += active.Opened; menu.Closed += active.Closed;
        }
        var opening = _active!;
        var context = _context();
        if (!opening.ContextAssigned || !ReferenceEquals(opening.Context, context))
        {
            // Record ownership BEFORE DataContextChanged can replace or close it.
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
        var slot = Owners.GetOrCreateValue(menu);
        var owns = slot.Owner?.TryGetTarget(out var owner) == true && ReferenceEquals(owner, this);
        List<Exception>? failures = null;
        void Attempt(Action action)
        { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        try
        {
            // Do not release the shared lease until cleanup AND native Hide
            // finish. A reentrant new owner must not lose its context to us.
            if (owns)
            {
                Attempt(() => MenuContext.Clear(menu));
                if (active.ShowStarted) Attempt(menu.Hide);
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

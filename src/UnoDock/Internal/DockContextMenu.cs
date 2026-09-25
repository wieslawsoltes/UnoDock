using Microsoft.UI.Xaml.Automation;
using UnoDock.Controls;
using UnoDock.Layout;
using Strings = UnoDock.Properties.Resources;

namespace UnoDock.Internal;
// An item owns its menu. Neither a global cache nor a subscribed application
// command keeps an obsolete layout alive after the opening session has ended.
internal sealed class DockContextMenu : MenuFlyout, IDisposable
{
    private LayoutItem? _item;
    private DockingManager? _manager;
    private LayoutRoot? _root;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
    private readonly List<Entry> _entries = [];
    private volatile bool _disposed, _open;
    private bool _refreshing, _dirty;
    private int _refreshScheduled;
    private long _version;
    private DockMenuPalette? _palette;
    internal DockContextMenu(LayoutItem item, DockingManager manager)
    {
        _item = item;
        _manager = manager;
        _root = manager.Layout;
        _dispatcher = manager.DispatcherQueue;
        if (item is LayoutAnchorableItem tool)
        {
            Add("FloatCommand", () => Strings.Anchorable_Float, () => item.FloatCommand);
            Add("DockCommand", () => Strings.Anchorable_Dock, () => tool.DockCommand);
            Add("DockAsDocumentCommand", () => Strings.Anchorable_DockAsDocument, () => item.DockAsDocumentCommand);
            Add("AutoHideCommand", () => item.LayoutElement is LayoutAnchorable { IsAutoHidden: true } ? Strings.Window_Restore : Strings.Anchorable_AutoHide, () => tool.AutoHideCommand);
            Add("CloseCommand", () => Strings.Document_Close, () => item.CloseCommand, () => item.CanClose);
            Add("HideCommand", () => Strings.Anchorable_Hide, () => tool.HideCommand);
        }
        else
        {
            Add("CloseCommand", () => Strings.Document_Close, () => item.CloseCommand, () => item.CanClose);
            Add("CloseAllButThisCommand", () => Strings.Document_CloseAllButThis, () => item.CloseAllButThisCommand);
            Add("CloseAllCommand", () => Strings.Document_CloseAll, () => item.CloseAllCommand);
            Add("FloatCommand", () => Strings.Document_Float, () => item.FloatCommand);
            Add("DockAsDocumentCommand", () => Strings.Document_DockAsDocument, () => item.DockAsDocumentCommand);
            Add("NewHorizontalTabGroupCommand", () => Strings.Document_NewHorizontalTabGroup, () => item.NewHorizontalTabGroupCommand, collapseDisabled: true);
            Add("NewVerticalTabGroupCommand", () => Strings.Document_NewVerticalTabGroup, () => item.NewVerticalTabGroupCommand, collapseDisabled: true);
            Add("MoveToNextTabGroupCommand", () => Strings.Document_MoveToNextTabGroup, () => item.MoveToNextTabGroupCommand, collapseDisabled: true);
            Add("MoveToPreviousTabGroupCommand", () => Strings.Document_MoveToPreviousTabGroup, () => item.MoveToPreviousTabGroupCommand, collapseDisabled: true);
        }

        Opening += OnOpening;
        Closed += OnClosed;
        Refresh();
    }

    private void Add(string id, Func<string> label, Func<ICommand?> command, Func<bool>? visible = null, bool collapseDisabled = false)
    {
        var entry = new Entry(this, id, label, command, visible, collapseDisabled);
        _entries.Add(entry);
        Items.Add(entry.Row);
    }

    private bool Valid => !_disposed && _item is { } item && _manager is { } manager && _root is { } root && (item is LayoutAnchorableItem ? manager.AnchorableContextMenu : manager.DocumentContextMenu) == null && ReferenceEquals(item.LayoutElement.Root, root) && ReferenceEquals(manager.Layout, root) && ReferenceEquals(root.Manager, manager);

    internal void Refresh()
    {
        if (_disposed)
            return;
        _dirty = true;
        if (_refreshing)
            return;
        _refreshing = true;
        try
        {
            var budget = 32;
            while (_dirty)
            {
                if (--budget == 0)
                    throw new InvalidOperationException("Docking menu callbacks did not converge.");
                _dirty = false;
                var palette = DockMenuPalette.Resolve(_manager!);
                if (_palette != palette)
                {
                    _palette = palette;
                    MenuFlyoutPresenterStyle = DockMenuRow.PresenterStyle(palette);
                }

                var focused = _open && _manager?.XamlRoot != null ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(_manager.XamlRoot) as DockMenuRow : null;
                foreach (var entry in _entries)
                {
                    if (_disposed)
                        return;
                    entry.Refresh(palette);
                }

                if (_open && focused != null && (!focused.IsEnabled || focused.Visibility != Visibility.Visible))
                {
                    var position = _entries.FindIndex(e => ReferenceEquals(e.Row, focused));
                    if (position >= 0)
                    {
                        var replacement = _entries.Skip(position + 1).Concat(_entries.Take(position)).Select(e => e.Row).FirstOrDefault(r => r.IsEnabled && r.Visibility == Visibility.Visible);
                        replacement?.Focus(FocusState.Keyboard);
                    }
                }
            }
        }
        finally
        {
            _refreshing = false;
            _dirty = false;
        }
    }

    internal void Suspend()
    {
        EndOpening();
        Hide();
    }

    private void OnOpening(object? sender, object e)
    {
        EndOpening();
        if (!Valid)
        {
            QueueHide();
            return;
        }

        _open = true;
        _root!.Updated += Updated;
        _manager!.LayoutChanging += LayoutChanging;
        try
        {
            Refresh();
        }
        catch
        {
            EndOpening();
            Hide();
            throw;
        }
    }

    private void QueueHide()
    {
        var version = System.Threading.Volatile.Read(ref _version);
        _dispatcher.TryEnqueue(() =>
        {
            if (version == _version && !Valid)
                Hide();
        });
    }

    private void Updated(object? sender, EventArgs e) => QueueRefresh();
    private void LayoutChanging(object? sender, EventArgs e)
    {
        EndOpening();
        Hide();
    }

    private void OnClosed(object? sender, object e) => EndOpening();
    private void EndOpening()
    {
        _open = false;
        System.Threading.Interlocked.Exchange(ref _refreshScheduled, 0);
        System.Threading.Interlocked.Increment(ref _version);
        if (_root is { } root)
            root.Updated -= Updated;
        if (_manager is { } manager)
            manager.LayoutChanging -= LayoutChanging;
        foreach (var entry in _entries)
            entry.Unsubscribe();
    }

    private void QueueRefresh()
    {
        // Application ICommand events may originate on worker threads. Only the
        // queued continuation reads models or touches dependency objects.
        if (!_open || _disposed || System.Threading.Interlocked.CompareExchange(ref _refreshScheduled, 1, 0) != 0)
            return;
        var version = System.Threading.Volatile.Read(ref _version);
        if (!_dispatcher.TryEnqueue(() =>
        {
            if (version != System.Threading.Volatile.Read(ref _version) || !_open || _disposed)
                return;
            System.Threading.Interlocked.Exchange(ref _refreshScheduled, 0);
            if (!Valid)
            {
                Suspend();
                return;
            }

            try
            {
                Refresh();
            }
            catch
            {
                Suspend();
                throw;
            }
        }))
            System.Threading.Interlocked.Exchange(ref _refreshScheduled, 0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        EndOpening();
        // Keep the invalid-opening guard on an externally retained old flyout.
        // The self-subscription cannot root it; all model/command subscriptions end.
        foreach (var entry in _entries)
            entry.Release();
        _item = null;
        _manager = null;
        _root = null;
        _palette = null;
        Hide();
    }

    private sealed class Entry : ICommand
    {
        private readonly DockContextMenu _owner;
        private Func<string> _label;
        private Func<ICommand?> _resolve;
        private Func<bool>? _visible;
        private readonly bool _collapseDisabled;
        private ICommand? _subscribed;
        private bool _enabled;
        internal DockMenuRow Row
        {
            get;
        }

        internal Entry(DockContextMenu owner, string id, Func<string> label, Func<ICommand?> resolve, Func<bool>? visible, bool collapseDisabled)
        {
            _owner = owner;
            _label = label;
            _resolve = resolve;
            _visible = visible;
            _collapseDisabled = collapseDisabled;
            Row = new()
            {
                Name = "PART_" + id,
                Tag = id,
                Command = this
            };
        }

        public event EventHandler? CanExecuteChanged;
        private bool Check(ICommand? command) => command != null && _owner.Valid && _owner._item!.LayoutElement.IsEnabled && (_visible?.Invoke() ?? true) && command.CanExecute(null) && _owner.Valid && _owner._item!.LayoutElement.IsEnabled && ReferenceEquals(command, _resolve());
        public bool CanExecute(object? parameter) => Check(_resolve());
        public void Execute(object? parameter)
        {
            var command = _resolve();
            if (!Check(command))
                return;
            // Querying an application command may replace its model/command.
            // Check() revalidates both before delegating to this exact command.
            try
            {
                command!.Execute(null);
            }
            finally
            {
                _owner.Refresh();
            }
        }

        internal void Refresh(DockMenuPalette palette)
        {
            var command = _resolve();
            if (_owner._open && !ReferenceEquals(command, _subscribed))
            {
                Unsubscribe();
                _subscribed = command;
                if (_subscribed != null)
                    _subscribed.CanExecuteChanged += Changed;
            }

            var allowed = Check(command);
            if (_owner._disposed)
                return;
            Row.Text = _label();
            AutomationProperties.SetName(Row, Row.Text);
            var visible = _owner.Valid && (_visible?.Invoke() ?? true) && (!_collapseDisabled || allowed);
            Row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            Row.Configure(palette);
            Row.IsEnabled = allowed;
            if (_enabled != allowed)
            {
                _enabled = allowed;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Changed(object? sender, EventArgs args) => _owner.QueueRefresh();
        internal void Unsubscribe()
        {
            var command = _subscribed;
            _subscribed = null;
            if (command != null)
                command.CanExecuteChanged -= Changed;
        }

        internal void Release()
        {
            Unsubscribe();
            _resolve = static () => null;
            _label = static () => "";
            _visible = null;
            Row.IsEnabled = false;
            _enabled = false;
            Row.Command = null;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

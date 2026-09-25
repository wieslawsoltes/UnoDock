using Microsoft.UI.Xaml.Input;
using UnoDock.Controls;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Themes;

namespace UnoDock;

[TemplatePart(Name = "PART_AutoHideArea")]
[ContentProperty(Name = nameof(Layout))]
public partial class DockingManager : Control, IDisposable, UnoDock.Compatibility.IWeakEventListener
{
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(LayoutRoot), typeof(DockingManager), new PropertyMetadata(null, (d, e) => ((DockingManager)d).ChangeLayout((LayoutRoot?)e.OldValue, (LayoutRoot?)e.NewValue)));
    public static readonly DockRoutedEvent PreviewDockEvent = new(nameof(PreviewDock)), DockedEvent = new(nameof(Docked)), PreviewFloatEvent = new(nameof(PreviewFloat)), FloatedEvent = new(nameof(Floated));
    private readonly UpdateBatch _updates;
    private readonly DesktopWindowCoordinates _ownedCoordinates = new();
    private readonly Dictionary<LayoutContent, LayoutItem> _items = new(ReferenceEqualityComparer.Instance);
    private readonly ObservableCollection<LayoutFloatingWindowControl> _floating = [];
    private readonly Dictionary<DockRoutedEvent, List<RoutedEventHandler>> _handlers = [];
    private readonly List<SourceEntry> _documents = [], _anchorables = [];
    private SourceObserver? _documentObserver, _anchorableObserver;
    private DockSurface? _surface;
    private ContentPresenter? _host;
    private bool _loaded, _changingLayout, _initialized;
    private LayoutRoot? _attachedLayout;
    private readonly Dictionary<LayoutContent, bool> _transitions = new(ReferenceEqualityComparer.Instance);
    private bool _renderPending, _disposed, _syncActive, _reconcilingSources;
    private int _suspendSources;
    private ResourceDictionary? _themeResources;
    internal LayoutRoot? LastRenderedLayout
    {
        get; private set;
    }

    public DockingManager()
    {
        DefaultStyleKey = typeof(DockingManager);
        IsTabStop = false;
        _updates = new(ScheduleRender);
        ActualThemeChanged += (_, _) => InvalidateView();
        CrossWindowCoordinates = _ownedCoordinates;
        _floating.CollectionChanged += (_, args) => LayoutFloatingWindowControlCollectionChanged?.Invoke(this, new(args));
        SetValue(LayoutProperty, new LayoutRoot());
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public LayoutRoot Layout
    {
        get => (LayoutRoot)GetValue(LayoutProperty);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Manager != null && !ReferenceEquals(value.Manager, this))
                throw new InvalidOperationException("A layout cannot be owned by two docking managers.");
            SetValue(LayoutProperty, value);
        }
    }

    public ICrossWindowCoordinates? CrossWindowCoordinates
    {
        get; set;
    }

    internal void SetAutoHideHost(LayoutAutoHideWindowControl? value) => SetAutoHideWindow(value!);
    public FloatingWindowMode FloatingWindowMode { get; set; } = FloatingWindowMode.Auto;
    public IEnumerable<LayoutFloatingWindowControl> FloatingWindows => _floating;
    public int RealizedContentCount => _items.Values.Count(i => i.IsViewCreated);
    public IEnumerator LogicalChildrenPublic => LogicalChildren;
    /// <summary>Snapshot of owned realized views and floating controls. This is a
        /// compatibility enumeration, not a replacement for the native XAML logical tree.</summary>
        protected virtual IEnumerator LogicalChildren => _items.Values.Select(i => i.ExistingView).OfType<object>().Concat(_floating).ToArray().GetEnumerator();

    /// <summary>Called on first loading, after the derived constructor has completed.</summary>
    protected virtual void OnInitialized(EventArgs e)
    {
    }

    public IReadOnlyList<IDropArea> GetDropAreas() => _surface?.GetDropAreas() ?? [];
    public DockDropPlan? GetDropPlan(LayoutContent content, Point surfacePoint) => _surface?.GetDropPlan(content, surfacePoint);
    public event EventHandler? ActiveContentChanged, LayoutChanged, LayoutChanging;
    public event EventHandler<DocumentClosingEventArgs>? DocumentClosing;
    public event EventHandler<DocumentClosedEventArgs>? DocumentClosed;
    public event EventHandler<LayoutFloatingWindowControlCollectionChangedEventArgs>? LayoutFloatingWindowControlCollectionChanged;
    public event RoutedEventHandler? PreviewDock, Docked, PreviewFloat, Floated;
    public event EventHandler<Exception>? RenderingFailed;
    public IDisposable BeginLayoutUpdate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _suspendSources++;
        return new ActionDisposable(() =>
        {
            if (--_suspendSources == 0)
            {
                ReconcileSources();
                InvalidateView();
            }
        });
    }

    private void ChangeLayout(LayoutRoot? oldLayout, LayoutRoot? newLayout)
    {
        if (ReferenceEquals(oldLayout, newLayout) || _changingLayout)
            return;
        _changingLayout = true;
        try
        {
            // DP callbacks can replace Layout again. Drain the final value rather
            // than attaching a stale argument from an outer callback.
            for (var iteration = 0; iteration < 64; iteration++)
            {
                var candidate = GetValue(LayoutProperty) as LayoutRoot;
                if (candidate == null)
                {
                    SetValue(LayoutProperty, new LayoutRoot());
                    continue;
                }

                if (candidate.Manager != null && !ReferenceEquals(candidate.Manager, this))
                    throw new InvalidOperationException("A layout cannot be owned by two docking managers.");
                if (ReferenceEquals(candidate, _attachedLayout))
                    return;
                var previous = _attachedLayout;
                LayoutChanging?.Invoke(this, EventArgs.Empty);
                if (!ReferenceEquals(candidate, GetValue(LayoutProperty)))
                    continue;
                if (candidate.Manager != null && !ReferenceEquals(candidate.Manager, this))
                    throw new InvalidOperationException("A layout acquired another owner during LayoutChanging.");
                if (previous != null)
                {
                    previous.Updated -= OnLayoutModelUpdated;
                    if (ReferenceEquals(previous.Manager, this))
                        previous.Manager = null;
                }

                _attachedLayout = null;
                var windows = _floating.ToArray();
                _floating.Clear();
                var items = _items.Values.ToArray();
                _items.Clear();
                _documents.Clear();
                _anchorables.Clear();
                foreach (var window in windows)
                    window.CloseHost();
                foreach (var item in items)
                    item.Dispose();
                _surface?.Reset();
                if (!ReferenceEquals(candidate, GetValue(LayoutProperty)))
                    continue;
                _attachedLayout = candidate;
                candidate.Manager = this;
                candidate.Updated += OnLayoutModelUpdated;
                OnLayoutChanged(previous!, candidate);
                if (!ReferenceEquals(candidate, GetValue(LayoutProperty)))
                    continue;
                ReconcileSources();
                if (!ReferenceEquals(candidate, GetValue(LayoutProperty)))
                    continue;
                SyncActive();
                InvalidateView();
                LayoutChanged?.Invoke(this, EventArgs.Empty);
                if (ReferenceEquals(candidate, GetValue(LayoutProperty)))
                    return;
            }

            throw new InvalidOperationException("Layout callbacks did not converge after 64 replacements.");
        }
        catch
        {
            // In particular, direct SetValue must not bypass CLR ownership checks.
            // Revert only the DP value; an already attached root keeps its owner.
            SetValue(LayoutProperty, _attachedLayout ?? oldLayout ?? new LayoutRoot());
            throw;
        }
        finally
        {
            _changingLayout = false;
        }
    }

    protected virtual void OnLayoutChanged(LayoutRoot oldLayout, LayoutRoot newLayout)
    {
    }

    internal void PropertyChanged(string name, DependencyPropertyChangedEventArgs e)
    {
        switch (name)
        {
            case nameof(ActiveContent):
                if (!_syncActive)
                {
                    var selected = e.NewValue as LayoutContent ?? Layout.Descendents().OfType<LayoutContent>().FirstOrDefault(c => ReferenceEquals(c.Content, e.NewValue));
                    if (selected != null && selected.Root == Layout)
                        selected.IsActive = true;
                    else if (e.NewValue == null)
                        Layout.ActiveContent = null;
                }

                ActiveContentChanged?.Invoke(this, EventArgs.Empty);
                break;
            case nameof(DocumentsSource):
                _documentObserver?.Dispose();
                _documentObserver = DocumentsSource == null ? null : new(this, DocumentsSource, true);
                ReconcileSources();
                break;
            case nameof(AnchorablesSource):
                _anchorableObserver?.Dispose();
                _anchorableObserver = AnchorablesSource == null ? null : new(this, AnchorablesSource, false);
                ReconcileSources();
                break;
            case nameof(Theme):
                if (_themeResources != null)
                    Resources.MergedDictionaries.Remove(_themeResources);
                _themeResources = Theme?.GetResourceDictionary();
                if (_themeResources != null)
                    Resources.MergedDictionaries.Add(_themeResources);
                break;
            case nameof(LayoutItemContainerStyle):
            case nameof(LayoutItemContainerStyleSelector):
                foreach (var item in _items.Values)
                    ApplyItemStyle(item);
                break;
        }

        InvalidateView();
    }

    private void OnLayoutModelUpdated(object? sender, EventArgs e)
    {
        SyncActive();
        InvalidateView();
    }

    private void SyncActive()
    {
        if (_syncActive)
            return;
        var content = Layout.ActiveContent?.Content;
        if (ReferenceEquals(content, ActiveContent))
            return;
        _syncActive = true;
        try
        {
            SetValue(ActiveContentProperty, content);
        }
        finally
        {
            _syncActive = false;
        }
    }

    internal void InvalidateView()
    {
        if (!_disposed)
            _updates.Invalidate();
    }

    private void ScheduleRender()
    {
        if (_renderPending || _host == null || _disposed || !_loaded)
            return;
        _renderPending = true;
        if (DispatcherQueue?.TryEnqueue(() =>
        {
            _renderPending = false;
            if (!_disposed && _loaded)
                RenderNow();
        }) != true)
            _renderPending = false;
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RenderNow();
    }

    internal void RenderNow()
    {
        if (_host == null)
            return;
        _surface ??= new(this);
        if (!ReferenceEquals(_host.Content, _surface))
            _host.Content = _surface;
        try
        {
            _surface.Update();
            SyncFloatingWindows();
            foreach (var key in _items.Keys.Where(c => !ReferenceEquals(c.Root, Layout)).ToArray())
            {
                _items[key].Dispose();
                _items.Remove(key);
            }

            LastRenderedLayout = Layout;
        }
        catch (Exception ex)
        {
            RenderingFailed?.Invoke(this, ex);
            throw;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_LayoutHost") as ContentPresenter;
        if (_host != null)
            RenderNow();
    }

    protected override Size ArrangeOverride(Size arrangeBounds) => base.ArrangeOverride(arrangeBounds);
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed)
            return;
        _loaded = true;
        if (!_initialized)
        {
            _initialized = true;
            OnInitialized(EventArgs.Empty);
        }

        // A derived callback can dispose this manager or replace its layout.
        if (!_disposed && _loaded)
        {
            ReconcileSources();
            InvalidateView();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _loaded = false;
        _surface?.CancelDrag();
        foreach (var window in _floating)
            window.HideHost();
    }

    public LayoutItem GetLayoutItemFromModel(LayoutContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_items.TryGetValue(content, out var existing))
            return existing;
        LayoutItem item = content is LayoutAnchorable ? new LayoutAnchorableItem() : new LayoutDocumentItem();
        _items.Add(content, item);
        item.Attach(content, this);
        ApplyItemStyle(item);
        return item;
    }

    private void ApplyItemStyle(LayoutItem item) => item.ApplyContainerStyle(LayoutItemContainerStyleSelector?.SelectStyle(item.Model, item) ?? LayoutItemContainerStyle);
    internal DataTemplate? ContentTemplate(LayoutContent model, DependencyObject container) => LayoutItemTemplateSelector?.SelectTemplate(model.Content, container) ?? LayoutItemTemplate;
    internal DataTemplate? HeaderTemplate(LayoutContent model, DependencyObject container, bool title = false)
    {
        if (model is LayoutAnchorable)
            return title ? AnchorableTitleTemplateSelector?.SelectTemplate(model, container) ?? AnchorableTitleTemplate : AnchorableHeaderTemplateSelector?.SelectTemplate(model, container) ?? AnchorableHeaderTemplate;
        return title ? DocumentTitleTemplateSelector?.SelectTemplate(model, container) ?? DocumentTitleTemplate : DocumentHeaderTemplateSelector?.SelectTemplate(model, container) ?? DocumentHeaderTemplate;
    }

    internal bool RaiseDocumentClosing(LayoutDocument document)
    {
        var args = new DocumentClosingEventArgs(document);
        DocumentClosing?.Invoke(this, args);
        return args.Cancel;
    }

    internal void RaiseDocumentClosed(LayoutDocument document) => DocumentClosed?.Invoke(this, new(document));
    protected internal virtual void RaisePreviewDockEvent(LayoutContent layoutContent) => RaiseDockEvent(PreviewDockEvent, layoutContent);
    protected internal virtual void RaiseDockedEvent(LayoutContent layoutContent) => RaiseDockEvent(DockedEvent, layoutContent);
    protected virtual void RaisePreviewFloatEvent(LayoutContent layoutContent) => RaiseDockEvent(PreviewFloatEvent, layoutContent);
    protected virtual void RaiseFloatedEvent(LayoutContent layoutContent) => RaiseDockEvent(FloatedEvent, layoutContent);
    internal bool RaiseDockEvent(DockRoutedEvent id, LayoutContent content)
    {
        var args = new DockEventArgs(content);
        if (id == PreviewDockEvent)
            PreviewDock?.Invoke(this, args);
        else if (id == DockedEvent)
            Docked?.Invoke(this, args);
        else if (id == PreviewFloatEvent)
            PreviewFloat?.Invoke(this, args);
        else
            Floated?.Invoke(this, args);
        if (_handlers.TryGetValue(id, out var handlers))
            foreach (var handler in handlers.ToArray())
                handler(this, args);
        if ((id == PreviewDockEvent || id == PreviewFloatEvent) && _transitions.ContainsKey(content))
            _transitions[content] &= !args.Cancel;
        return !args.Cancel;
    }

    internal IDisposable? BeginTransition(LayoutContent content, bool floating)
    {
        if (_transitions.ContainsKey(content))
            return null;
        _transitions.Add(content, true);
        var before = content.Parent;
        var beforeWindow = content.FindParent<LayoutFloatingWindow>();
        var root = content.Root;
        try
        {
            if (floating)
                RaisePreviewFloatEvent(content);
            else
                RaisePreviewDockEvent(content);
            if (!_transitions[content] || !ReferenceEquals(root, Layout) || !ReferenceEquals(content.Root, root) || !ReferenceEquals(content.Parent, before) || !DockOperations.CanMove(content) || floating && !content.CanFloat)
            {
                _transitions.Remove(content);
                return null;
            }
        }
        catch
        {
            _transitions.Remove(content);
            throw;
        }

        return new ActionDisposable(() =>
        {
            _transitions.Remove(content);
            if (!ReferenceEquals(before, content.Parent) || !ReferenceEquals(beforeWindow, content.FindParent<LayoutFloatingWindow>()))
            {
                if (floating)
                    RaiseFloatedEvent(content);
                else
                    RaiseDockedEvent(content);
            }
        });
    }

    public void AddHandler(DockRoutedEvent routedEvent, RoutedEventHandler handler, bool handledEventsToo = false)
    {
        if (!_handlers.TryGetValue(routedEvent, out var list))
            _handlers[routedEvent] = list = [];
        list.Add(handler);
    }

    public void RemoveHandler(DockRoutedEvent routedEvent, RoutedEventHandler handler)
    {
        if (_handlers.TryGetValue(routedEvent, out var list))
            list.Remove(handler);
    }

    public LayoutFloatingWindowControl CreateFloatingWindow(LayoutContent contentModel, bool isContentImmutable)
    {
        contentModel.Float();
        var model = contentModel.FindParent<LayoutFloatingWindow>() ?? throw new InvalidOperationException("Content cannot float in its current state.");
        return EnsureFloatingWindow(model, isContentImmutable);
    }

    private LayoutFloatingWindowControl EnsureFloatingWindow(LayoutFloatingWindow model, bool immutable = false)
    {
        var existing = _floating.FirstOrDefault(w => ReferenceEquals(w.Model, model));
        if (existing != null)
            return existing;
        LayoutFloatingWindowControl control = model is LayoutDocumentFloatingWindow d ? new LayoutDocumentFloatingWindowControl(d, immutable) : new LayoutAnchorableFloatingWindowControl((LayoutAnchorableFloatingWindow)model, immutable);
        _floating.Add(control);
        return control;
    }

    private void SyncFloatingWindows()
    {
        foreach (var window in _floating.Where(w => !Layout.FloatingWindows.Contains((LayoutFloatingWindow)w.Model)).ToArray())
        {
            window.CloseHost();
            _floating.Remove(window);
        }

        foreach (var model in Layout.FloatingWindows.Where(f => f.IsValid))
        {
            var control = EnsureFloatingWindow(model);
            control.UpdateView();
            var native = FloatingWindowMode != FloatingWindowMode.InSurface && !OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();
            if (_loaded)
            {
                if (native)
                    control.ShowNative();
                else
                    _surface?.ShowFloating(control);
            }
        }
    }

    protected void SetAutoHideWindow(LayoutAutoHideWindowControl value) => AutoHideWindow = value;
    internal void OpenAutoHide(LayoutAnchorable content, bool activate = true) => _surface?.OpenAutoHide(content, activate);
    /// <summary>Creates the managed flyout; subclasses can extend its focus-retention policy.</summary>
    protected virtual LayoutAutoHideWindowControl CreateAutoHideWindowControl() => new();
    internal LayoutAutoHideWindowControl CreateAutoHideView() => CreateAutoHideWindowControl() ?? throw new InvalidOperationException("Auto-hide factory returned null.");
    internal void CloseAutoHide() => _surface?.CloseAutoHide();
    internal void BeginDrag(LayoutContent content, FrameworkElement source, PointerRoutedEventArgs args) => _surface?.BeginDrag(content, source, args);
    internal DockSurface? Surface => _surface;

    /// <summary>Creates the document pane view. Override to supply custom input/selection hooks.</summary>
    protected virtual LayoutDocumentPaneControl CreateDocumentPaneControl(LayoutDocumentPane model) => new(model);
    /// <summary>Creates the tool pane view. The returned control must own the requested model.</summary>
    protected virtual LayoutAnchorablePaneControl CreateAnchorablePaneControl(LayoutAnchorablePane model) => new(model);
    internal LayoutDocumentPaneControl CreateDocumentPaneView(LayoutDocumentPane model)
    {
        var view = CreateDocumentPaneControl(model);
        if (view == null || !ReferenceEquals(view.Model, model) || VisualTreeHelper.GetParent(view) != null)
            throw new InvalidOperationException("The document pane factory must return an unparented control for the requested model.");
        return view;
    }

    internal LayoutAnchorablePaneControl CreateAnchorablePaneView(LayoutAnchorablePane model)
    {
        var view = CreateAnchorablePaneControl(model);
        if (view == null || !ReferenceEquals(view.Model, model) || VisualTreeHelper.GetParent(view) != null)
            throw new InvalidOperationException("The tool pane factory must return an unparented control for the requested model.");
        return view;
    }

    public virtual NavigatorWindow CreateNavigatorWindow() => new(this);
    protected internal virtual void ShowNavigatorWindow() => _surface?.ShowNavigator(CreateNavigatorWindow());
    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled)
            HandleDockingKey(e);
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
            HandleDockingKey(e);
    }

    private void HandleDockingKey(KeyRoutedEventArgs e)
    {
        var ctrl = InputState.ControlDown;
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            _surface?.CancelDrag();
            CloseAutoHide();
            _surface?.CloseNavigator(false);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Windows.System.VirtualKey.Tab)
        {
            ShowNavigatorWindow();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Windows.System.VirtualKey.F4 && Layout.ActiveContent is { } active)
        {
            var command = GetLayoutItemFromModel(active).CloseCommand;
            if (command?.CanExecute(null) == true)
                command.Execute(null);
            e.Handled = true;
        }
    }

#if WINDOWS
    public void Dispose()
#else
    public new void Dispose()
#endif
    {
        if (_disposed)
            return;
        _disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _documentObserver?.Dispose();
        _anchorableObserver?.Dispose();
        Layout.Updated -= OnLayoutModelUpdated;
        Layout.Manager = null;
        foreach (var window in _floating.ToArray())
            window.CloseHost();
        _floating.Clear();
        _surface?.Dispose();
        foreach (var item in _items.Values)
            item.Dispose();
        _items.Clear();
        if (_host != null)
            _host.Content = null;
        _handlers.Clear();
        _documents.Clear();
        _anchorables.Clear();
        _ownedCoordinates.Dispose(); // A caller-supplied adapter remains caller-owned.
    }

    private sealed record SourceEntry(object Value, LayoutContent Model);
}

using System.Diagnostics;
using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Internal;

internal sealed partial class DockSurface : Grid, IDisposable
{
    private readonly Grid _docked = new();
    private readonly Canvas _floats = new();
    private readonly OverlayWindow _overlay = new();
    private readonly Grid _flyouts = new() { IsHitTestVisible = false };
    private readonly Dictionary<ILayoutElement, FrameworkElement> _views = new(ReferenceEqualityComparer.Instance);
    private readonly DockDragSession _drag = new();
    private readonly DispatcherTimer _autoHideTimer = new();
    private readonly DispatcherTimer _dragScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private long _lastScrollTick;
    private Point _lastDragPoint;
    private FrameworkElement? _dragSource;
    private DockInputControl? _dragInput;
    private LayoutContent? _dragContent;
    private LayoutAutoHideWindowControl? _autoHide;
    private long _autoHideGeneration;
    private NavigatorWindow? _navigator;
    private long _navigatorGeneration;
    private bool _disposed;
    internal DockingManager Manager { get; }
    internal DockSurface(DockingManager manager)
    {
        Manager = manager;
        _dragScrollTimer.Tick += OnDragScroll;
        _docked.RowDefinitions.Add(new() { Height = GridLength.Auto }); _docked.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); _docked.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _docked.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _docked.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _docked.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Children.Add(_docked); Children.Add(_floats); Children.Add(_overlay); Children.Add(_flyouts);
        _autoHideTimer.Tick += (_, _) => ExpireAutoHide();
        SizeChanged += (_, _) =>
        {
            PositionAutoHide();
            foreach (var window in Manager.FloatingWindows.Where(w => w.NativeWindow == null && w.IsMaximized).ToArray()) window.UpdateView();
        };
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        { if (_autoHide == null) return; for (var d = e.OriginalSource as DependencyObject; d != null; d = VisualTreeHelper.GetParent(d)) if (ReferenceEquals(d, _autoHide) || d is LayoutAnchorControl) return; CloseAutoHide(); }), true);
    }
    internal FrameworkElement GetView(ILayoutElement model)
    {
        if (_views.TryGetValue(model, out var existing)) return existing;
        FrameworkElement view = model switch
        {
            LayoutPanel p => new LayoutPanelControl(p), LayoutDocumentPaneGroup p => new LayoutDocumentPaneGroupControl(p),
            LayoutAnchorablePaneGroup p => new LayoutAnchorablePaneGroupControl(p), LayoutDocumentPane p => Manager.CreateDocumentPaneView(p),
            LayoutAnchorablePane p => Manager.CreateAnchorablePaneView(p), LayoutAnchorSide p => new LayoutAnchorSideControl(p),
            _ => throw new ArgumentException("No visual representation for " + model.GetType().Name, nameof(model))
        };
        _views.Add(model, view); return view;
    }
    internal void UpdateView(ILayoutElement model)
    {
        var view = GetView(model);
        if (view is IRefreshableLayoutControl refreshable) refreshable.Update(this);
        if (view is LayoutAnchorSideControl side) side.Update(Manager);
    }
    internal void Update()
    {
        if (_disposed) return;
        _navigator?.UpdateAppearance();
        var root = Manager.Layout;
        var panel = (LayoutPanelControl)GetView(root.RootPanel); UpdateView(root.RootPanel); Manager.LayoutRootPanel = panel;
        var top = (LayoutAnchorSideControl)GetView(root.TopSide); var bottom = (LayoutAnchorSideControl)GetView(root.BottomSide);
        var left = (LayoutAnchorSideControl)GetView(root.LeftSide); var right = (LayoutAnchorSideControl)GetView(root.RightSide);
        top.Update(Manager); bottom.Update(Manager); left.Update(Manager); right.Update(Manager);
        Manager.TopSidePanel = top; Manager.BottomSidePanel = bottom; Manager.LeftSidePanel = left; Manager.RightSidePanel = right;
        Grid.SetRow(panel, 1); Grid.SetColumn(panel, 1);
        Grid.SetRow(top, 0); Grid.SetColumn(top, 1); Grid.SetRow(bottom, 2); Grid.SetColumn(bottom, 1);
        Grid.SetRow(left, 1); Grid.SetColumn(left, 0); Grid.SetRow(right, 1); Grid.SetColumn(right, 2);
        VisualParenting.ReconcilePanel(_docked, [panel, top, bottom, left, right]);
        foreach (var stale in _views.Keys.Where(k => !ReferenceEquals(k.Root, root)).ToArray())
        { if (_views[stale] is LayoutCachePaneControl pane) pane.ReleaseViews(); VisualParenting.Detach(_views[stale]); _views.Remove(stale); }
        foreach (var floating in _floats.Children.OfType<LayoutFloatingWindowControl>().Where(f => !ReferenceEquals(f.Model.Root, root)).ToArray()) _floats.Children.Remove(floating);
        if (_autoHide?.Model is LayoutAnchorable a && (!ReferenceEquals(a.Root, root) || !a.IsAutoHidden)) CloseAutoHide(); else PositionAutoHide();
    }
    internal void ShowFloating(LayoutFloatingWindowControl control)
    {
        if (!_floats.Children.Contains(control)) { VisualParenting.Detach(control); _floats.Children.Add(control); }
        control.Visibility = control.IsMinimized ? Visibility.Collapsed : Visibility.Visible;
        RefreshFloatingOrder();
    }
    internal void RefreshFloatingOrder()
    {
        var z = 0;
        foreach (var window in _floats.Children.OfType<LayoutFloatingWindowControl>().OrderBy(w => w.InteractionOrder))
            Canvas.SetZIndex(window, z++);
    }
    internal void OpenAutoHide(LayoutAnchorable model, bool activate = true)
    {
        if (_disposed || !model.IsEnabled || !model.IsAutoHidden || model.Root?.Manager != Manager) return;
        var generation = ++_autoHideGeneration;
        StopAutoHideTimer();
        var view = _autoHide ?? Manager.CreateAutoHideView();
        if (generation != _autoHideGeneration || _disposed) return;
        _autoHide = view;
        if (!_flyouts.Children.Contains(view)) _flyouts.Children.Add(view);
        if (generation != _autoHideGeneration) return;
        _flyouts.IsHitTestVisible = true; view.Open(model, activate);
        if (generation != _autoHideGeneration) return;
        PositionAutoHide();
        if (generation == _autoHideGeneration && view.Model is LayoutAnchorable) Manager.SetAutoHideHost(view);
    }
    internal Grid AutoHideLayer => _flyouts;
    internal Rect AutoHideClientRect
    {
        get
        {
            var left = Manager.LeftSidePanel is { Visibility: Visibility.Visible } l ? l.ActualWidth : 0;
            var right = Manager.RightSidePanel is { Visibility: Visibility.Visible } r ? r.ActualWidth : 0;
            var top = Manager.TopSidePanel is { Visibility: Visibility.Visible } t ? t.ActualHeight : 0;
            var bottom = Manager.BottomSidePanel is { Visibility: Visibility.Visible } b ? b.ActualHeight : 0;
            return new(left, top, Math.Max(0, ActualWidth - left - right), Math.Max(0, ActualHeight - top - bottom));
        }
    }
    internal void PositionAutoHide()
    {
        if (_autoHide?.Model is not LayoutAnchorable model) return;
        if (!model.IsEnabled || !model.IsAutoHidden || !ReferenceEquals(model.Root, Manager.Layout)) { CloseAutoHide(); return; }
        _autoHide.SetViewport(AutoHideClientRect, new(ActualWidth, ActualHeight));
    }
    internal void CloseAutoHide()
    {
        var generation = ++_autoHideGeneration; var view = _autoHide;
        StopAutoHideTimer(); if (view == null) return;
        try { view.CloseView(); }
        finally
        {
            // Even a throwing observer must leave an empty flyout detached. A
            // reentrant open, however, owns both the control and its hit-testing.
            if (generation == _autoHideGeneration && view.Model == null)
            {
                _flyouts.Children.Remove(view); _flyouts.IsHitTestVisible = _navigator != null;
                if (ReferenceEquals(Manager.AutoHideWindow, view)) Manager.SetAutoHideHost(null);
            }
        }
    }
    internal void StopAutoHideTimer() => _autoHideTimer.Stop();
    internal void StartAutoHideTimer()
    {
        _autoHideTimer.Stop();
        if (_disposed || _autoHide?.Model is not LayoutAnchorable) return;
        _autoHideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, Manager.AutoHideWindowClosingTimer));
        _autoHideTimer.Start();
    }
    internal void ExpireAutoHide()
    {
        _autoHideTimer.Stop();
        if (_autoHide == null || _autoHide.RetainOpen) return;
        CloseAutoHide();
    }
    internal void ShowNavigator(NavigatorWindow navigator)
    {
        if (_navigator != null) { _navigator.Advance(InputState.ShiftDown ? -1 : 1); return; }
        var generation = ++_navigatorGeneration;
        // A shortcut can originate in a separate native floating XamlRoot.
        Microsoft.Windows.Shell.WindowRegistry.Find(Manager)?.Activate();
        _navigator = navigator; _flyouts.Children.Add(navigator); _flyouts.IsHitTestVisible = true; navigator.Initialize();
        navigator.HorizontalAlignment = HorizontalAlignment.Center; navigator.VerticalAlignment = VerticalAlignment.Center;
        if (!navigator.Focus(FocusState.Programmatic)) DispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed && ReferenceEquals(_navigator, navigator) && generation == _navigatorGeneration)
                navigator.Focus(FocusState.Programmatic);
        });
    }
    internal void CloseNavigator(bool commit)
    {
        if (_navigator == null) return;
        var navigator = _navigator; _navigator = null; var generation = ++_navigatorGeneration;
        navigator.EndSession();
        _flyouts.Children.Remove(navigator); _flyouts.IsHitTestVisible = _autoHide?.Visibility == Visibility.Visible;
        if (commit) navigator.CommitSelection();
        var root = Manager.Layout; var active = root.ActiveContent;
        if (_disposed || active == null || !ReferenceEquals(active.Root, root)) return;
        Manager.Refresh();
        var item = Manager.GetLayoutItemFromModel(active);
        if (active is LayoutAnchorable { IsAutoHidden: true } tool) OpenAutoHide(tool);
        var floating = Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, active.FindParent<LayoutFloatingWindow>()));
        floating?.Activate();
        if (item.RestoreEditorFocus()) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed && _navigator == null && _navigatorGeneration == generation &&
                ReferenceEquals(Manager.Layout, root) && ReferenceEquals(root.ActiveContent, active))
                item.RestoreEditorFocus();
        });
    }
    internal void BeginDrag(LayoutContent content, FrameworkElement source, PointerRoutedEventArgs args)
    {
        if (!DockOperations.CanMove(content) || content.FindParent<LayoutFloatingWindow>() is { } f && Manager.FloatingWindows.Any(w => ReferenceEquals(w.Model, f) && w.IsContentImmutable)) return;
        // Cross-XamlRoot drags require an explicit platform coordinate adapter rather than guessed window-frame offsets.
        if (!ReferenceEquals(source.XamlRoot, XamlRoot) && Manager.CrossWindowCoordinates == null) return;
        CancelDrag();
        _dragInput = source as DockInputControl;
        source = _dragInput?.DockCaptureElement ?? source;
        _dragContent = content; _dragSource = source;
        if (!TryGetPoint(args, out var point)) { CancelDrag(); return; }
        _drag.Arm(args.Pointer.PointerId, new(point.X, point.Y), content is LayoutDocument);
        if (_dragInput is { } input)
        { input.DockDragMoved += OnDragMoved; input.DockDragReleased += OnDragReleased; input.DockInputCancelled += OnDockInputCancelled; }
        else
        {
            source.AddHandler(PointerMovedEvent, new PointerEventHandler(OnDragMoved), true);
            source.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnDragReleased), true);
        }
        source.PointerCanceled += OnDragCancelled; source.PointerCaptureLost += OnCaptureLost; source.Unloaded += OnDragSourceUnloaded;
        if (!source.CapturePointer(args.Pointer)) CancelDrag();
    }
    private bool TryGetPoint(PointerRoutedEventArgs args, out Point point)
    {
        point = default;
        if (_dragSource == null) return false;
        try
        {
            point = DockCoordinates.Translate(_dragSource, args.GetCurrentPoint(_dragSource).Position, this, Manager.CrossWindowCoordinates);
            return true;
        }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e)) { return false; }
    }
    private void OnDockInputCancelled(object? sender, uint id) { if (ReferenceEquals(sender, _dragInput) && _drag.OwnsPointer(id)) CancelDrag(); }
    private void OnDragSourceUnloaded(object sender, RoutedEventArgs e) => CancelDrag();
    internal IReadOnlyList<IDropArea> GetDropAreas()
    {
        var result = new List<IDropArea>();
        foreach (var (model, view) in _views)
        {
            if (!ReferenceEquals(model.Root, Manager.Layout) || !Visible(view)) continue;
            var type = model switch
            {
                LayoutDocumentPane => DropAreaType.DocumentPane,
                LayoutAnchorablePane => DropAreaType.AnchorablePane,
                LayoutDocumentPaneGroup { ChildrenCount: 0 } => DropAreaType.DocumentPaneGroup,
                _ => (DropAreaType?)null
            };
            if (type != null) result.Add(new DropArea<FrameworkElement>(view, type.Value, this));
        }
        result.Add(new DropArea<DockingManager>(Manager, DropAreaType.DockingManager, this));
        return result;
    }
    private static bool Visible(FrameworkElement view)
    {
        if (view.XamlRoot == null || view.ActualWidth <= 0 || view.ActualHeight <= 0) return false;
        for (DependencyObject? current = view; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed }) return false;
        return true;
    }
    private IModelDropArea? FindDropArea(Point point)
    {
        // Prefer the most specific arranged area. A forbidden pane does not fall
        // through to a different root operation hidden underneath its preview.
        LayoutFloatingWindowControl? floating;
        try
        {
            if (Manager.CrossWindowCoordinates is DesktopWindowCoordinates coordinates && coordinates.TryGetTopmostRoot(this, point, out var hitRoot))
            {
                if (hitRoot == null) return null;
                floating = ReferenceEquals(hitRoot, XamlRoot) ? FloatingAt(point, inSurfaceOnly: true) :
                    Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.XamlRoot, hitRoot));
                if (!ReferenceEquals(hitRoot, XamlRoot) && floating == null) return null;
            }
            else floating = FloatingAt(point);
        }
        catch (Exception error) when (DockCoordinates.IsUnavailable(error)) { return null; }
        var area = GetDropAreas().OfType<IModelDropArea>()
            .Where(a => ReferenceEquals(a.Model?.FindParent<LayoutFloatingWindow>(), floating?.Model))
            .Where(a => a.DetectionRect.Width > 0 && a.DetectionRect.Height > 0 && a.DetectionRect.Contains(point))
            .OrderBy(a => a.Type == DropAreaType.DockingManager ? 1 : 0)
            .ThenBy(a => a.DetectionRect.Width * a.DetectionRect.Height).FirstOrDefault();
        return area;
    }
    internal DockDropPlan? GetDropPlan(LayoutContent content, Point point)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point));
        return ResolveDrop(content, point, out _);
    }
    private DockDropPlan? LegacyDropPlan(LayoutContent content, Point point, IModelDropArea area, bool headerOnly)
    {
        if (area.Model is not ILayoutGroup target) return null;
        var bounds = area.DetectionRect;
        var pane = area.Type != DropAreaType.DockingManager ? GetView(target) as LayoutCachePaneControl : null;
        if (headerOnly && pane?.IsOverHeader(point, this) != true) return null;
        // A tab strip is an insertion surface, not the pane's top split zone.
        var position = pane?.IsOverHeader(point, this) == true ? DockPosition.Inside :
            DockSplitSolver.HitTest(new(bounds.X, bounds.Y, bounds.Width, bounds.Height), new(point.X, point.Y));
        var offset = position switch { DockPosition.Left => 0, DockPosition.Top => 1, DockPosition.Right => 2, DockPosition.Bottom => 3, _ => 4 };
        DropTargetType type;
        switch (area.Type)
        {
            case DropAreaType.DockingManager:
                if (offset == 4) return null;
                type = (DropTargetType)offset; break;
            case DropAreaType.DocumentPane: type = (DropTargetType)(4 + offset); break;
            case DropAreaType.AnchorablePane: type = (DropTargetType)(10 + offset); break;
            case DropAreaType.DocumentPaneGroup: type = DropTargetType.DocumentPaneGroupDockInside; break;
            default: return null;
        }
        int index;
        try { index = position == DockPosition.Inside && pane != null && pane.IsOverHeader(point, this) ? pane.InsertionIndex(point, this) : -1; }
        catch (Exception e) when (DockCoordinates.IsUnavailable(e)) { return null; }
        return DockDropPlan.Create(content, target, type, bounds, index);
    }
    private void OnDragMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_dragContent == null || !_drag.OwnsPointer(args.Pointer.PointerId)) return;
        if (!TryGetPoint(args, out var point)) { CancelDrag(); return; }
        if (!_drag.Move(args.Pointer.PointerId, new(point.X, point.Y), [])) return;
        _lastDragPoint = point;
        UpdateDragAdorners(point);
        if (!_dragScrollTimer.IsEnabled) { _lastScrollTick = Stopwatch.GetTimestamp(); _dragScrollTimer.Start(); }
        args.Handled = true;
    }
    private void OnDragReleased(object sender, PointerRoutedEventArgs args)
    {
        var content = _dragContent;
        if (content == null || !_drag.OwnsPointer(args.Pointer.PointerId)) return;
        if (!TryGetPoint(args, out var point)) { CancelDrag(); return; }
        // A final release can arrive after arrange, source changes, or without a
        // matching move event. Never execute the last painted hover snapshot.
        _drag.Move(args.Pointer.PointerId, new(point.X, point.Y), []);
        var plan = GetDropPlan(content, point);
        var overDockingClient = FindDropArea(point) != null;
        var committed = _drag.Commit(args.Pointer.PointerId);
        DetachDrag();
        if (!committed) return;
        if (plan != null) plan.Execute();
        else if (!overDockingClient && !new DockRect(0, 0, ActualWidth, ActualHeight).Contains(new(point.X, point.Y)) && content.CanFloat)
        { content.FloatingLeft = point.X; content.FloatingTop = point.Y; content.Float(); }
        args.Handled = true;
    }
    private void OnDragCancelled(object sender, PointerRoutedEventArgs args)
    { if (_drag.OwnsPointer(args.Pointer.PointerId)) CancelDrag(); }
    private void OnCaptureLost(object sender, PointerRoutedEventArgs args)
    { if (_dragSource != null && ReferenceEquals(args.OriginalSource, _dragSource) && _drag.OwnsPointer(args.Pointer.PointerId)) CancelDrag(); }
    internal void CancelDrag() { _drag.Cancel(); DetachDrag(); }
    private LayoutFloatingWindowControl? FloatingAt(Point point, bool inSurfaceOnly = false)
    {
        foreach (var window in Manager.FloatingWindows.OrderByDescending(w => w.InteractionOrder))
        {
            if (inSurfaceOnly && window.NativeWindow != null || !Visible(window) || window.NativeWindow is { } native && !native.AppWindow.IsVisible) continue;
            try
            {
                var local = DockCoordinates.Translate(this, point, window, Manager.CrossWindowCoordinates);
                if (new Rect(0, 0, window.ActualWidth, window.ActualHeight).Contains(local)) return window;
            }
            catch (Exception e) when (DockCoordinates.IsUnavailable(e)) { }
        }
        return null;
    }
    private void ShowDragPreview(DockDropPlan? plan)
    {
        _overlay.Hide(); foreach (var window in Manager.FloatingWindows) window.HideDropPreview();
        if (plan == null) return;
        var accent = DockVisuals.Brush(Manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");
        var floating = plan.Target.FindParent<LayoutFloatingWindow>();
        var host = floating == null ? null : Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, floating));
        if (host?.NativeWindow == null) _overlay.ShowPreview(plan, accent);
        else host.ShowDropPreview(plan, this, accent);
    }
    private void OnDragScroll(object? sender, object e)
    {
        if (_disposed || _dragContent == null || _drag.State != DockDragState.Dragging) { _dragScrollTimer.Stop(); return; }
        var now = Stopwatch.GetTimestamp(); var seconds = Stopwatch.GetElapsedTime(_lastScrollTick, now).TotalSeconds; _lastScrollTick = now;
        var plan = UpdateDragAdorners(_lastDragPoint);
        if (plan?.CanExecute != true) return;
        if (GetView(plan.Target) is not LayoutCachePaneControl pane) return;
        try
        {
            if (pane.ScrollHeaderAt(_lastDragPoint, this, seconds))
            {
                pane.UpdateLayout(); // Insertion indices must use the newly scrolled geometry.
                if (_dragContent != null) UpdateDragAdorners(_lastDragPoint);
            }
        }
        catch (Exception error) when (DockCoordinates.IsUnavailable(error)) { CancelDrag(); }
    }
    private void DetachDrag()
    {
        _dragScrollTimer.Stop();
        var source = _dragSource; var input = _dragInput;
        _dragSource = null; _dragInput = null; _dragContent = null;
        if (source != null)
        {
            if (input != null)
            { input.DockDragMoved -= OnDragMoved; input.DockDragReleased -= OnDragReleased; input.DockInputCancelled -= OnDockInputCancelled; }
            else { source.RemoveHandler(PointerMovedEvent, new PointerEventHandler(OnDragMoved)); source.RemoveHandler(PointerReleasedEvent, new PointerEventHandler(OnDragReleased)); }
            source.PointerCanceled -= OnDragCancelled; source.PointerCaptureLost -= OnCaptureLost; source.Unloaded -= OnDragSourceUnloaded; source.ReleasePointerCaptures();
        }
        ShowDragPreview(null);
    }
    internal void Reset()
    {
        CancelDrag(); CloseAutoHide(); CloseNavigator(false);
        foreach (var view in _views.Values) if (view is LayoutCachePaneControl pane) pane.ReleaseViews();
        _views.Clear(); _docked.Children.Clear(); _floats.Children.Clear();
    }
    #if WINDOWS
    public void Dispose()
    #else
    public new void Dispose()
    #endif
    { if (_disposed) return; _disposed = true; Reset(); _autoHideTimer.Stop(); _dragScrollTimer.Tick -= OnDragScroll; }
}

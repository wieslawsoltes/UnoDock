using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Internal;

internal sealed class DockSurface : Grid, IDisposable
{
    private readonly Grid _docked = new();
    private readonly Canvas _floats = new();
    private readonly OverlayWindow _overlay = new();
    private readonly Grid _flyouts = new() { IsHitTestVisible = false };
    private readonly Dictionary<ILayoutElement, FrameworkElement> _views = new(ReferenceEqualityComparer.Instance);
    private readonly DockDragSession _drag = new();
    private readonly DispatcherTimer _autoHideTimer = new();
    private FrameworkElement? _dragSource;
    private LayoutContent? _dragContent;
    private LayoutAutoHideWindowControl? _autoHide;
    private NavigatorWindow? _navigator;
    private bool _disposed;
    internal DockingManager Manager { get; }
    internal DockSurface(DockingManager manager)
    {
        Manager = manager;
        _docked.RowDefinitions.Add(new() { Height = GridLength.Auto }); _docked.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); _docked.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _docked.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _docked.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _docked.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Children.Add(_docked); Children.Add(_floats); Children.Add(_overlay); Children.Add(_flyouts);
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            if (_autoHide?.XamlRoot != null && FocusManager.GetFocusedElement(_autoHide.XamlRoot) is DependencyObject focused)
                for (var current = focused; current != null; current = VisualTreeHelper.GetParent(current)) if (ReferenceEquals(current, _autoHide)) return;
            CloseAutoHide();
        };
        SizeChanged += (_, _) => PositionAutoHide();
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        { if (_autoHide == null) return; for (var d = e.OriginalSource as DependencyObject; d != null; d = VisualTreeHelper.GetParent(d)) if (ReferenceEquals(d, _autoHide) || d is LayoutAnchorControl) return; CloseAutoHide(); }), true);
    }
    internal FrameworkElement GetView(ILayoutElement model)
    {
        if (_views.TryGetValue(model, out var existing)) return existing;
        FrameworkElement view = model switch
        {
            LayoutPanel p => new LayoutPanelControl(p), LayoutDocumentPaneGroup p => new LayoutDocumentPaneGroupControl(p),
            LayoutAnchorablePaneGroup p => new LayoutAnchorablePaneGroupControl(p), LayoutDocumentPane p => new LayoutDocumentPaneControl(p),
            LayoutAnchorablePane p => new LayoutAnchorablePaneControl(p), LayoutAnchorSide p => new LayoutAnchorSideControl(p),
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
        control.Visibility = Visibility.Visible;
    }
    internal void OpenAutoHide(LayoutAnchorable model)
    {
        if (!model.IsAutoHidden || model.Root?.Manager != Manager) return;
        StopAutoHideTimer(); _autoHide ??= new();
        if (!_flyouts.Children.Contains(_autoHide)) _flyouts.Children.Add(_autoHide);
        _flyouts.IsHitTestVisible = true; Manager.SetAutoHideHost(_autoHide); _autoHide.Open(model); PositionAutoHide();
    }
    internal void PositionAutoHide()
    {
        if (_autoHide?.Model is not LayoutAnchorable model) return;
        var side = model.GetSide(); var horizontal = side is AnchorSide.Left or AnchorSide.Right;
        _autoHide.Width = horizontal ? Math.Min(Math.Max(model.AutoHideWidth > 0 ? model.AutoHideWidth : 300, model.AutoHideMinWidth), Math.Max(0, ActualWidth - 40)) : double.NaN;
        _autoHide.Height = horizontal ? double.NaN : Math.Min(Math.Max(model.AutoHideHeight > 0 ? model.AutoHideHeight : 240, model.AutoHideMinHeight), Math.Max(0, ActualHeight - 40));
        _autoHide.HorizontalAlignment = side == AnchorSide.Left ? HorizontalAlignment.Left : side == AnchorSide.Right ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _autoHide.VerticalAlignment = side == AnchorSide.Top ? VerticalAlignment.Top : side == AnchorSide.Bottom ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
        _autoHide.Margin = new Thickness(34, 32, 34, 32);
    }
    internal void CloseAutoHide()
    {
        StopAutoHideTimer(); if (_autoHide == null) return;
        _autoHide.CloseView(); _flyouts.Children.Remove(_autoHide); _flyouts.IsHitTestVisible = _navigator != null;
    }
    internal void StopAutoHideTimer() => _autoHideTimer.Stop();
    internal void StartAutoHideTimer()
    { _autoHideTimer.Stop(); _autoHideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, Manager.AutoHideWindowClosingTimer)); _autoHideTimer.Start(); }
    internal void ShowNavigator(NavigatorWindow navigator)
    {
        if (_navigator != null) { _navigator.Advance(InputState.ShiftDown ? -1 : 1); return; }
        _navigator = navigator; _flyouts.Children.Add(navigator); _flyouts.IsHitTestVisible = true; navigator.Initialize();
        navigator.HorizontalAlignment = HorizontalAlignment.Center; navigator.VerticalAlignment = VerticalAlignment.Center; navigator.Focus(FocusState.Programmatic);
    }
    internal void CloseNavigator(bool commit)
    {
        if (_navigator == null) return; var navigator = _navigator; _navigator = null;
        if (commit) navigator.CommitSelection(); _flyouts.Children.Remove(navigator); _flyouts.IsHitTestVisible = _autoHide?.Visibility == Visibility.Visible;
    }
    internal void BeginDrag(LayoutContent content, FrameworkElement source, PointerRoutedEventArgs args)
    {
        if (!DockOperations.CanMove(content) || content.FindParent<LayoutFloatingWindow>() is { } f && Manager.FloatingWindows.Any(w => ReferenceEquals(w.Model, f) && w.IsContentImmutable)) return;
        // Cross-XamlRoot drags require an explicit platform coordinate adapter rather than guessed window-frame offsets.
        if (!ReferenceEquals(source.XamlRoot, XamlRoot) && Manager.CrossWindowCoordinates == null) return;
        CancelDrag();
        _dragContent = content; _dragSource = source;
        if (!TryGetPoint(args, out var point)) { CancelDrag(); return; }
        _drag.Arm(args.Pointer.PointerId, new(point.X, point.Y), content is LayoutDocument);
        source.AddHandler(PointerMovedEvent, new PointerEventHandler(OnDragMoved), true);
        source.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnDragReleased), true);
        source.PointerCanceled += OnDragCancelled; source.PointerCaptureLost += OnCaptureLost; source.Unloaded += OnDragSourceUnloaded;
        if (!source.CapturePointer(args.Pointer)) CancelDrag();
    }
    private Point GetPoint(PointerRoutedEventArgs args)
    {
        if (_dragSource == null || ReferenceEquals(_dragSource.XamlRoot, XamlRoot)) return args.GetCurrentPoint(this).Position;
        return Manager.CrossWindowCoordinates!.Translate(_dragSource, args.GetCurrentPoint(_dragSource).Position, this);
    }
    private bool TryGetPoint(PointerRoutedEventArgs args, out Point point)
    {
        try { point = GetPoint(args); return true; }
        catch (InvalidOperationException) { point = default; return false; }
        catch (PlatformNotSupportedException) { point = default; return false; }
    }
    private void OnDragSourceUnloaded(object sender, RoutedEventArgs args) => CancelDrag();
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
    internal DockDropPlan? GetDropPlan(LayoutContent content, Point point)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point));
        // Prefer the most specific arranged area. A forbidden pane does not fall
        // through to a different root operation hidden underneath its preview.
        var area = GetDropAreas().OfType<IModelDropArea>()
            .Where(a => a.DetectionRect.Width > 0 && a.DetectionRect.Height > 0 && a.DetectionRect.Contains(point))
            .OrderBy(a => a.Type == DropAreaType.DockingManager ? 1 : 0)
            .ThenBy(a => a.DetectionRect.Width * a.DetectionRect.Height).FirstOrDefault();
        if (area?.Model is not ILayoutGroup target) return null;
        var bounds = area.DetectionRect;
        var position = DockSplitSolver.HitTest(new(bounds.X, bounds.Y, bounds.Width, bounds.Height), new(point.X, point.Y));
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
        var index = position == DockPosition.Inside && GetView(target) is LayoutCachePaneControl pane ? pane.InsertionIndex(point, this) : -1;
        return DockDropPlan.Create(content, target, type, bounds, index);
    }
    private void OnDragMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_dragContent == null || !_drag.OwnsPointer(args.Pointer.PointerId)) return;
        if (!TryGetPoint(args, out var point)) { CancelDrag(); return; }
        if (!_drag.Move(args.Pointer.PointerId, new(point.X, point.Y), [])) return;
        try
        {
            var plan = GetDropPlan(_dragContent, point);
            var accent = DockVisuals.Brush(Manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");
            foreach (var window in Manager.FloatingWindows) window.HideDropPreview();
            var floating = plan?.Target.FindParent<LayoutFloatingWindow>();
            var host = floating == null ? null : Manager.FloatingWindows.FirstOrDefault(w => ReferenceEquals(w.Model, floating));
            if (host?.NativeWindow != null && plan != null)
            { _overlay.Hide(); host.ShowDropPreview(plan, this, accent); }
            else _overlay.ShowPreview(plan, accent);
        }
        catch (InvalidOperationException) { CancelDrag(); }
        catch (PlatformNotSupportedException) { CancelDrag(); }
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
        DockDropPlan? plan;
        try { plan = GetDropPlan(content, point); }
        catch (InvalidOperationException) { CancelDrag(); return; }
        catch (PlatformNotSupportedException) { CancelDrag(); return; }
        var committed = _drag.Commit(args.Pointer.PointerId);
        DetachDrag();
        if (!committed) return;
        if (plan != null) plan.Execute();
        else if (!new DockRect(0, 0, ActualWidth, ActualHeight).Contains(new(point.X, point.Y)) && content.CanFloat)
        { content.FloatingLeft = point.X; content.FloatingTop = point.Y; content.Float(); }
        args.Handled = true;
    }
    private void OnDragCancelled(object sender, PointerRoutedEventArgs args)
    { if (_drag.OwnsPointer(args.Pointer.PointerId)) CancelDrag(); }
    private void OnCaptureLost(object sender, PointerRoutedEventArgs args)
    { if (_dragSource != null && _drag.OwnsPointer(args.Pointer.PointerId)) CancelDrag(); }
    internal void CancelDrag() { _drag.Cancel(); DetachDrag(); }
    private void DetachDrag()
    {
        var source = _dragSource; _dragSource = null; _dragContent = null;
        if (source != null)
        {
            source.RemoveHandler(PointerMovedEvent, new PointerEventHandler(OnDragMoved)); source.RemoveHandler(PointerReleasedEvent, new PointerEventHandler(OnDragReleased));
            source.PointerCanceled -= OnDragCancelled; source.PointerCaptureLost -= OnCaptureLost; source.Unloaded -= OnDragSourceUnloaded; source.ReleasePointerCaptures();
        }
        _overlay.Hide();
        foreach (var window in Manager.FloatingWindows) window.HideDropPreview();
    }
    internal void Reset()
    {
        CancelDrag(); CloseAutoHide(); CloseNavigator(false);
        foreach (var view in _views.Values) if (view is LayoutCachePaneControl pane) pane.ReleaseViews();
        _views.Clear(); _docked.Children.Clear(); _floats.Children.Clear();
    }
    public void Dispose() { if (_disposed) return; _disposed = true; Reset(); _autoHideTimer.Stop(); }
}

using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

public enum DropAreaType { DockingManager = 0, DocumentPane = 1, DocumentPaneGroup = 2, AnchorablePane = 3 }
public enum DropTargetType
{
    DockingManagerDockLeft = 0, DockingManagerDockTop = 1, DockingManagerDockRight = 2, DockingManagerDockBottom = 3,
    DocumentPaneDockLeft = 4, DocumentPaneDockTop = 5, DocumentPaneDockRight = 6, DocumentPaneDockBottom = 7,
    DocumentPaneDockInside = 8, DocumentPaneGroupDockInside = 9,
    AnchorablePaneDockLeft = 10, AnchorablePaneDockTop = 11, AnchorablePaneDockRight = 12, AnchorablePaneDockBottom = 13,
    AnchorablePaneDockInside = 14,
    DocumentPaneDockAsAnchorableLeft = 15, DocumentPaneDockAsAnchorableTop = 16,
    DocumentPaneDockAsAnchorableRight = 17, DocumentPaneDockAsAnchorableBottom = 18
}
public enum OverlayWindowDropTargetType
{
    DockingManagerDockLeft = 0, DockingManagerDockTop = 1, DockingManagerDockRight = 2, DockingManagerDockBottom = 3,
    DocumentPaneDockLeft = 4, DocumentPaneDockTop = 5, DocumentPaneDockRight = 6, DocumentPaneDockBottom = 7, DocumentPaneDockInside = 8,
    AnchorablePaneDockLeft = 9, AnchorablePaneDockTop = 10, AnchorablePaneDockRight = 11,
    AnchorablePaneDockBottom = 12, AnchorablePaneDockInside = 13
}
public interface IDropArea
{
    Rect DetectionRect { get; }
    DropAreaType Type { get; }
}
internal interface IModelDropArea : IDropArea
{
    ILayoutElement? Model { get; }
    DockingManager? Manager { get; }
}
/// <summary>A measured area in the coordinate space of the supplied reference visual.
/// Refresh after arrange or DPI changes. Detached or collapsed visuals have empty bounds.</summary>
public class DropArea<T> : IDropArea, IModelDropArea where T : FrameworkElement
{
    private readonly FrameworkElement _relativeTo;
    public DropArea(T areaElement, DropAreaType type) : this(areaElement, type,
        areaElement?.FindVisualTreeRoot() as FrameworkElement ?? areaElement!) { }
    public DropArea(T areaElement, DropAreaType type, FrameworkElement relativeTo)
    {
        ArgumentNullException.ThrowIfNull(areaElement); ArgumentNullException.ThrowIfNull(relativeTo);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        AreaElement = areaElement; Type = type; _relativeTo = relativeTo; Refresh();
    }
    public T AreaElement { get; }
    public Rect DetectionRect { get; private set; }
    public DropAreaType Type { get; }
    ILayoutElement? IModelDropArea.Model => AreaElement is DockingManager manager ? manager.Layout.RootPanel : (AreaElement as ILayoutControl)?.Model;
    DockingManager? IModelDropArea.Manager => AreaElement as DockingManager ?? (AreaElement as ILayoutControl)?.Model?.Root?.Manager;
    public void Refresh()
    {
        DetectionRect = default;
        if (AreaElement.XamlRoot == null || _relativeTo.XamlRoot == null || AreaElement.Visibility != Visibility.Visible || AreaElement.ActualWidth <= 0 || AreaElement.ActualHeight <= 0) return;
        if (ReferenceEquals(AreaElement.XamlRoot, _relativeTo.XamlRoot))
        {
            DetectionRect = AreaElement.TransformToVisual(_relativeTo).TransformBounds(new Rect(0, 0, AreaElement.ActualWidth, AreaElement.ActualHeight));
            return;
        }
        var converter = ((IModelDropArea)this).Manager?.CrossWindowCoordinates;
        if (converter == null) return;
        try
        {
            var a = converter.Translate(AreaElement, new Point(0, 0), _relativeTo);
            var b = converter.Translate(AreaElement, new Point(AreaElement.ActualWidth, 0), _relativeTo);
            var c = converter.Translate(AreaElement, new Point(0, AreaElement.ActualHeight), _relativeTo);
            var d = converter.Translate(AreaElement, new Point(AreaElement.ActualWidth, AreaElement.ActualHeight), _relativeTo);
            var left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            var top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            var right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            var bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            DetectionRect = new Rect(left, top, right - left, bottom - top);
        }
        catch (InvalidOperationException) { /* Native host closed between arrange and query. */ }
        catch (PlatformNotSupportedException) { /* This host needs an application coordinate adapter. */ }
    }
}

/// <summary>Validated intent, not a mutable tree snapshot. Execution rechecks ownership and
/// policy so an overlay cannot commit a stale target following layout replacement.</summary>
public sealed class DockDropPlan
{
    private readonly LayoutRoot _root;
    private readonly DockingManager? _manager;
    private readonly bool _asDocument;
    private readonly bool _atRoot;
    public LayoutContent Content { get; }
    public ILayoutGroup Target { get; }
    public DockPosition Position { get; }
    public DropTargetType Type { get; }
    public int InsertionIndex { get; }
    public Rect PreviewRect { get; }
    private DockDropPlan(LayoutContent content, ILayoutGroup target, DropTargetType type, Rect bounds, int insertionIndex)
    {
        Content = content; Target = target; Type = type; InsertionIndex = insertionIndex;
        _root = (LayoutRoot)content.Root!; _manager = _root.Manager;
        _atRoot = (int)type <= 3;
        _asDocument = type is >= DropTargetType.DocumentPaneDockLeft and <= DropTargetType.DocumentPaneDockInside;
        Position = type switch
        {
            DropTargetType.DockingManagerDockLeft or DropTargetType.DocumentPaneDockLeft or DropTargetType.AnchorablePaneDockLeft or DropTargetType.DocumentPaneDockAsAnchorableLeft => DockPosition.Left,
            DropTargetType.DockingManagerDockRight or DropTargetType.DocumentPaneDockRight or DropTargetType.AnchorablePaneDockRight or DropTargetType.DocumentPaneDockAsAnchorableRight => DockPosition.Right,
            DropTargetType.DockingManagerDockTop or DropTargetType.DocumentPaneDockTop or DropTargetType.AnchorablePaneDockTop or DropTargetType.DocumentPaneDockAsAnchorableTop => DockPosition.Top,
            DropTargetType.DockingManagerDockBottom or DropTargetType.DocumentPaneDockBottom or DropTargetType.AnchorablePaneDockBottom or DropTargetType.DocumentPaneDockAsAnchorableBottom => DockPosition.Bottom,
            _ => DockPosition.Inside
        };
        var preview = DockSplitSolver.Preview(new(bounds.X, bounds.Y, bounds.Width, bounds.Height), Position);
        PreviewRect = new(preview.X, preview.Y, preview.Width, preview.Height);
    }
    public static DockDropPlan? Create(LayoutContent content, ILayoutGroup target, DropTargetType type, Rect bounds, int insertionIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!double.IsFinite(bounds.X + bounds.Y + bounds.Width + bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0) return null;
        if (content.Root is not LayoutRoot root || !ReferenceEquals(target.Root, root)) return null;
        var validType = type switch
        {
            <= DropTargetType.DockingManagerDockBottom => ReferenceEquals(target, root.RootPanel),
            <= DropTargetType.DocumentPaneDockInside => target is LayoutDocumentPane,
            DropTargetType.DocumentPaneGroupDockInside => target is LayoutDocumentPaneGroup,
            <= DropTargetType.AnchorablePaneDockInside => target is LayoutAnchorablePane && content is LayoutAnchorable,
            _ => target is LayoutDocumentPane && content is LayoutAnchorable
        };
        if (!validType) return null;
        var plan = new DockDropPlan(content, target, type, bounds, insertionIndex);
        return plan.CanExecute ? plan : null;
    }
    public bool CanExecute
    {
        get
        {
            if (!ReferenceEquals(_root.Manager, _manager) || _manager != null && !ReferenceEquals(_manager.Layout, _root)) return false;
            if (!ReferenceEquals(Content.Root, _root) || !ReferenceEquals(Target.Root, _root) || !DockOperations.CanMove(Content)) return false;
            if (Content.FindParent<LayoutFloatingWindow>() is { } floating && _root.Manager?.FloatingWindows.Any(w => ReferenceEquals(w.Model, floating) && w.IsContentImmutable) == true) return false;
            if (_atRoot) return ReferenceEquals(Target, _root.RootPanel);
            if (Type == DropTargetType.DocumentPaneGroupDockInside)
                return Target is LayoutDocumentPaneGroup { ChildrenCount: 0 } && (Content is LayoutDocument || Content is LayoutAnchorable { CanDockAsTabbedDocument: true });
            return DockOperations.CanDock(Content, Target, Position, _asDocument);
        }
    }
    public bool Execute()
    {
        if (!CanExecute) return false;
        var previousParent = Content.Parent;
        var previousIndex = (previousParent as ILayoutGroup)?.IndexOfChild(Content) ?? -1;
        if (_atRoot) DockOperations.DockToRoot(Content, Position);
        else if (Type == DropTargetType.DocumentPaneGroupDockInside)
            DockOperations.DockIntoEmptyDocumentGroup(Content, (LayoutDocumentPaneGroup)Target);
        else DockOperations.Dock(Content, Target, Position, InsertionIndex, _asDocument);
        if (!ReferenceEquals(_root.Manager, _manager) || !ReferenceEquals(Content.Root, _root)) return false;
        var changed = !ReferenceEquals(Content.Parent, previousParent) || previousIndex != ((Content.Parent as ILayoutGroup)?.IndexOfChild(Content) ?? -1);
        return changed && (Position != DockPosition.Inside || ReferenceEquals(Content.Parent, Target) || ReferenceEquals(Content.Parent?.Parent, Target));
    }
}

public abstract class OverlayArea
{
    public Rect ScreenDetectionArea { get; private set; }
    protected void SetScreenDetectionArea(Rect rect)
    {
        if (!double.IsFinite(rect.X + rect.Y + rect.Width + rect.Height) || rect.Width < 0 || rect.Height < 0) throw new ArgumentOutOfRangeException(nameof(rect));
        ScreenDetectionArea = rect;
    }
    public bool HitTest(Point point) => ScreenDetectionArea.Width > 0 && ScreenDetectionArea.Height > 0 && ScreenDetectionArea.Contains(point);
}
public class DockingManagerOverlayArea : OverlayArea
{
    public DockingManagerOverlayArea(Rect bounds) => SetScreenDetectionArea(bounds);
}
public class DocumentPaneControlOverlayArea : OverlayArea
{
    public DocumentPaneControlOverlayArea(Rect bounds) => SetScreenDetectionArea(bounds);
}
public class AnchorablePaneControlOverlayArea : OverlayArea
{
    public AnchorablePaneControlOverlayArea(Rect bounds) => SetScreenDetectionArea(bounds);
}
public class OverlayWindowDropTarget
{
    public OverlayWindowDropTarget(DockDropPlan plan) => Plan = plan ?? throw new ArgumentNullException(nameof(plan));
    public DockDropPlan Plan { get; }
    public Rect DetectionRect => Plan.PreviewRect;
    public DropTargetType Type => Plan.Type;
    public bool Drop() => Plan.Execute();
}
/// <summary>Non-activating, hit-test-transparent overlay hosted in the application's visual tree.
/// No WPF Window identity or HWND ownership is implied by this portable implementation.</summary>
public class OverlayWindow : ContentControl
{
    private readonly Canvas _canvas = new();
    private readonly Border _preview = new() { BorderThickness = new(2), Opacity = .35 };
    public DockDropPlan? CurrentPlan { get; private set; }
    public bool IsOpen => Visibility == Visibility.Visible && CurrentPlan != null;
    public OverlayWindow()
    {
        IsHitTestVisible = false; IsTabStop = false; Visibility = Visibility.Collapsed;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _canvas.Children.Add(_preview); Content = _canvas;
    }
    public void ShowPreview(DockDropPlan? plan, Brush accent) => ShowPreview(plan, accent, plan?.PreviewRect ?? default);
    internal void ShowPreview(DockDropPlan? plan, Brush accent, Rect rect)
    {
        ArgumentNullException.ThrowIfNull(accent);
        if (plan?.CanExecute != true) { Hide(); return; }
        CurrentPlan = plan;
        Canvas.SetLeft(_preview, rect.X); Canvas.SetTop(_preview, rect.Y);
        _preview.Width = rect.Width; _preview.Height = rect.Height; _preview.Background = accent; _preview.BorderBrush = accent;
        Visibility = Visibility.Visible;
    }
    public void Hide() { CurrentPlan = null; Visibility = Visibility.Collapsed; }
    public void Close() { var args = new CancelEventArgs(); OnClosing(args); if (!args.Cancel) Hide(); }
    protected virtual void OnClosing(CancelEventArgs e) { }
}

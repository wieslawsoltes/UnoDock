using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

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
            <= DropTargetType.AnchorablePaneDockBottom => target is LayoutAnchorablePane && content is LayoutAnchorable,
            DropTargetType.AnchorablePaneDockInside => target is LayoutAnchorablePane && content is LayoutAnchorable,
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

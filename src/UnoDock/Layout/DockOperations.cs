namespace Xceed.Wpf.AvalonDock.Layout;

/// <summary>All visual and programmatic docking goes through these ownership-preserving operations.</summary>
public static class DockOperations
{
    public static bool CanMove(LayoutContent content) => content.IsEnabled && content is not LayoutDocument { CanMove: false } && content.Parent is not ILayoutPositionableElement { CanRepositionItems: false };
    public static void Float(LayoutContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanFloat || !CanMove(content) || content.IsFloating || content.Root is not LayoutRoot root) return;
        var manager = root.Manager;
        using var transition = manager?.BeginTransition(content, true); if (manager != null && transition == null) return;
        using var batch = root.BeginUpdate();
        content.RememberDockPosition();
        LayoutFloatingWindow floating;
        if (content is LayoutDocument doc) floating = new LayoutDocumentFloatingWindow { RootDocument = doc };
        else if (content is LayoutAnchorable tool) floating = new LayoutAnchorableFloatingWindow { RootPanel = new(new LayoutAnchorablePane(tool)) };
        else throw new ArgumentException("Unknown layout content type.", nameof(content));
        root.FloatingWindows.Add(floating); content.IsActive = true; root.CollectGarbage();
    }
    public static void Restore(LayoutContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!CanMove(content) || content.Root is not LayoutRoot root) return;
        var manager = root.Manager;
        using var transition = manager?.BeginTransition(content, false); if (manager != null && transition == null) return;
        using var batch = root.BeginUpdate();
        var previous = content.PreviousContainer as ILayoutGroup;
        if (previous != null && ReferenceEquals(previous.Root, root) && CanContain(previous, content))
        {
            if (!ReferenceEquals(content.Parent, previous)) previous.InsertChildAt(Math.Clamp(content.PreviousContainerIndex, 0, previous.ChildrenCount), content);
        }
        else if (content is LayoutAnchorable anchorable) AddAnchorable(root, anchorable, AnchorableShowStrategy.Right);
        else InsertDocument(root, content);
        content.IsActive = true; root.CollectGarbage();
    }
    public static void AsDocument(LayoutContent content)
    {
        if (!CanMove(content) || content.Root is not LayoutRoot root || content is LayoutAnchorable { CanDockAsTabbedDocument: false }) return;
        var manager = root.Manager;
        using var transition = manager?.BeginTransition(content, false); if (manager != null && transition == null) return;
        using var batch = root.BeginUpdate();
        if (content.Parent is LayoutDocumentPane) { content.IsActive = true; return; }
        content.RememberDockPosition(); InsertDocument(root, content);
        if (ReferenceEquals(content.Root, root)) content.IsActive = true;
        root.CollectGarbage();
    }
    private static void InsertDocument(LayoutRoot root, LayoutContent content)
    {
        var manager = root.Manager;
        var pane = root.LastFocusedDocument?.Parent as LayoutDocumentPane ?? root.RootPanel.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); root.RootPanel.Children.Add(pane); }
        if (content is LayoutDocument doc)
        {
            var handled = manager?.LayoutUpdateStrategy?.BeforeInsertDocument(root, doc, pane) == true;
            if (manager != null && !ReferenceEquals(manager.Layout, root) || !ReferenceEquals(pane.Root, root)) return;
            if (!handled && !ReferenceEquals(doc.Parent, pane)) pane.Children.Add(doc);
            if (ReferenceEquals(doc.Root, root)) manager?.LayoutUpdateStrategy?.AfterInsertDocument(root, doc);
        }
        else if (AllowsDrop(pane, content)) pane.Children.Add(content);
    }

    public static void AddAnchorable(LayoutRoot root, LayoutAnchorable content, AnchorableShowStrategy strategy)
    {
        using var batch = root.BeginUpdate();
        var side = strategy.HasFlag(AnchorableShowStrategy.Left) ? AnchorSide.Left : strategy.HasFlag(AnchorableShowStrategy.Top) ? AnchorSide.Top : strategy.HasFlag(AnchorableShowStrategy.Bottom) ? AnchorSide.Bottom : AnchorSide.Right;
        var existing = strategy.HasFlag(AnchorableShowStrategy.Most) ? null : root.RootPanel.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(p => p.GetSide() == side);
        if (existing == null)
        {
            existing = new();
            AddAtRoot(root, existing, side);
        }
        var manager = root.Manager;
        var handled = manager?.LayoutUpdateStrategy?.BeforeInsertAnchorable(root, content, existing) == true;
        if (manager != null && !ReferenceEquals(manager.Layout, root) || !ReferenceEquals(existing.Root, root)) return;
        if (!handled && !ReferenceEquals(content.Parent, existing)) existing.Children.Add(content);
        if (ReferenceEquals(content.Root, root)) manager?.LayoutUpdateStrategy?.AfterInsertAnchorable(root, content);
        if (ReferenceEquals(content.Root, root)) content.IsSelected = true;
    }
    public static void ToggleAutoHide(LayoutAnchorable content)
    {
        if (!content.CanAutoHide || content.Root is not LayoutRoot root) return;
        using var batch = root.BeginUpdate();
        if (content.Parent is LayoutAnchorGroup group)
        {
            var destination = group.PreviousContainer as LayoutAnchorablePane;
            if (destination?.Root != root)
            {
                destination = new(); AddAtRoot(root, destination, group.GetSide());
            }
            destination.Children.Add(content);
            if (group.Children.Count == 0) group.Parent?.RemoveChild(group);
            content.IsActive = true;
        }
        else if (content.Parent is LayoutAnchorablePane pane)
        {
            var side = pane.GetSide();
            var anchorGroup = new LayoutAnchorGroup { PreviousContainer = pane, PreviousContainerIndex = (pane.Parent as ILayoutGroup)?.IndexOfChild(pane) ?? 0 };
            root.GetSide(side).Children.Add(anchorGroup);
            content.SetPrevious(pane, pane.Children.IndexOf(content));
            anchorGroup.Children.Add(content);
        }
        root.CollectGarbage();
    }
    public static bool CanContain(ILayoutGroup target, LayoutContent content) => target is LayoutDocumentPane && (content is LayoutDocument || content is LayoutAnchorable { CanDockAsTabbedDocument: true })
        || target is LayoutAnchorablePane or LayoutAnchorGroup && content is LayoutAnchorable;
    /// <summary>Checks shared docking policy without mutating either tree.</summary>
    public static bool CanDock(LayoutContent content, ILayoutGroup target, DockPosition position) => CanDock(content, target, position, content is LayoutDocument);
    public static bool CanDock(LayoutContent content, ILayoutGroup target, DockPosition position, bool asDocument)
    {
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(target);
        if (!CanMove(content) || target.Root is not LayoutRoot root || !ReferenceEquals(content.Root, root)) return false;
        if (content.FindParent<LayoutFloatingWindow>() is { } floating && root.Manager?.FloatingWindows.Any(w => ReferenceEquals(w.Model, floating) && w.IsContentImmutable) == true) return false;
        if (asDocument && content is LayoutAnchorable { CanDockAsTabbedDocument: false }) return false;
        if (position == DockPosition.Inside) return CanContain(target, content) && AllowsDrop(target, content);
        if (position is not (DockPosition.Left or DockPosition.Right or DockPosition.Top or DockPosition.Bottom)) return false;
        if (target is not ILayoutPanelElement || target.Parent is not ILayoutGroup) return false;
        var orientation = position is DockPosition.Left or DockPosition.Right ? Orientation.Horizontal : Orientation.Vertical;
        return !asDocument || root.Manager?.AllowMixedOrientation != false ||
            target.Parent is not LayoutDocumentPaneGroup group || group.ChildrenCount <= 1 || group.Orientation == orientation;
    }
    public static void Dock(LayoutContent content, ILayoutGroup target, DockPosition position, int insertionIndex = -1) => Dock(content, target, position, insertionIndex, content is LayoutDocument);
    public static void Dock(LayoutContent content, ILayoutGroup target, DockPosition position, int insertionIndex, bool asDocument)
    {
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(target);
        if (!CanDock(content, target, position, asDocument) || target.Root is not LayoutRoot root) return;
        if (position == DockPosition.Inside)
        {
            if (!CanContain(target, content)) return;
            var initiatingManager = root.Manager;
            using var transition = initiatingManager?.BeginTransition(content, false); if (initiatingManager != null && transition == null) return;
            if (!CanDock(content, target, position, asDocument)) return;
            using var batch = root.BeginUpdate();
            if (ReferenceEquals(content.Parent, target))
            {
                var from = target.IndexOfChild(content);
                if (insertionIndex >= 0 && target is ILayoutPane pane)
                {
                    var boundary = Math.Clamp(insertionIndex, 0, target.ChildrenCount);
                    var destination = boundary > from ? boundary - 1 : boundary;
                    if (destination != from) pane.MoveChild(from, destination);
                }
            }
            else target.InsertChildAt(insertionIndex < 0 ? target.ChildrenCount : Math.Clamp(insertionIndex, 0, target.ChildrenCount), content);
            content.SetPrevious(null, 0); content.IsActive = true; root.CollectGarbage(); return;
        }
        if (target is not ILayoutPanelElement targetElement || target.Parent is not ILayoutGroup parent) return;
        // Lift mixed-kind splits out of homogeneous groups before changing ownership.
        var documentKind = asDocument || content is LayoutDocument;
        while ((parent is LayoutDocumentPaneGroup && !documentKind) || (parent is LayoutAnchorablePaneGroup && documentKind))
        {
            if (parent is not ILayoutPanelElement outer || parent.Parent is not ILayoutGroup outerParent) return;
            targetElement = outer; parent = outerParent;
        }
        if (parent is not LayoutPanel && parent is not LayoutDocumentPaneGroup && parent is not LayoutAnchorablePaneGroup) return;
        var manager = root.Manager;
        using var edgeTransition = manager?.BeginTransition(content, false); if (manager != null && edgeTransition == null) return;
        if (!CanDock(content, target, position, asDocument) || !ReferenceEquals(targetElement.Parent, parent)) return;
        using (root.BeginUpdate())
        {
            var horizontal = position is DockPosition.Left or DockPosition.Right;
            var orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
            var before = position is DockPosition.Left or DockPosition.Top;
            ILayoutPanelElement newPane;
            if (documentKind) { var documents = new LayoutDocumentPane(); documents.Children.Add(content); newPane = documents; }
            else newPane = new LayoutAnchorablePane((LayoutAnchorable)content);
            if (parent is ILayoutOrientableGroup oriented && oriented.Orientation == orientation && AcceptsPanel(parent, newPane))
                parent.InsertChildAt(parent.IndexOfChild(targetElement) + (before ? 0 : 1), newPane);
            else
            {
                // Select the narrowest compatible group type, preserving the parent's element contract.
                ILayoutGroup wrapper = parent switch
                {
                    LayoutDocumentPaneGroup when newPane is ILayoutDocumentPane && targetElement is ILayoutDocumentPane => new LayoutDocumentPaneGroup { Orientation = orientation },
                    LayoutAnchorablePaneGroup when newPane is ILayoutAnchorablePane && targetElement is ILayoutAnchorablePane => new LayoutAnchorablePaneGroup { Orientation = orientation },
                    LayoutPanel => new LayoutPanel { Orientation = orientation },
                    _ => throw new InvalidOperationException("This split requires a compatible parent group.")
                };
                parent.ReplaceChild(targetElement, wrapper);
                wrapper.InsertChildAt(0, before ? newPane : targetElement);
                wrapper.InsertChildAt(1, before ? targetElement : newPane);
            }
            content.SetPrevious(null, 0); content.IsActive = true; root.CollectGarbage();
        }
    }
    // Public documentation defines duplicate content by the Title/ContentId pair.
    private static bool AllowsDrop(ILayoutGroup target, LayoutContent content) =>
        ReferenceEquals(content.Parent, target) || target is not ILayoutPositionableElement { AllowDuplicateContent: false } ||
        !target.Children.OfType<LayoutContent>().Any(other => !ReferenceEquals(other, content) &&
            string.Equals(other.Title, content.Title, StringComparison.Ordinal) && string.Equals(other.ContentId, content.ContentId, StringComparison.Ordinal));
    public static void DockIntoEmptyDocumentGroup(LayoutContent content, LayoutDocumentPaneGroup group)
    {
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(group);
        if (group.ChildrenCount != 0 || !CanMove(content) || content.Root is not LayoutRoot root ||
            !ReferenceEquals(group.Root, root) || content is LayoutAnchorable { CanDockAsTabbedDocument: false }) return;
        var manager = root.Manager;
        using var transition = manager?.BeginTransition(content, false);
        if (manager != null && transition == null || group.ChildrenCount != 0 || !ReferenceEquals(group.Root, root)) return;
        using var batch = root.BeginUpdate();
        var pane = new LayoutDocumentPane(); group.Children.Add(pane); pane.Children.Add(content);
        content.SetPrevious(null, -1); content.IsActive = true; root.CollectGarbage();
    }
    private static bool AcceptsPanel(ILayoutGroup parent, ILayoutPanelElement item) => parent is LayoutPanel || parent is LayoutDocumentPaneGroup && item is ILayoutDocumentPane || parent is LayoutAnchorablePaneGroup && item is ILayoutAnchorablePane;
    public static void DockToRoot(LayoutContent content, DockPosition position)
    {
        if (!CanMove(content) || content.Root is not LayoutRoot root || position is not (DockPosition.Left or DockPosition.Right or DockPosition.Top or DockPosition.Bottom)) return;
        var manager = root.Manager;
        using var transition = manager?.BeginTransition(content, false); if (manager != null && transition == null) return;
        using var batch = root.BeginUpdate();
        ILayoutPanelElement pane = content is LayoutDocument d ? new LayoutDocumentPane(d) : new LayoutAnchorablePane((LayoutAnchorable)content);
        AddAtRoot(root, pane, position switch { DockPosition.Left => AnchorSide.Left, DockPosition.Top => AnchorSide.Top, DockPosition.Bottom => AnchorSide.Bottom, _ => AnchorSide.Right });
        content.SetPrevious(null, 0); content.IsActive = true; root.CollectGarbage();
    }
    internal static void AddAtRoot(LayoutRoot root, ILayoutPanelElement pane, AnchorSide side)
    {
        var orientation = side is AnchorSide.Left or AnchorSide.Right ? Orientation.Horizontal : Orientation.Vertical;
        var before = side is AnchorSide.Left or AnchorSide.Top;
        var panel = root.RootPanel;
        if (panel.Orientation != orientation && panel.ChildrenCount > 1)
        {
            var wrapper = new LayoutPanel { Orientation = orientation };
            root.RootPanel = wrapper; wrapper.Children.Add(panel); panel = wrapper;
        }
        panel.Orientation = orientation; panel.Children.Insert(before ? 0 : panel.ChildrenCount, pane);
    }
}

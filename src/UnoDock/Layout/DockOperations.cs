namespace Xceed.Wpf.AvalonDock.Layout;

/// <summary>All visual and programmatic docking goes through these ownership-preserving operations.</summary>
public static class DockOperations
{
    public static bool CanMove(LayoutContent content) => content.IsEnabled && content is not LayoutDocument { CanMove: false } && content.Parent is not ILayoutPositionableElement { CanRepositionItems: false };
    public static void Float(LayoutContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanFloat || !CanMove(content) || content.IsFloating || content.Root is not LayoutRoot root) return;
        using var transition = root.Manager?.BeginTransition(content, true); if (root.Manager != null && transition == null) return;
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
        if (content.Root is not LayoutRoot root) return;
        using var transition = root.Manager?.BeginTransition(content, false); if (root.Manager != null && transition == null) return;
        using var batch = root.BeginUpdate();
        var previous = content.PreviousContainer as ILayoutGroup;
        if (previous != null && ReferenceEquals(previous.Root, root) && CanContain(previous, content))
        {
            if (!ReferenceEquals(content.Parent, previous)) previous.InsertChildAt(Math.Clamp(content.PreviousContainerIndex, 0, previous.ChildrenCount), content);
        }
        else if (content is LayoutAnchorable anchorable) AddAnchorable(root, anchorable, AnchorableShowStrategy.Right);
        else AsDocument(content);
        content.IsActive = true; root.CollectGarbage();
    }
    public static void AsDocument(LayoutContent content)
    {
        if (content.Root is not LayoutRoot root || content is LayoutAnchorable { CanDockAsTabbedDocument: false }) return;
        using var transition = root.Manager?.BeginTransition(content, false); if (root.Manager != null && transition == null) return;
        using var batch = root.BeginUpdate();
        if (content.Parent is LayoutDocumentPane) { content.IsActive = true; return; }
        content.RememberDockPosition();
        var pane = root.LastFocusedDocument?.Parent as LayoutDocumentPane ?? root.RootPanel.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane == null) { pane = new(); root.RootPanel.Children.Add(pane); }
        if (content is LayoutDocument doc)
        {
            if (root.Manager?.LayoutUpdateStrategy?.BeforeInsertDocument(root, doc, pane) != true) pane.Children.Add(doc);
            root.Manager?.LayoutUpdateStrategy?.AfterInsertDocument(root, doc);
        }
        else pane.Children.Add(content);
        if (ReferenceEquals(content.Root, root)) content.IsActive = true;
        root.CollectGarbage();
    }
    public static void AddAnchorable(LayoutRoot root, LayoutAnchorable content, AnchorableShowStrategy strategy)
    {
        using var batch = root.BeginUpdate();
        var side = strategy.HasFlag(AnchorableShowStrategy.Left) ? AnchorSide.Left : strategy.HasFlag(AnchorableShowStrategy.Top) ? AnchorSide.Top : strategy.HasFlag(AnchorableShowStrategy.Bottom) ? AnchorSide.Bottom : AnchorSide.Right;
        var existing = root.RootPanel.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(p => p.GetSide() == side);
        if (existing == null)
        {
            existing = new() { DockWidth = new(280), DockHeight = new(220) };
            AddAtRoot(root, existing, side);
        }
        if (root.Manager?.LayoutUpdateStrategy?.BeforeInsertAnchorable(root, content, existing) != true)
            existing.Children.Add(content);
        root.Manager?.LayoutUpdateStrategy?.AfterInsertAnchorable(root, content);
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
            foreach (var tool in group.Children.ToArray()) destination.Children.Add(tool);
            group.Parent?.RemoveChild(group); content.IsActive = true;
        }
        else if (content.Parent is LayoutAnchorablePane pane)
        {
            if (pane.Children.Any(c => !c.CanAutoHide)) return;
            var side = pane.GetSide();
            var anchorGroup = new LayoutAnchorGroup { PreviousContainer = pane, PreviousContainerIndex = (pane.Parent as ILayoutGroup)?.IndexOfChild(pane) ?? 0 };
            root.GetSide(side).Children.Add(anchorGroup);
            var children = pane.Children.ToArray();
            for (var i = 0; i < children.Length; i++) { children[i].SetPrevious(pane, i); anchorGroup.Children.Add(children[i]); }
        }
        root.CollectGarbage();
    }
    public static bool CanContain(ILayoutGroup target, LayoutContent content) => target is LayoutDocumentPane && (content is LayoutDocument || content is LayoutAnchorable { CanDockAsTabbedDocument: true })
        || target is LayoutAnchorablePane or LayoutAnchorGroup && content is LayoutAnchorable;
    public static void Dock(LayoutContent content, ILayoutGroup target, DockPosition position, int insertionIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(target);
        if (!CanMove(content) || target.Root is not LayoutRoot root || !ReferenceEquals(content.Root, root)) return;
        if (position == DockPosition.Inside)
        {
            if (!CanContain(target, content)) return;
            using var transition = root.Manager?.BeginTransition(content, false); if (root.Manager != null && transition == null) return;
            using var batch = root.BeginUpdate();
            if (ReferenceEquals(content.Parent, target))
            {
                var from = target.IndexOfChild(content);
                if (insertionIndex >= 0 && target is ILayoutPane pane) pane.MoveChild(from, Math.Clamp(insertionIndex, 0, target.ChildrenCount - 1));
            }
            else target.InsertChildAt(insertionIndex < 0 ? target.ChildrenCount : Math.Clamp(insertionIndex, 0, target.ChildrenCount), content);
            content.SetPrevious(null, 0); content.IsActive = true; root.CollectGarbage(); return;
        }
        if (target is not ILayoutPanelElement targetElement || target.Parent is not ILayoutGroup parent) return;
        // Lift mixed-kind splits out of homogeneous groups before changing ownership.
        var documentKind = content is LayoutDocument;
        while ((parent is LayoutDocumentPaneGroup && !documentKind) || (parent is LayoutAnchorablePaneGroup && documentKind))
        {
            if (parent is not ILayoutPanelElement outer || parent.Parent is not ILayoutGroup outerParent) return;
            targetElement = outer; parent = outerParent;
        }
        if (parent is not LayoutPanel && parent is not LayoutDocumentPaneGroup && parent is not LayoutAnchorablePaneGroup) return;
        using var edgeTransition = root.Manager?.BeginTransition(content, false); if (root.Manager != null && edgeTransition == null) return;
        using (root.BeginUpdate())
        {
            var horizontal = position is DockPosition.Left or DockPosition.Right;
            var orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
            var before = position is DockPosition.Left or DockPosition.Top;
            ILayoutPanelElement newPane;
            if (content is LayoutDocument doc) newPane = new LayoutDocumentPane(doc);
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
    private static bool AcceptsPanel(ILayoutGroup parent, ILayoutPanelElement item) => parent is LayoutPanel || parent is LayoutDocumentPaneGroup && item is ILayoutDocumentPane || parent is LayoutAnchorablePaneGroup && item is ILayoutAnchorablePane;
    public static void DockToRoot(LayoutContent content, DockPosition position)
    {
        if (!CanMove(content) || content.Root is not LayoutRoot root || position == DockPosition.Inside) return;
        using var transition = root.Manager?.BeginTransition(content, false); if (root.Manager != null && transition == null) return;
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

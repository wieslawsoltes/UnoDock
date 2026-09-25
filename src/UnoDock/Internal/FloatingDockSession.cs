using System.Runtime.ExceptionServices;
using UnoDock.Controls;
using UnoDock.Layout;

namespace UnoDock.Internal;
/// <summary>One-use whole-window docking intent. Edge drops move an existing tool
/// subtree intact. Every incoming tab is preflighted before any library mutation.</summary>
internal sealed class FloatingDockSession : IDisposable
{
    private readonly DockingManager _manager;
    private readonly LayoutRoot _root;
    private readonly ILayoutElement _model;
    private readonly (ILayoutElement Node, ILayoutContainer? Parent)[] _nodes;
    private readonly long _enabledToken, _immutableToken, _managerEnabledToken;
    private bool _invalid, _disposed, _consumed;
    internal LayoutFloatingWindowControl Window { get; }
    internal LayoutContent[] Contents { get; }
    internal LayoutContent Representative { get; }

    internal FloatingDockSession(DockingManager manager, LayoutFloatingWindowControl window)
    {
        _manager = manager;
        _root = manager.Layout;
        Window = window;
        _model = window.Model;
        Contents = window.Contents.ToArray();
        if (Contents.Length == 0)
            throw new ArgumentException("The floating window has no content.", nameof(window));
        Representative = Contents.FirstOrDefault(c => c.IsActive) ?? Contents.FirstOrDefault(c => c.IsSelected) ?? Contents[0];
        _nodes = new[]
        {
            _model
        }.Concat(_model.Descendents()).Select(node => (node, node.Parent)).ToArray();
        foreach (var(node, _)in _nodes)
            node.PropertyChanged += OnNodeChanged;
        _manager.LayoutChanged += OnLayoutChanged;
        _enabledToken = window.RegisterPropertyChangedCallback(Control.IsEnabledProperty, Invalidate);
        _immutableToken = window.RegisterPropertyChangedCallback(LayoutFloatingWindowControl.IsContentImmutableProperty, Invalidate);
        _managerEnabledToken = manager.RegisterPropertyChangedCallback(Control.IsEnabledProperty, Invalidate);
    }

    private void Invalidate(DependencyObject _, DependencyProperty __) => _invalid = true;
    private void OnLayoutChanged(object? sender, EventArgs e) => _invalid = true;
    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Window bounds/title/selection may change during the drag. Ownership and
        // policy changes revoke it even when application code restores the value.
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Parent" or "Root" or "ChildrenCount" or "RootPanel" or "RootDocument" or "CanMove" or "CanFloat" or "CanRepositionItems" or "CanDockAsTabbedDocument" or "IsEnabled" or "IsHidden" or "Orientation")
            _invalid = true;
    }

    internal bool IsCurrent => !_disposed && !_invalid && _manager.IsEnabled && Window.IsEnabled && !Window.IsContentImmutable && ReferenceEquals(_manager.Layout, _root) && ReferenceEquals(_root.Manager, _manager) && ReferenceEquals(Window.Model, _model) && ReferenceEquals(_model.Root, _root) && _manager.FloatingWindows.Any(w => ReferenceEquals(w, Window)) && _nodes.All(pair => ReferenceEquals(pair.Node.Parent, pair.Parent)) && Contents.All(c => DockOperations.CanMove(c) && ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), _model));

    internal bool CanAccept(DockDropPlan? plan)
    {
        if (plan == null || !IsCurrent || !ReferenceEquals(plan.Content, Representative) || !ReferenceEquals(plan.Target.Root, _root) || ReferenceEquals(plan.Target.FindParent<LayoutFloatingWindow>(), _model))
            return false;
        foreach (var content in Contents)
            if (DockDropPlan.Create(content, plan.Target, plan.Type, new Rect(0, 0, 1, 1), plan.InsertionIndex) == null)
                return false;
        if (plan.Position == DockPosition.Inside && plan.Target is ILayoutPositionableElement { AllowDuplicateContent: false } && Contents.Select(c => (c.Title, c.ContentId)).Distinct().Count() != Contents.Length)
            return false;
        return IsCurrent;
    }

    internal bool Execute(DockDropPlan? plan)
    {
        if (_consumed || !CanAccept(plan))
            return false;
        _consumed = true;
        var target = plan!.Target;
        var targetNodes = Ancestors(target).ToArray();
        var targetChanged = false;
        void Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Parent" or "Root" or "ChildrenCount" or "Orientation" or "AllowDuplicateContent" or "CanRepositionItems")
                targetChanged = true;
        }

        foreach (var node in targetNodes)
            node.PropertyChanged += Changed;
        var leases = new List<IDisposable>(Contents.Length);
        Exception? failure = null;
        var result = false;
        try
        {
            result = Apply();
        }
        catch (Exception e)
        {
            failure = e;
        }
        finally
        {
            foreach (var node in targetNodes)
                node.PropertyChanged -= Changed;
            foreach (var lease in leases)
                try
                {
                    lease.Dispose();
                }
                catch (Exception e)
                {
                    failure = failure == null ? e : new AggregateException("Docking and completion observers failed.", failure, e);
                }
        }

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
        bool Apply()
        {
            foreach (var content in Contents)
            {
                if (targetChanged || !CanAccept(plan))
                    return false;
                var lease = _manager.BeginTransition(content, false);
                if (lease == null)
                    return false;
                leases.Add(lease);
                // PreviewDock is application code. Recheck the WHOLE source.
                if (targetChanged || !CanAccept(plan))
                    return false;
            }

            foreach (var node in targetNodes)
                node.PropertyChanged -= Changed;
            Dispose();
            using var update = _root.BeginUpdate();
            if (plan.Position == DockPosition.Inside)
            {
                ILayoutGroup destination = target;
                if (target is LayoutDocumentPaneGroup group)
                {
                    if (group.ChildrenCount != 0)
                        return false;
                    var pane = new LayoutDocumentPane();
                    group.Children.Add(pane);
                    destination = pane;
                }

                var index = plan.InsertionIndex < 0 ? destination.ChildrenCount : Math.Clamp(plan.InsertionIndex, 0, destination.ChildrenCount);
                foreach (var content in Contents)
                {
                    // Never reclaim a later tab deliberately transferred by an
                    // application collection observer. Its side effects are not
                    // rolled back or overwritten by this operation.
                    if (!WorkspaceCurrent() || !ReferenceEquals(destination.Root, _root) || !ReferenceEquals(content.FindParent<LayoutFloatingWindow>(), _model) || !DockOperations.CanDock(content, destination, DockPosition.Inside))
                        return false;
                    destination.InsertChildAt(Math.Min(index++, destination.ChildrenCount), content);
                    if (!ReferenceEquals(content.Parent, destination))
                        return false;
                    content.SetPrevious(null, 0);
                }
            }
            else
            {
                var asDocument = plan.Type is >= DropTargetType.DocumentPaneDockLeft and <= DropTargetType.DocumentPaneDockInside;
                ILayoutPanelElement incoming = !asDocument && _model is LayoutAnchorableFloatingWindow { RootPanel: { } panel } ? panel : new LayoutDocumentPane();
                if ((int)plan.Type <= 3)
                    DockOperations.AddAtRoot(_root, incoming, plan.Position switch
                    {
                        DockPosition.Left => AnchorSide.Left,
                        DockPosition.Top => AnchorSide.Top,
                        DockPosition.Bottom => AnchorSide.Bottom,
                        _ => AnchorSide.Right
                    });
                else if (!InsertBeside(target, incoming, plan.Position))
                    return false;
                if (!WorkspaceCurrent() || !ReferenceEquals(incoming.Root, _root))
                    return false;
                if (incoming is LayoutDocumentPane documents)
                    foreach (var content in Contents)
                    {
                        if (!WorkspaceCurrent() || !ReferenceEquals(content.FindParent<LayoutFloatingWindow>(), _model) || !DockOperations.CanMove(content))
                            return false;
                        documents.Children.Add(content);
                        if (!ReferenceEquals(content.Parent, documents))
                            return false;
                    }

                foreach (var content in Contents)
                    if (ReferenceEquals(content.Root, _root))
                        content.SetPrevious(null, 0);
            }

            if (!WorkspaceCurrent())
                return false;
            if (ReferenceEquals(Representative.Root, _root))
                Representative.IsActive = true;
            _root.CollectGarbage();
            return Contents.All(c => ReferenceEquals(c.Root, _root) && !ReferenceEquals(c.FindParent<LayoutFloatingWindow>(), _model));
        }
    }

    private bool WorkspaceCurrent() => ReferenceEquals(_manager.Layout, _root) && ReferenceEquals(_root.Manager, _manager);
    private static IEnumerable<ILayoutElement> Ancestors(ILayoutElement element)
    {
        for (ILayoutElement? current = element; current != null; current = current.Parent)
            yield return current;
    }

    private static bool InsertBeside(ILayoutGroup target, ILayoutPanelElement incoming, DockPosition position)
    {
        if (target is not ILayoutPanelElement anchor || target.Parent is not ILayoutGroup parent)
            return false;
        var document = incoming is ILayoutDocumentPane;
        while (parent is LayoutDocumentPaneGroup && !document || parent is LayoutAnchorablePaneGroup && document)
        {
            if (parent is not ILayoutPanelElement outer || parent.Parent is not ILayoutGroup outerParent)
                return false;
            anchor = outer;
            parent = outerParent;
        }

        var orientation = position is DockPosition.Left or DockPosition.Right ? Orientation.Horizontal : Orientation.Vertical;
        var before = position is DockPosition.Left or DockPosition.Top;
        if (parent is ILayoutOrientableGroup oriented && oriented.Orientation == orientation)
            parent.InsertChildAt(parent.IndexOfChild(anchor) + (before ? 0 : 1), incoming);
        else
        {
            ILayoutGroup? wrapper = parent switch
            {
                LayoutPanel => new LayoutPanel
                {
                    Orientation = orientation
                },
                LayoutDocumentPaneGroup when incoming is ILayoutDocumentPane && anchor is ILayoutDocumentPane => new LayoutDocumentPaneGroup
                {
                    Orientation = orientation
                },
                LayoutAnchorablePaneGroup when incoming is ILayoutAnchorablePane && anchor is ILayoutAnchorablePane => new LayoutAnchorablePaneGroup
                {
                    Orientation = orientation
                },
                _ => null
            };
            if (wrapper == null)
                return false;
            parent.ReplaceChild(anchor, wrapper);
            if (!ReferenceEquals(wrapper.Parent, parent))
                return false;
            wrapper.InsertChildAt(0, before ? incoming : anchor);
            wrapper.InsertChildAt(1, before ? anchor : incoming);
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var(node, _)in _nodes)
            node.PropertyChanged -= OnNodeChanged;
        _manager.LayoutChanged -= OnLayoutChanged;
        Window.UnregisterPropertyChangedCallback(Control.IsEnabledProperty, _enabledToken);
        Window.UnregisterPropertyChangedCallback(LayoutFloatingWindowControl.IsContentImmutableProperty, _immutableToken);
        _manager.UnregisterPropertyChangedCallback(Control.IsEnabledProperty, _managerEnabledToken);
    }
}

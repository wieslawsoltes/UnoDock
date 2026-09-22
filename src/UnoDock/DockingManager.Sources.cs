using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock;

public partial class DockingManager
{
    bool Xceed.Wpf.AvalonDock.Compatibility.IWeakEventListener.ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(managerType); ArgumentNullException.ThrowIfNull(e);
        if (_disposed || DispatcherQueue?.HasThreadAccess == false) return false;
        return OnReceiveWeakEvent(managerType, sender, e);
    }
    /// <summary>Return false without calling base to suppress default collection reconciliation.</summary>
    protected virtual bool OnReceiveWeakEvent(Type managerType, object sender, EventArgs e)
    {
        if (_disposed || managerType != typeof(INotifyCollectionChanged) || e is not NotifyCollectionChangedEventArgs ||
            (!ReferenceEquals(sender, DocumentsSource) && !ReferenceEquals(sender, AnchorablesSource))) return false;
        SourceChanged(); return true;
    }
    internal void ReceiveSourceEvent(SourceObserver observer, object sender, NotifyCollectionChangedEventArgs e)
    {
        if (_disposed) return;
        void Deliver()
        {
            // Events queued by replaced/disposed subscriptions cannot reach new sources.
            if (!_disposed && (ReferenceEquals(observer, _documentObserver) || ReferenceEquals(observer, _anchorableObserver)))
                ((Xceed.Wpf.AvalonDock.Compatibility.IWeakEventListener)this).ReceiveWeakEvent(typeof(INotifyCollectionChanged), sender, e);
        }
        if (DispatcherQueue?.HasThreadAccess == false) DispatcherQueue.TryEnqueue(Deliver);
        else Deliver();
    }
    private bool _sourcesDirty;
    private int _sourceDispatchPending;
    internal void SourceChanged()
    {
        if (DispatcherQueue?.HasThreadAccess == false)
        {
            if (Interlocked.Exchange(ref _sourceDispatchPending, 1) != 0) return;
            if (!DispatcherQueue.TryEnqueue(() => { Interlocked.Exchange(ref _sourceDispatchPending, 0); ReconcileSources(); }))
                Interlocked.Exchange(ref _sourceDispatchPending, 0);
            return;
        }
        ReconcileSources();
    }
    private void ReconcileSources()
    {
        if (_disposed) return;
        _sourcesDirty = true;
        if (_suspendSources > 0 || _reconcilingSources) return;
        _reconcilingSources = true;
        try
        {
            for (var pass = 0; _sourcesDirty; pass++)
            {
                if (pass == 64) throw new InvalidOperationException("Docking source callbacks did not converge after 64 reconciliation passes.");
                _sourcesDirty = false;
                var root = Layout;
                // Enumerating user sequences may run user code or throw. Snapshot both
                // before removing anything from either collection.
                var documents = Snapshot(DocumentsSource);
                var anchorables = Snapshot(AnchorablesSource);
                if (!ReferenceEquals(Layout, root)) { _sourcesDirty = true; continue; }
                using var batch = root.BeginUpdate();
                Reconcile(root, documents, _documents, true);
                if (ReferenceEquals(Layout, root)) Reconcile(root, anchorables, _anchorables, false);
                else _sourcesDirty = true;
            }
        }
        finally { _reconcilingSources = false; }
    }
    private static object[] Snapshot(IEnumerable? source) => source?.Cast<object>().Where(o => o != null).Distinct(ReferenceEqualityComparer.Instance).ToArray() ?? [];
    private void Reconcile(LayoutRoot root, object[] values, List<SourceEntry> entries, bool documents)
    {
        var wanted = new HashSet<object>(values, ReferenceEqualityComparer.Instance);
        foreach (var entry in entries.Where(e => !wanted.Contains(e.Value)).ToArray())
        {
            if (!ReferenceEquals(Layout, root)) return;
            entry.Model.Parent?.RemoveChild(entry.Model);
            entries.Remove(entry);
        }
        var tracked = new HashSet<object>(entries.Select(e => e.Value), ReferenceEqualityComparer.Instance);
        var models = new Dictionary<object, LayoutContent>(ReferenceEqualityComparer.Instance);
        foreach (var model in root.Descendents().OfType<LayoutContent>())
            if (model.Content is { } value && (documents ? model is LayoutDocument : model is LayoutAnchorable)) models.TryAdd(value, model);
        foreach (var value in values)
        {
            if (!ReferenceEquals(Layout, root)) return;
            if (!tracked.Add(value)) continue;
            models.TryGetValue(value, out var existing);
            var descriptor = value as IDockContent;
            LayoutContent model = existing ?? (documents ? value as LayoutDocument ?? new LayoutDocument() : value as LayoutAnchorable ?? new LayoutAnchorable());
            if (existing == null)
            {
                if (!ReferenceEquals(model, value)) { model.Content = value; model.Title = descriptor?.Title ?? value.ToString(); }
                model.ContentId ??= descriptor?.ContentId;
                if (model.Parent == null)
                {
                    if (model is LayoutAnchorable a) DockOperations.AddAnchorable(root, a, AnchorableShowStrategy.Right);
                    else
                    {
                        var pane = root.RootPanel.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
                        if (pane == null) { pane = new(); root.RootPanel.Children.Add(pane); }
                        var strategy = LayoutUpdateStrategy;
                        var handled = strategy?.BeforeInsertDocument(root, (LayoutDocument)model, pane) == true;
                        if (!ReferenceEquals(Layout, root) || !ReferenceEquals(pane.Root, root)) return;
                        if (!handled && model.Parent == null) pane.Children.Add(model);
                        if (ReferenceEquals(model.Root, root)) strategy?.AfterInsertDocument(root, (LayoutDocument)model);
                    }
                }
            }
            if (!ReferenceEquals(Layout, root)) return;
            entries.Add(new(value, model));
        }
    }
}

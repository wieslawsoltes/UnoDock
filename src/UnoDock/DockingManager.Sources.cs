using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock;

public partial class DockingManager
{
    bool UnoDock.Compatibility.IWeakEventListener.ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(managerType);
        ArgumentNullException.ThrowIfNull(e);
        if (_disposed || DispatcherQueue?.HasThreadAccess == false)
            return false;
        return OnReceiveWeakEvent(managerType, sender, e);
    }

    /// <summary>Return false without calling base to suppress default collection reconciliation.</summary>
    protected virtual bool OnReceiveWeakEvent(Type managerType, object sender, EventArgs e)
    {
        if (_disposed || managerType != typeof(INotifyCollectionChanged) || e is not NotifyCollectionChangedEventArgs || (!ReferenceEquals(sender, DocumentsSource) && !ReferenceEquals(sender, AnchorablesSource)))
            return false;
        SourceChanged();
        return true;
    }

    internal void ReceiveSourceEvent(SourceObserver observer, object sender, NotifyCollectionChangedEventArgs e)
    {
        if (_disposed)
            return;
        void Deliver()
        {
            if (!_disposed && (ReferenceEquals(observer, _documentObserver) || ReferenceEquals(observer, _anchorableObserver)))
                ((UnoDock.Compatibility.IWeakEventListener)this).ReceiveWeakEvent(typeof(INotifyCollectionChanged), sender, e);
        }

        if (DispatcherQueue?.HasThreadAccess == false)
            DispatcherQueue.TryEnqueue(Deliver);
        else
            Deliver();
    }

    private bool _sourcesDirty;
    private int _sourceDispatchPending;
    private long _sourceRevision;
    private sealed record SourcePass(LayoutRoot Root, IEnumerable? Documents, IEnumerable? Anchorables, ILayoutUpdateStrategy? Strategy, long Revision);
    internal void SourceChanged()
    {
        if (DispatcherQueue?.HasThreadAccess == false)
        {
            if (Interlocked.Exchange(ref _sourceDispatchPending, 1) != 0)
                return;
            if (!DispatcherQueue.TryEnqueue(() =>
            {
                Interlocked.Exchange(ref _sourceDispatchPending, 0);
                ReconcileSources();
            }))
                Interlocked.Exchange(ref _sourceDispatchPending, 0);
            return;
        }

        ReconcileSources();
    }

    private bool IsCurrent(SourcePass pass) => !_disposed && _sourceRevision == pass.Revision && ReferenceEquals(Layout, pass.Root) && ReferenceEquals(_attachedLayout, pass.Root) && ReferenceEquals(pass.Root.Manager, this) && ReferenceEquals(DocumentsSource, pass.Documents) && ReferenceEquals(AnchorablesSource, pass.Anchorables) && ReferenceEquals(LayoutUpdateStrategy, pass.Strategy);
    private void ReconcileSources()
    {
        if (_disposed)
            return;
        // A notification invalidates an in-flight pass even when collection/root
        // identity ends up unchanged (remove/re-add, replace/restore and Reset).
        unchecked
        {
            _sourceRevision++;
        }

        _sourcesDirty = true;
        if (_suspendSources > 0 || _reconcilingSources)
            return;
        _reconcilingSources = true;
        try
        {
            for (var attempt = 0; _sourcesDirty && !_disposed; attempt++)
            {
                if (attempt == 64)
                    throw new InvalidOperationException("Docking source callbacks did not converge after 64 reconciliation passes.");
                _sourcesDirty = false;
                var pass = new SourcePass(Layout, DocumentsSource, AnchorablesSource, LayoutUpdateStrategy, _sourceRevision);
                if (!IsCurrent(pass))
                    return;
                var documents = SnapshotSource(pass.Documents, pass);
                if (!IsCurrent(pass))
                {
                    _sourcesDirty = true;
                    continue;
                }

                var anchorables = SnapshotSource(pass.Anchorables, pass);
                if (!IsCurrent(pass))
                {
                    _sourcesDirty = true;
                    continue;
                }

                // Neither source is mutated if enumeration or preflight fails.
                ValidateDirectModels(pass.Root, documents, true);
                ValidateDirectModels(pass.Root, anchorables, false);
                using (pass.Root.BeginUpdate())
                {
                    Reconcile(pass, documents, _documents, true);
                    if (IsCurrent(pass))
                        Reconcile(pass, anchorables, _anchorables, false);
                }

                if (!IsCurrent(pass))
                    _sourcesDirty = true;
            }
        }
        finally
        {
            _reconcilingSources = false;
        }
    }

    private object[] SnapshotSource(IEnumerable? source, SourcePass pass)
    {
        if (source == null)
            return [];
        var values = new List<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var iterator = source.GetEnumerator();
        try
        {
            while (IsCurrent(pass))
            {
                var more = iterator.MoveNext();
                if (!IsCurrent(pass) || !more)
                    break;
                var value = iterator.Current;
                if (!IsCurrent(pass))
                    break;
                if (value != null && seen.Add(value))
                    values.Add(value);
            }
        }
        finally
        {
            (iterator as IDisposable)?.Dispose();
        }

        return values.ToArray();
    }

    private static void ValidateDirectModels(LayoutRoot root, object[] values, bool documents)
    {
        foreach (var value in values)
        {
            LayoutContent? model = documents ? value as LayoutDocument : value as LayoutAnchorable;
            if (model?.Parent != null && !ReferenceEquals(model.Root, root))
                throw new InvalidOperationException("A source model already belongs to another layout. Detach it explicitly before transferring ownership.");
        }
    }

    private static bool OwnsEntry(LayoutRoot root, SourceEntry entry) => (entry.Model.Parent == null || ReferenceEquals(entry.Model.Root, root)) && (ReferenceEquals(entry.Model, entry.Value) || ReferenceEquals(entry.Model.Content, entry.Value));
    private static void ForgetEntry(List<SourceEntry> entries, SourceEntry entry)
    {
        // SourceEntry is a record: List.Remove would evaluate payload Equals while
        // scanning other entries. Tracking must never invoke application equality.
        for (var index = 0; index < entries.Count; index++)
            if (ReferenceEquals(entries[index], entry))
            {
                entries.RemoveAt(index);
                return;
            }
    }

    private void Reconcile(SourcePass pass, object[] values, List<SourceEntry> entries, bool documents)
    {
        var root = pass.Root;
        // Application reparenting or Content replacement ends this source's removal
        // authority. In particular, never RemoveChild from a different manager.
        entries.RemoveAll(entry => !OwnsEntry(root, entry));
        var wanted = new HashSet<object>(values, ReferenceEqualityComparer.Instance);
        foreach (var entry in entries.Where(e => !wanted.Contains(e.Value)).ToArray())
        {
            if (!IsCurrent(pass))
                return;
            ForgetEntry(entries, entry);
            // A direct model and its payload can name the same existing model.
            // Dropping one alias must not close the model still named by the other.
            var stillNamed = wanted.Contains(entry.Model) || entry.Model.Content is { } content && wanted.Contains(content);
            if (!stillNamed && OwnsEntry(root, entry) && ReferenceEquals(entry.Model.Root, root))
                entry.Model.Parent?.RemoveChild(entry.Model);
        }

        if (!IsCurrent(pass))
            return;
        var tracked = new HashSet<object>(entries.Select(e => e.Value), ReferenceEqualityComparer.Instance);
        var models = new Dictionary<object, LayoutContent>(ReferenceEqualityComparer.Instance);
        foreach (var model in root.Descendents().OfType<LayoutContent>())
            if (model.Content is { } value && (documents ? model is LayoutDocument : model is LayoutAnchorable))
                models.TryAdd(value, model);
        foreach (var value in values)
        {
            if (!IsCurrent(pass))
                return;
            if (!tracked.Add(value))
                continue;
            models.TryGetValue(value, out var existing);
            LayoutContent model = existing ?? (documents ? value as LayoutDocument ?? new LayoutDocument() : value as LayoutAnchorable ?? new LayoutAnchorable());
            var entry = new SourceEntry(value, model);
            entries.Add(entry); // A throwing AfterInsert still leaves removable tracking.
            var accepted = false;
            try
            {
                if (existing == null)
                {
                    if (!ReferenceEquals(model, value))
                    {
                        var descriptor = value as IDockContent;
                        var title = descriptor?.Title;
                        if (!IsCurrent(pass))
                            continue;
                        title ??= value.ToString();
                        if (!IsCurrent(pass))
                            continue;
                        var id = descriptor?.ContentId;
                        if (!IsCurrent(pass))
                            continue;
                        model.Content = value;
                        if (!IsCurrent(pass))
                            continue;
                        model.Title = title;
                        if (!IsCurrent(pass))
                            continue;
                        model.ContentId ??= id;
                        if (!IsCurrent(pass))
                            continue;
                    }

                    if (model.Parent == null && !InsertSourceModel(pass, model))
                    {
                        _sourcesDirty = true;
                        return;
                    }
                }

                accepted = IsCurrent(pass);
                if (accepted && ReferenceEquals(model.Root, root) && model.Content is { } content)
                    models.TryAdd(content, model);
            }
            finally
            {
                // Retry an aborted unplaced model; retain an already attached one so
                // removal after a throwing callback cannot leave a source-owned orphan.
                if (!accepted && model.Parent == null)
                    ForgetEntry(entries, entry);
            }
        }
    }

    private bool InsertSourceModel(SourcePass pass, LayoutContent model)
    {
        var root = pass.Root;
        if (model is LayoutDocument document)
        {
            var pane = root.RootPanel.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
            if (pane == null)
            {
                pane = new();
                root.RootPanel.Children.Add(pane);
                if (!IsCurrent(pass))
                    return false;
            }

            var handled = pass.Strategy?.BeforeInsertDocument(root, document, pane) == true;
            if (!IsCurrent(pass))
                return false;
            if (!handled && document.Parent == null)
            {
                if (!ReferenceEquals(pane.Root, root))
                    return false;
                pane.Children.Add(document);
            }

            if (IsCurrent(pass) && ReferenceEquals(document.Root, root))
                pass.Strategy?.AfterInsertDocument(root, document);
        }
        else if (model is LayoutAnchorable anchorable)
        {
            var pane = root.RootPanel.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(p => p.GetSide() == AnchorSide.Right);
            if (pane == null)
            {
                pane = new();
                DockOperations.AddAtRoot(root, pane, AnchorSide.Right);
                if (!IsCurrent(pass))
                    return false;
            }

            var handled = pass.Strategy?.BeforeInsertAnchorable(root, anchorable, pane) == true;
            if (!IsCurrent(pass))
                return false;
            if (!handled && anchorable.Parent == null)
            {
                if (!ReferenceEquals(pane.Root, root))
                    return false;
                pane.Children.Add(anchorable);
            }

            if (IsCurrent(pass) && ReferenceEquals(anchorable.Root, root))
                pass.Strategy?.AfterInsertAnchorable(root, anchorable);
            if (IsCurrent(pass) && ReferenceEquals(anchorable.Root, root))
                anchorable.IsSelected = true;
        }

        return IsCurrent(pass);
    }
}

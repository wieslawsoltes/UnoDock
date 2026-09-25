using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Xml;

namespace UnoDock.Layout.Serialization;
public abstract class LayoutSerializer
{
    private static readonly ConditionalWeakTable<DockingManager, RestoreState> Restores = new();
    private sealed class RestoreState
    {
        internal int Active;
    }

    private sealed record PreviousContent(object? Content, Microsoft.UI.Xaml.Media.ImageSource? IconSource, object? ToolTip);
    private IDisposable? _transaction;
    private RestoreState? _state;
    private LayoutRoot? _original;
    private long _layoutToken;
    private bool _observing, _superseded;
    private readonly Dictionary<string, PreviousContent> _previous = new(StringComparer.Ordinal);
    public LayoutSerializer(DockingManager manager) => Manager = manager ?? throw new ArgumentNullException(nameof(manager));
    public DockingManager Manager { get; }

    public event EventHandler<LayoutSerializationCallbackEventArgs>? LayoutSerializationCallback;
    protected void StartDeserialization()
    {
        if (Manager.DispatcherQueue?.HasThreadAccess == false)
            throw new InvalidOperationException("Layout restoration must run on the manager's UI thread.");
        if (_state != null)
            throw new InvalidOperationException("A serializer instance is not reentrant.");
        var state = Restores.GetValue(Manager, static _ => new RestoreState());
        if (Interlocked.CompareExchange(ref state.Active, 1, 0) != 0)
            throw new InvalidOperationException("Another serializer is already restoring this docking manager.");
        _state = state;
        try
        {
            _transaction = Manager.BeginLayoutUpdate();
            _original = Manager.Layout;
            _superseded = false;
            _layoutToken = Manager.RegisterPropertyChangedCallback(DockingManager.LayoutProperty, (_, _) => _superseded = true);
            _observing = true;
            // Snapshot values, not live model objects: a resolver may mutate or detach
            // the old tree while later content IDs are still awaiting resolution.
            foreach (var content in _original.Descendents().OfType<LayoutContent>())
                if (content.ContentId is { } id)
                    _previous.TryAdd(id, new(content.Content, content.IconSource, content.ToolTip));
            EnsureCurrentRestore();
        }
        catch (Exception error)
        {
            try
            {
                EndDeserialization();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("Starting layout restoration and releasing its scope both failed.", error, cleanup);
            }

            throw;
        }
    }

    protected void EndDeserialization()
    {
        // Keep the lease through source reconciliation. Callbacks emitted by Dispose
        // must not start a second restore before this operation has fully unwound.
        try
        {
            _transaction?.Dispose();
        }
        finally
        {
            try
            {
                if (_observing)
                    Manager.UnregisterPropertyChangedCallback(DockingManager.LayoutProperty, _layoutToken);
            }
            finally
            {
                _observing = false;
                _transaction = null;
                _original = null;
                _previous.Clear();
                var state = _state;
                _state = null;
                if (state != null)
                    Interlocked.Exchange(ref state.Active, 0);
            }
        }
    }

    internal void EnsureCurrentRestore()
    {
        if (_state == null || _superseded || !ReferenceEquals(Manager.Layout, _original) || !ReferenceEquals(_original?.Manager, Manager))
            throw new InvalidOperationException("Layout restoration was superseded by a callback or the manager was disposed.");
    }

    protected virtual void FixupLayout(LayoutRoot layout)
    {
        using var batch = layout.BeginUpdate();
        foreach (var content in layout.Descendents().OfType<LayoutContent>().ToArray())
        {
            EnsureCurrentRestore();
            // A previous resolver may deliberately omit a whole subtree.
            if (!ReferenceEquals(content.Root, layout))
                continue;
            var previous = content.ContentId is { } id ? _previous.GetValueOrDefault(id) : null;
            var args = new LayoutSerializationCallbackEventArgs(content, previous?.Content);
            LayoutSerializationCallback?.Invoke(this, args);
            EnsureCurrentRestore();
            if (!ReferenceEquals(content.Root, layout))
                continue;
            if (args.Cancel)
            {
                content.Parent?.RemoveChild(content);
                continue;
            }

            content.Content = args.Content;
            EnsureCurrentRestore();
            if (!ReferenceEquals(content.Root, layout))
                continue;
            content.IconSource = previous?.IconSource;
            EnsureCurrentRestore();
            if (!ReferenceEquals(content.Root, layout))
                continue;
            content.ToolTip = previous?.ToolTip;
            EnsureCurrentRestore();
        }

        layout.CollectGarbage();
    }
}

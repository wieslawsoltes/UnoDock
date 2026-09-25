using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;
internal sealed class SourceObserver : IDisposable
{
    private readonly WeakReference<DockingManager> _manager;
    private INotifyCollectionChanged? _source;
    internal SourceObserver(DockingManager manager, IEnumerable source, bool documents)
    {
        _manager = new(manager); _source = source as INotifyCollectionChanged;
        if (_source != null) _source.CollectionChanged += Changed;
    }
    private void Changed(object? sender, NotifyCollectionChangedEventArgs args)
    { if (_source is { } source && _manager.TryGetTarget(out var manager)) manager.ReceiveSourceEvent(this, source, args); else Dispose(); }
    public void Dispose() { if (Interlocked.Exchange(ref _source, null) is { } source) source.CollectionChanged -= Changed; }
}

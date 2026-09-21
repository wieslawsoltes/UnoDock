namespace UnoDock.Core;

/// <summary>Thread-affine, nestable notification batching with nonrecursive reentrant delivery.</summary>
public sealed class UpdateBatch(Action flush)
{
    private readonly Action _flush = flush ?? throw new ArgumentNullException(nameof(flush));
    private int _depth;
    private bool _dirty, _flushing;
    public IDisposable Begin() { checked { _depth++; } return new Scope(this); }
    public void Invalidate() { _dirty = true; Drain(); }
    private void End() { if (_depth <= 0) throw new InvalidOperationException("Unbalanced update scope."); _depth--; Drain(); }
    private void Drain()
    {
        if (_depth != 0 || _flushing || !_dirty) return;
        _flushing = true;
        try
        {
            var iterations = 0;
            while (_depth == 0 && _dirty)
            {
                if (++iterations > 1024) throw new InvalidOperationException("Layout notification did not converge after 1024 passes.");
                _dirty = false;
                _flush();
            }
        }
        finally { _flushing = false; }
    }
    private sealed class Scope(UpdateBatch owner) : IDisposable
    {
        private UpdateBatch? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.End();
    }
}

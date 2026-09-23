using Microsoft.UI.Dispatching;

namespace UnoDock.Compatibility;

/// <summary>An explicit, UI-thread-owned binding transfer for CultureInfo converters.
/// Unlike a native WinUI Binding, this preserves DoNothing without invoking FallbackValue.
/// Delegates provide source access without reflection and allow multi-value projections.
/// Dispose before releasing the view to remove source and dependency-property subscriptions.</summary>
public sealed class ConverterBinding : IDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, Dictionary<DependencyProperty, ConverterBinding>> Targets = new();
    private readonly FrameworkElement _target;
    private readonly DependencyProperty _property;
    private readonly Func<object?> _readSource;
    private readonly Func<object?, object?> _convert;
    private readonly Action<object?>? _writeSource;
    private readonly Func<object?, object?>? _convertBack;
    private readonly INotifyPropertyChanged[] _sources;
    private readonly string? _sourceProperty;
    private readonly object? _fallback;
    private readonly bool _hasFallback;
    private readonly DispatcherQueue _dispatcher;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private long? _targetToken;
    private bool _dirty, _updating;
    private int _targetWriteDepth, _sourceWriteDepth, _queued;
    private volatile bool _disposed;

    /// <summary>Attaches and performs the initial source-to-target transfer. A null source
    /// property filter observes every property, supporting multi-value projection delegates.
    /// The source and target delegates always execute on the creating UI thread.</summary>
    public ConverterBinding(FrameworkElement target, DependencyProperty targetProperty,
        Func<object?> readSource, Func<object?, object?> convert,
        IEnumerable<INotifyPropertyChanged>? sources = null, string? sourceProperty = null,
        Action<object?>? writeSource = null, Func<object?, object?>? convertBack = null,
        object? fallbackValue = null, bool useFallbackValue = false)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(readSource); ArgumentNullException.ThrowIfNull(convert);
        if ((writeSource == null) != (convertBack == null))
            throw new ArgumentException("Two-way binding requires both source-write and reverse-conversion delegates.");
        if (!target.DispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException("Create converter bindings on the target UI thread.");
        if (target.GetBindingExpression(targetProperty) != null)
            throw new InvalidOperationException("The target already has a native binding. Remove it explicitly before attaching ConverterBinding.");
        _target = target; _property = targetProperty; _readSource = readSource; _convert = convert;
        _writeSource = writeSource; _convertBack = convertBack; _sourceProperty = sourceProperty;
        _fallback = fallbackValue; _hasFallback = useFallbackValue; _dispatcher = target.DispatcherQueue;
        _sources = sources?.Distinct(ReferenceEqualityComparer.Instance).Cast<INotifyPropertyChanged>().ToArray() ?? [];
        if (_sources.Any(source => source == null)) throw new ArgumentException("A notification source cannot be null.", nameof(sources));
        var bindings = Targets.GetOrCreateValue(target);
        if (bindings.ContainsKey(targetProperty)) throw new InvalidOperationException("A converter binding already owns this target property.");
        bindings.Add(targetProperty, this);
        var subscribed = 0;
        try
        {
            foreach (var source in _sources) { source.PropertyChanged += SourceChanged; subscribed++; }
            if (writeSource != null) _targetToken = target.RegisterPropertyChangedCallback(targetProperty, TargetChanged);
            UpdateTarget();
        }
        catch
        {
            _disposed = true; bindings.Remove(targetProperty);
            for (var i = 0; i < subscribed; i++) _sources[i].PropertyChanged -= SourceChanged;
            if (_targetToken is { } token) target.UnregisterPropertyChangedCallback(targetProperty, token);
            throw;
        }
    }

    /// <summary>Reads and converts the source now; callable only on the owning UI thread.</summary>
    public void UpdateTarget()
    {
        VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        if (_target.GetBindingExpression(_property) != null)
            throw new InvalidOperationException("The target acquired a native binding. Dispose ConverterBinding before changing its binding owner.");
        _dirty = true;
        if (_updating || _sourceWriteDepth != 0) return;
        _updating = true;
        try
        {
            // Reentrant source changes during conversion are drained, never discarded.
            // A non-convergent user conversion is rejected instead of locking the UI.
            var iterations = 0;
            while (_dirty && !_disposed)
            {
                if (++iterations > 64) throw new InvalidOperationException("Converter binding did not converge after 64 reentrant transfers.");
                _dirty = false;
                var value = _convert(_readSource());
                if (_disposed || ReferenceEquals(value, BindingValue.DoNothing)) continue;
                if (ReferenceEquals(value, DependencyProperty.UnsetValue))
                {
                    if (!_hasFallback) continue;
                    value = _fallback;
                }
                if (ReferenceEquals(value, BindingValue.DoNothing) || ReferenceEquals(value, DependencyProperty.UnsetValue)) continue;
                if (Equals(_target.GetValue(_property), value)) continue;
                _targetWriteDepth++;
                try { _target.SetValue(_property, value); }
                finally { _targetWriteDepth--; }
            }
        }
        finally { _updating = false; }
    }

    private void SourceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || !string.IsNullOrEmpty(_sourceProperty) && !string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != _sourceProperty) return;
        if (Environment.CurrentManagedThreadId == _ownerThread) { UpdateTarget(); return; }
        // Background notifications are coalesced, while the source value is read only
        // on the UI thread. Dispose can invalidate already queued work safely.
        if (Interlocked.Exchange(ref _queued, 1) != 0) return;
        if (!_dispatcher.TryEnqueue(() =>
        {
            Interlocked.Exchange(ref _queued, 0);
            if (!_disposed) UpdateTarget();
        }))
        {
            Interlocked.Exchange(ref _queued, 0);
            if (!_disposed) throw new InvalidOperationException("The target dispatcher is shutting down.");
        }
    }

    private void TargetChanged(DependencyObject sender, DependencyProperty property)
    {
        if (_disposed || _targetWriteDepth != 0 || _sourceWriteDepth != 0) return;
        VerifyAccess();
        _sourceWriteDepth++;
        try
        {
            var value = _convertBack!(_target.GetValue(_property));
            if (!_disposed && !ReferenceEquals(value, BindingValue.DoNothing) && !ReferenceEquals(value, DependencyProperty.UnsetValue))
                _writeSource!(value);
        }
        finally { _sourceWriteDepth--; }
        if (_dirty && !_disposed) UpdateTarget();
    }

    public void Dispose()
    {
        VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        if (Targets.TryGetValue(_target, out var bindings)) bindings.Remove(_property);
        foreach (var source in _sources) source.PropertyChanged -= SourceChanged;
        if (_targetToken is { } token) _target.UnregisterPropertyChangedCallback(_property, token);
        _targetToken = null;
    }
    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Access converter bindings on the creating UI thread.");
    }
}
